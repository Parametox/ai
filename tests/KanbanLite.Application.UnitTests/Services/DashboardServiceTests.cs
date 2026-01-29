using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class DashboardServiceTests
{
    private readonly IDashboardRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _repository = Substitute.For<IDashboardRepository>();
        _currentUser = Substitute.For<ICurrentUser>();
        _sut = new DashboardService(_repository, _currentUser);
    }

    [Fact]
    public async Task GetAsync_WithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(false);

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
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(true);

        _repository.GetStatusCountsForActiveProjectsAsync().Returns(new Dictionary<string, int>
        {
            { BatchStatus.New.ToString(), 1 },
            { BatchStatus.InProgress.ToString(), 1 },
            { BatchStatus.Done.ToString(), 1 }
        });

        _repository.GetStageCountsForActiveProjectsAsync().Returns(new Dictionary<short, int>
        {
            { (short)ProductionStage.Design, 1 },
            { (short)ProductionStage.Print, 1 }
        });

        _repository.GetUrgentOrdersAsync(Arg.Any<DateTime>()).Returns(new List<DashboardUrgentOrderDto>());

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
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(true);

        _repository.GetStatusCountsForActiveProjectsAsync().Returns(new Dictionary<string, int>
        {
            { BatchStatus.InProgress.ToString(), 21 }
        });

        _repository.GetStageCountsForActiveProjectsAsync().Returns(new Dictionary<short, int>());
        _repository.GetUrgentOrdersAsync(Arg.Any<DateTime>()).Returns(new List<DashboardUrgentOrderDto>());

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Warnings.Should().Contain(w => w.Code == "InProgressSoftLimitExceeded");
    }

    [Fact]
    public async Task GetAsync_HandlesUnexpectedException()
    {
         // Arrange
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(true);

        // Using Throws (if ExceptionExtensions imported)
        _repository.GetStatusCountsForActiveProjectsAsync().Throws(new Exception("Database connection failed"));

        // Act
        var result = await _sut.GetAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Database connection failed");
    }
}
