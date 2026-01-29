using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class ProjectServiceTests : TestBase
{
    private readonly ProjectService _sut;
    private readonly IProjectRepository _projectRepository;
    private readonly IBatchRepository _batchRepository;

    public ProjectServiceTests()
    {
        _projectRepository = Substitute.For<IProjectRepository>();
        _batchRepository = Substitute.For<IBatchRepository>();
        _sut = new ProjectService(_projectRepository, _batchRepository, CurrentUser);
    }

    [Fact]
    public async Task GetAsync_CallsRepositoryCorrectly()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        CurrentUser.IsInRole("Operator").Returns(true);

        var supaOrder = new SupabaseOrder { Id = 1, OrderNumber = "ORD-1", DueDate = DateTime.Today };
        var supaProject = new SupabaseProject { Id = 1, OrderId = 1, Order = supaOrder, ProjectNumber = "PRJ-1", IsCompleted = false };

        _projectRepository.GetProjectsAsync(Arg.Any<ProjectQuery>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([supaProject], 1));

        // Act
        var result = await _sut.GetAsync(new ProjectQuery { Page = 1, PageSize = 10 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().ProjectNumber.Should().Be("PRJ-1");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProjectDetails()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);

        var supaOrder = new SupabaseOrder { Id = 1, OrderNumber = "ORD-1", DueDate = DateTime.Today };
        var supaProject = new SupabaseProject { Id = 1, OrderId = 1, Order = supaOrder, ProjectNumber = "PRJ-1", IsCompleted = false };
        var supaBatch = new SupabaseBatch { Id = 1, ProjectId = 1, BatchNo = 1, Status = BatchStatus.New.ToString(), Stage = (short)ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };
        supaProject.Batches = [supaBatch];

        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(supaProject);

        // Act
        var result = await _sut.GetByIdAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectNumber.Should().Be("PRJ-1");
        result.Value.Batches.Should().HaveCount(1);
        result.Value.Batches.First().BatchNo.Should().Be(1);
    }

    [Fact]
    public async Task ShipToCustomerAsync_WhenValid_ReturnsSuccess()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var supaProject = new SupabaseProject { Id = 1, IsCompleted = false };
        var supaBatch = new SupabaseBatch { Id = 1, Status = BatchStatus.Done.ToString(), Stage = (short)ProductionStage.Ship };
        supaProject.Batches = [supaBatch];

        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(supaProject);

        // Act
        var result = await _sut.ShipToCustomerAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _projectRepository.Received(1).UpdateAsync(Arg.Is<SupabaseProject>(p => p.IsCompleted == true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_ReturnsSuccess()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var supaProject = new SupabaseProject { Id = 1 };

        _projectRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(supaProject);

        // Act
        var result = await _sut.DeleteAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _projectRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }
}
