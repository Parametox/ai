using DataAccess.Entities;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchAuditServiceTests : TestBase
{
    private readonly BatchAuditService _sut;

    public BatchAuditServiceTests()
    {
        _sut = new BatchAuditService(DbFactory, CurrentUser);
    }

    [Fact]
    public async Task GetForBatchAsync_WithNullPage_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.GetForBatchAsync(1, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("Brak parametrów stronicowania");
    }

    [Fact]
    public async Task GetForBatchAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);
        var page = new PageQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetForBatchAsync_UserWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);
        CurrentUser.IsInRole("Operator").Returns(false);
        var page = new PageQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetForBatchAsync_BatchDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);
        var page = new PageQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.GetForBatchAsync(999, page);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
        result.Error.Message.Should().Contain("Batch o id=999 nie istnieje");
    }

    [Fact]
    public async Task GetForBatchAsync_WithValidRequest_ReturnsPagedResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = project.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        
        var audit1 = new BatchAuditLog 
        { 
            Id = 1, 
            BatchId = 1, 
            ChangedAt = DateTimeOffset.UtcNow.AddMinutes(-10), 
            ChangedByUserId = "user1",
            OldStatus = null,
            NewStatus = BatchStatus.New,
            OldStage = null,
            NewStage = ProductionStage.Design
        };
        
        var audit2 = new BatchAuditLog 
        { 
            Id = 2, 
            BatchId = 1, 
            ChangedAt = DateTimeOffset.UtcNow.AddMinutes(-5), 
            ChangedByUserId = "user2",
            OldStatus = BatchStatus.New,
            NewStatus = BatchStatus.InProgress,
            OldStage = ProductionStage.Design,
            NewStage = ProductionStage.Print
        };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        DbContext.BatchAuditLog.AddRange(audit1, audit2);
        await DbContext.SaveChangesAsync();

        var page = new PageQuery { Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        
        // Should be ordered by ChangedAt descending
        result.Value.Items.First().Id.Should().Be(2);
        result.Value.Items.Last().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetForBatchAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Operator").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = project.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        // Add 5 audit logs
        for (int i = 1; i <= 5; i++)
        {
            DbContext.BatchAuditLog.Add(new BatchAuditLog
            {
                Id = i,
                BatchId = 1,
                ChangedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
                ChangedByUserId = $"user{i}",
                OldStatus = null,
                NewStatus = BatchStatus.New,
                OldStage = null,
                NewStage = ProductionStage.Design
            });
        }

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        var page = new PageQuery { Page = 2, PageSize = 2 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(5);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetForBatchAsync_WithInvalidPageNumbers_NormalizesValues()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = project.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        // Page 0 should become 1, PageSize -5 should become 50
        var page = new PageQuery { Page = 0, PageSize = -5 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetForBatchAsync_WithPageSizeOver200_ClampsTo200()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = project.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        var page = new PageQuery { Page = 1, PageSize = 500 };

        // Act
        var result = await _sut.GetForBatchAsync(1, page);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(200);
    }
}
