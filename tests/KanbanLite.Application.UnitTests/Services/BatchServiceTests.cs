using DataAccess.Entities;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchServiceTests : TestBase
{
    private readonly BatchService _sut;

    public BatchServiceTests()
    {
        _sut = new BatchService(DbFactory, CurrentUser);
    }

    [Fact]
    public async Task GetKanbanAsync_WithNullQuery_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.GetKanbanAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task GetKanbanAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetKanbanAsync(new KanbanQuery());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetKanbanAsync_WithValidQuery_ReturnsFilteredResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        // use long ids
        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        var batch1 = new Batch { Id = 1, ProjectId = project.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        var batch2 = new Batch { Id = 2, ProjectId = project.Id, BatchNo = 2, Status = BatchStatus.InProgress, Stage = ProductionStage.Print, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(batch1, batch2);
        await DbContext.SaveChangesAsync();

        // Act - Filter by Status 'New'
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Status = BatchStatus.New });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().BatchId.Should().Be(batch1.Id);
    }

    [Fact]
    public async Task GetKanbanAsync_WithSearchQuery_FiltersResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order1 = new Order { Id = 10, OrderNumber = "ALPHA", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project1 = new Project { Id = 10, OrderId = order1.Id, ProjectNumber = "PRJ-ALPHA", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch1 = new Batch { Id = 10, ProjectId = project1.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        var order2 = new Order { Id = 20, OrderNumber = "BETA", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project2 = new Project { Id = 20, OrderId = order2.Id, ProjectNumber = "PRJ-BETA", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch2 = new Batch { Id = 20, ProjectId = project2.Id, BatchNo = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.AddRange(order1, order2);
        DbContext.Projects.AddRange(project1, project2);
        DbContext.Batches.AddRange(batch1, batch2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Q = "ALPHA" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().OrderNumber.Should().Be("ALPHA");
    }

    [Fact]
    public async Task GetKanbanAsync_SortByUpdatedAtDesc_ReturnsSortedResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 100, OrderNumber = "ORD", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 100, OrderId = order.Id, ProjectNumber = "PRJ", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        var batchOld = new Batch { Id = 101, ProjectId = project.Id, BatchNo = 1, UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2) };
        var batchNew = new Batch { Id = 102, ProjectId = project.Id, BatchNo = 2, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(batchOld, batchNew);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Sort = KanbanSort.UpdatedAtDesc });

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value.Items.ToList();
        items[0].BatchId.Should().Be(batchNew.Id);
        items[1].BatchId.Should().Be(batchOld.Id);
    }
}
