using DataAccess.Entities;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class ProjectServiceTests : TestBase
{
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(DbFactory, CurrentUser);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithNullQuery_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.GetAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task GetAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 1, PageSize = 10 });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetAsync_UserWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);
        CurrentUser.IsInRole("Operator").Returns(false);

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 1, PageSize = 10 });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAsync_WithValidQuery_ReturnsPagedResults()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 1, PageSize = 10 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Total.Should().Be(1);
        result.Value.Items.First().ProjectNumber.Should().Be("PRJ-1");
    }

    [Fact]
    public async Task GetAsync_FilterByIsCompleted_ReturnsOnlyMatchingProjects()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Operator").Returns(true);

        var order1 = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var order2 = new Order { Id = 2, OrderNumber = "ORD-2", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        
        var completedProject = new Project { Id = 1, OrderId = order1.Id, ProjectNumber = "PRJ-1", IsCompleted = true, CreatedAt = DateTimeOffset.UtcNow };
        var activeProject = new Project { Id = 2, OrderId = order2.Id, ProjectNumber = "PRJ-2", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.AddRange(order1, order2);
        DbContext.Projects.AddRange(completedProject, activeProject);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 1, PageSize = 10, IsCompleted = false });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().ProjectNumber.Should().Be("PRJ-2");
        result.Value.Items.First().IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        for (int i = 1; i <= 5; i++)
        {
            var order = new Order { Id = i, OrderNumber = $"ORD-{i}", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
            var project = new Project { Id = i, OrderId = order.Id, ProjectNumber = $"PRJ-{i}", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
            DbContext.Orders.Add(order);
            DbContext.Projects.Add(project);
        }
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 2, PageSize = 2 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(2);
        result.Value.Total.Should().Be(5);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.GetByIdAsync(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsProjectDetails()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat { Id = 1, Name = "A4", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order 
        { 
            Id = 1, 
            OrderNumber = "ORD-1", 
            ProductFormatId = 1,
            Quantity = 1000,
            DueDate = DateOnly.FromDateTime(DateTime.Today), 
            CreatedAt = DateTimeOffset.UtcNow 
        };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.Add(format);
        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(1);
        result.Value.ProjectNumber.Should().Be("PRJ-1");
        result.Value.Order.OrderNumber.Should().Be("ORD-1");
        result.Value.Order.ProductFormatName.Should().Be("A4");
    }

    [Fact]
    public async Task GetByIdAsync_WithBatches_IncludesBatchSummaries()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat { Id = 1, Name = "A4", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order 
        { 
            Id = 1, 
            OrderNumber = "ORD-1", 
            ProductFormatId = 1,
            Quantity = 1000,
            DueDate = DateOnly.FromDateTime(DateTime.Today), 
            CreatedAt = DateTimeOffset.UtcNow 
        };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        
        var batch1 = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 500, Status = BatchStatus.Done, Stage = ProductionStage.Ship, UpdatedAt = DateTimeOffset.UtcNow };
        var batch2 = new Batch { Id = 2, ProjectId = 1, BatchNo = 2, Quantity = 500, Status = BatchStatus.InProgress, Stage = ProductionStage.Print, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.Add(format);
        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(batch1, batch2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Batches.Should().HaveCount(2);
        result.Value.Batches.Should().BeInAscendingOrder(b => b.BatchNo);
    }

    [Fact]
    public async Task GetByIdAsync_WithAllBatchesDoneAndShipped_CanShipIsTrue()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat { Id = 1, Name = "A4", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order 
        { 
            Id = 1, 
            OrderNumber = "ORD-1", 
            ProductFormatId = 1,
            Quantity = 1000,
            DueDate = DateOnly.FromDateTime(DateTime.Today), 
            CreatedAt = DateTimeOffset.UtcNow 
        };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        
        var batch = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 1000, Status = BatchStatus.Done, Stage = ProductionStage.Ship, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.Add(format);
        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Completion.CanShip.Should().BeTrue();
        result.Value.Completion.Reason.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithIncompleteBatches_CanShipIsFalse()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(true);

        var format = new ProductFormat { Id = 1, Name = "A4", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order 
        { 
            Id = 1, 
            OrderNumber = "ORD-1", 
            ProductFormatId = 1,
            Quantity = 1000,
            DueDate = DateOnly.FromDateTime(DateTime.Today), 
            CreatedAt = DateTimeOffset.UtcNow 
        };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        
        var batch = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 1000, Status = BatchStatus.InProgress, Stage = ProductionStage.Print, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.ProductFormats.Add(format);
        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Completion.CanShip.Should().BeFalse();
        result.Value.Completion.Reason.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ShipToCustomerAsync Tests

    [Fact]
    public async Task ShipToCustomerAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task ShipToCustomerAsync_UserWithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task ShipToCustomerAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.ShipToCustomerAsync(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task ShipToCustomerAsync_AlreadyCompleted_ReturnsConflict()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project 
        { 
            Id = 1, 
            OrderId = order.Id, 
            ProjectNumber = "PRJ-1", 
            IsCompleted = true,
            CompletedAt = DateTimeOffset.UtcNow,
            CompletedByUserId = "manager",
            CreatedAt = DateTimeOffset.UtcNow 
        };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Conflict");
        result.Error.Message.Should().Contain("już oznaczony");
    }

    [Fact]
    public async Task ShipToCustomerAsync_WithoutBatches_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("bez batchy");
    }

    [Fact]
    public async Task ShipToCustomerAsync_WithIncompleteBatches_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 1000, Status = BatchStatus.InProgress, Stage = ProductionStage.Print, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("nie wszystkie batche");
    }

    [Fact]
    public async Task ShipToCustomerAsync_WithAllBatchesReady_MarksProjectAsCompleted()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch1 = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 500, Status = BatchStatus.Done, Stage = ProductionStage.Ship, UpdatedAt = DateTimeOffset.UtcNow };
        var batch2 = new Batch { Id = 2, ProjectId = 1, BatchNo = 2, Quantity = 500, Status = BatchStatus.Done, Stage = ProductionStage.Ship, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(batch1, batch2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompleted.Should().BeTrue();
        result.Value.CompletedByUserId.Should().Be("manager");
        result.Value.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task DeleteAsync_UserWithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task DeleteAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.DeleteAsync(999);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task DeleteAsync_ProjectWithoutBatches_DeletesSuccessfully()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ProjectWithBatches_DeletesBatchesAndProject()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch1 = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 500, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        var batch2 = new Batch { Id = 2, ProjectId = 1, BatchNo = 2, Quantity = 500, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(batch1, batch2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ProjectWithBatchAuditLogs_DeletesAuditLogsAndBatchesAndProject()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD-1", DueDate = DateOnly.FromDateTime(DateTime.Today), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = 1, BatchNo = 1, Quantity = 500, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        
        var auditLog = new BatchAuditLog
        {
            Id = 1,
            BatchId = 1,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedByUserId = "user1",
            OldStatus = null,
            NewStatus = BatchStatus.New,
            OldStage = null,
            NewStage = ProductionStage.Design
        };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.Add(batch);
        DbContext.BatchAuditLog.Add(auditLog);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
