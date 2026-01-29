using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchServiceTests : TestBase
{
    private readonly BatchService _sut;
    private readonly IBatchRepository _batchRepository;

    public BatchServiceTests()
    {
        _batchRepository = Substitute.For<IBatchRepository>();
        _sut = new BatchService(_batchRepository, CurrentUser);
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

        var supaOrder = new SupabaseOrder { Id = 1, OrderNumber = "ORD-1", DueDate = DateTime.Today };
        var supaProject = new SupabaseProject { Id = 1, OrderId = 1, Order = supaOrder, ProjectNumber = "PRJ-1", IsCompleted = false };
        var supaBatch = new SupabaseBatch { Id = 1, ProjectId = 1, Project = supaProject, BatchNo = 1, Status = BatchStatus.New.ToString(), Stage = (short)ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        _batchRepository.GetBatchesAsync(Arg.Any<KanbanQuery>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([supaBatch], 1));
            
        _batchRepository.GetInProgressCountAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        // Act - Filter by Status 'New'
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Status = BatchStatus.New });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().BatchId.Should().Be(1);
    }

    [Fact]
    public async Task GetKanbanAsync_WithSearchQuery_CallsRepositoryCorrectly()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        
        var supaOrder = new SupabaseOrder { Id = 10, OrderNumber = "ALPHA", DueDate = DateTime.Today };
        var supaProject = new SupabaseProject { Id = 10, OrderId = 10, Order = supaOrder, ProjectNumber = "PRJ-ALPHA", IsCompleted = false };
        var supaBatch = new SupabaseBatch { Id = 10, ProjectId = 10, Project = supaProject, BatchNo = 1, Status = BatchStatus.New.ToString(), Stage = (short)ProductionStage.Design, UpdatedAt = DateTimeOffset.UtcNow };

        _batchRepository.GetBatchesAsync(Arg.Is<KanbanQuery>(q => q.Q == "ALPHA"), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([supaBatch], 1));

        // Act
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Q = "ALPHA" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().OrderNumber.Should().Be("ALPHA");
    }

    [Fact]
    public async Task GetKanbanAsync_SortByUpdatedAtDesc_CallsRepositoryCorrectly()
    {
        // Arrange
        CurrentUser.UserId.Returns("user");
        CurrentUser.IsInRole("Manager").Returns(true);
        
        var supaBatchOld = new SupabaseBatch { Id = 101, Status = "New", UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2) };
        var supaBatchNew = new SupabaseBatch { Id = 102, Status = "New", UpdatedAt = DateTimeOffset.UtcNow };
        
        // Note: The repository handles sorting, so the mock should return sorted if we wanted to test logic, 
        // but here we are testing if the Service passes the parameters correctly, or if the Mapping handles the list.
        // Since we mock the repository, we can't test if it sorts. We test if Service deals with what Repository returns.
        
        _batchRepository.GetBatchesAsync(Arg.Is<KanbanQuery>(q => q.Sort == KanbanSort.UpdatedAtDesc), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(([supaBatchNew, supaBatchOld], 2));

        // Act
        var result = await _sut.GetKanbanAsync(new KanbanQuery { Sort = KanbanSort.UpdatedAtDesc });

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value.Items.ToList();
        items.Should().HaveCount(2);
        items[0].BatchId.Should().Be(102);
        items[1].BatchId.Should().Be(101);
    }
}
