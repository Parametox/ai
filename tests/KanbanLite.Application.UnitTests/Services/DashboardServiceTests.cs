using DataAccess.Entities;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class DashboardServiceTests : TestBase
{
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(DbFactory, CurrentUser);
    }

    [Fact]
    public async Task GetAsync_WithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAsync_WithManagerRole_ReturnsAggregatedStats()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 1, OrderNumber = "ORD", DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = order.Id, ProjectNumber = "PRJ", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        var b1 = new Batch { Id = 1, ProjectId = project.Id, Status = BatchStatus.New, Stage = ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        var b2 = new Batch { Id = 2, ProjectId = project.Id, Status = BatchStatus.InProgress, Stage = ProductionStage.Print, UpdatedAt = DateTimeOffset.UtcNow };
        var b3 = new Batch { Id = 3, ProjectId = project.Id, Status = BatchStatus.Done, Stage = ProductionStage.Ship, UpdatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);
        DbContext.Batches.AddRange(b1, b2, b3);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;

        dto.CountsByStatus[BatchStatus.New].Should().Be(1);
        dto.CountsByStatus[BatchStatus.InProgress].Should().Be(1);
        dto.CountsByStatus[BatchStatus.Done].Should().Be(1);

        dto.CountsByStage[ProductionStage.Design].Should().Be(1);
        dto.CountsByStage[ProductionStage.Print].Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_ReturnsWarnings_WhenSoftLimitExceeded()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var order = new Order { Id = 10, OrderNumber = "ORD", DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 10, OrderId = order.Id, ProjectNumber = "PRJ", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.Add(order);
        DbContext.Projects.Add(project);

        // Add 21 InProgress batches
        var batches = Enumerable.Range(100, 21).Select(i => new Batch
        {
            Id = (long)i,
            ProjectId = project.Id,
            Status = BatchStatus.InProgress,
            Stage = ProductionStage.Print
        });
        DbContext.Batches.AddRange(batches);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().Contain(w => w.Code == "InProgressSoftLimitExceeded");
    }

    [Fact]
    public async Task GetAsync_ReturnsUrgentOrders()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var today = DateTime.Today;
        // Urgent: due in 3 days
        var urgentOrder = new Order { Id = 100, OrderNumber = "URGENT", DueDate = DateOnly.FromDateTime(today.AddDays(3)), CreatedAt = DateTimeOffset.UtcNow };
        var urgentProject = new Project { Id = 100, OrderId = urgentOrder.Id, ProjectNumber = "PRJ-URGENT", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        // Not Urgent: due in 10 days
        var normalOrder = new Order { Id = 200, OrderNumber = "NORMAL", DueDate = DateOnly.FromDateTime(today.AddDays(10)), CreatedAt = DateTimeOffset.UtcNow };
        var normalProject = new Project { Id = 200, OrderId = normalOrder.Id, ProjectNumber = "PRJ-NORMAL", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        DbContext.Orders.AddRange(urgentOrder, normalOrder);
        DbContext.Projects.AddRange(urgentProject, normalProject);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UrgentOrders.Should().Contain(o => o.OrderNumber == "URGENT");
        result.Value.UrgentOrders.Should().NotContain(o => o.OrderNumber == "NORMAL");
    }
}
