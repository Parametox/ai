using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KanbanLite.Application.Common;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchSplitRuleServiceTests
{
    private readonly IBatchSplitRuleRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly BatchSplitRuleService _sut;

    public BatchSplitRuleServiceTests()
    {
        _repository = Substitute.For<IBatchSplitRuleRepository>();
        _currentUser = Substitute.For<ICurrentUser>();
        _sut = new BatchSplitRuleService(_repository, _currentUser);
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
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetAsync_UserWithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        _currentUser.UserId.Returns("user123");
        _currentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAsync_WithValidQuery_ReturnsAllRules()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var rules = new List<SupabaseBatchSplitRule>
        {
            new() { Id = 1, MinQty = 1, MaxQty = 100, Percent = 50, IsActive = true },
            new() { Id = 2, MinQty = 101, MaxQty = null, Percent = 75, IsActive = false }
        };

        _repository.GetAllAsync(null).Returns(rules);

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_WithActiveOnly_ReturnsActiveRules()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var rules = new List<SupabaseBatchSplitRule>
        {
            new() { Id = 1, MinQty = 1, MaxQty = 100, Percent = 50, IsActive = true }
        };

        _repository.GetAllAsync(true).Returns(rules);

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery(IsActive: true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value!.First().Id.Should().Be(1);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_NullDto_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.CreateAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.CreateAsync(new UpsertBatchSplitRuleRequest(0, 10, 0.5m, 1, null, true));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task CreateAsync_NotManager_ReturnsForbidden()
    {
        // Arrange
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(false);

        // Act
        var result = await _sut.CreateAsync(new UpsertBatchSplitRuleRequest(0, 10, 0.5m, 1, null, true));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task CreateAsync_Overlap_ReturnsValidationFailed()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var existingRules = new List<SupabaseBatchSplitRule>
        {
            new() { Id = 1, MinQty = 0, MaxQty = 100 }
        };
        _repository.GetActiveRulesAsync().Returns(existingRules);

        var dto = new UpsertBatchSplitRuleRequest(
            MinQty: 50,
            MaxQty: 150,
            Percent: 0.5m,
            MinBatchSize: 10,
            MaxBatchesPerProject: null,
            IsActive: true
        );

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error!.Message.Should().Contain("koliduje");
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesAndReturns()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        _repository.GetActiveRulesAsync().Returns(new List<SupabaseBatchSplitRule>());
        
        var dto = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 0.5m,
            MinBatchSize: 10,
            MaxBatchesPerProject: null,
            IsActive: true
        );

        SupabaseBatchSplitRule? createdRule = null;
        await _repository.CreateAsync(Arg.Do<SupabaseBatchSplitRule>(x => createdRule = x));

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        createdRule.Should().NotBeNull();
        createdRule!.MinQty.Should().Be(1);
        await _repository.Received(1).CreateAsync(Arg.Any<SupabaseBatchSplitRule>());
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);
        _repository.GetByIdAsync(1).Returns((SupabaseBatchSplitRule?)null);

        // Act
        var result = await _sut.UpdateAsync(1, new UpsertBatchSplitRuleRequest(10, 20, 0.5m, 1, null, true));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateAsync_Overlap_ReturnsValidationFailed()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var existingRule = new SupabaseBatchSplitRule { Id = 1, MinQty = 0, MaxQty = 50 };
        _repository.GetByIdAsync(1).Returns(existingRule);

        var allRules = new List<SupabaseBatchSplitRule>
        {
            existingRule,
            new() { Id = 2, MinQty = 100, MaxQty = 200 }
        };
        _repository.GetActiveRulesAsync().Returns(allRules);

        var dto = new UpsertBatchSplitRuleRequest(
            MinQty: 150, // Overlaps with rule 2
            MaxQty: 250,
            Percent: 50,
            MinBatchSize: 10,
            MaxBatchesPerProject: null,
            IsActive: true
        );

        // Act
        var result = await _sut.UpdateAsync(1, dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesAndReturns()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);

        var existingRule = new SupabaseBatchSplitRule { Id = 1, MinQty = 0, MaxQty = 50 };
        _repository.GetByIdAsync(1).Returns(existingRule);
        
        // No overlap check if not active, or mock active list to empty
        // Assuming update changes active status or just validates overlap even if inactive?
        // Logic says "if (request.IsActive)" then check overlap.
        // Let's pass IsActive = false so validation is skipped or mock empty list.
        _repository.GetActiveRulesAsync().Returns(new List<SupabaseBatchSplitRule>());

        var dto = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 60,
            Percent: 0.60m, // 60%
            MinBatchSize: 12,
            MaxBatchesPerProject: null,
            IsActive: false
        );

        // Act
        var result = await _sut.UpdateAsync(1, dto);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<SupabaseBatchSplitRule>(x => x.Id == 1 && x.Percent == 0.60m));
    }

    #endregion

     #region SetActiveAsync Tests
     // Added simple test since method exists
    [Fact]
    public async Task SetActiveAsync_Success_ReturnsDto()
    {
        // Arrange
        _currentUser.UserId.Returns("manager");
        _currentUser.IsInRole("Manager").Returns(true);
        var rule = new SupabaseBatchSplitRule { Id = 1 };
        _repository.GetByIdAsync(1).Returns(rule);
        _repository.GetActiveRulesAsync().Returns(new List<SupabaseBatchSplitRule>());

        // Act
        var result = await _sut.SetActiveAsync(1, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<SupabaseBatchSplitRule>(x => x.Id == 1 && x.IsActive == true));
    }
     #endregion
}
