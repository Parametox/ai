using DataAccess.Entities;
using FluentAssertions;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchSplitRuleServiceTests : TestBase
{
    private readonly BatchSplitRuleService _sut;

    public BatchSplitRuleServiceTests()
    {
        _sut = new BatchSplitRuleService(DbFactory, CurrentUser);
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
        var result = await _sut.GetAsync(new BatchSplitRuleQuery());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task GetAsync_UserWithoutManagerRole_ReturnsForbidden()
    {
        // Arrange
        CurrentUser.UserId.Returns("user123");
        CurrentUser.IsInRole("Manager").Returns(false);

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
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var rule1 = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            MaxBatchesPerProject = 5,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var rule2 = new BatchSplitRule
        {
            Id = 2,
            MinQty = 101,
            MaxQty = null,
            Percent = 75,
            MinBatchSize = 20,
            MaxBatchesPerProject = null,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.BatchSplitRules.AddRange(rule1, rule2);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_FilterByIsActive_ReturnsOnlyActiveRules()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var activeRule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var inactiveRule = new BatchSplitRule
        {
            Id = 2,
            MinQty = 101,
            MaxQty = 200,
            Percent = 75,
            MinBatchSize = 20,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.BatchSplitRules.AddRange(activeRule, inactiveRule);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync(new BatchSplitRuleQuery(IsActive: true));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Id.Should().Be(1);
        result.Value.First().IsActive.Should().BeTrue();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithNullRequest_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.CreateAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);
        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 50
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidMinQty_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 0, // Invalid
            MaxQty: 100,
            Percent: 50
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("minQty");
    }

    [Fact]
    public async Task CreateAsync_WithMaxQtyLessThanMinQty_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 100,
            MaxQty: 50, // Less than MinQty
            Percent: 50
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("maxQty");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidPercent_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 0 // Invalid (must be > 0)
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("percent");
    }

    [Fact]
    public async Task CreateAsync_WithPercentOver100_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 150 // Over 100
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidMinBatchSize_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 50,
            MinBatchSize: 0 // Invalid
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.FieldErrors.Should().ContainKey("minBatchSize");
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesRule()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 50,
            MinBatchSize: 10,
            MaxBatchesPerProject: 5,
            IsActive: true
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.MinQty.Should().Be(1);
        result.Value.MaxQty.Should().Be(100);
        result.Value.Percent.Should().Be(50);
        result.Value.MinBatchSize.Should().Be(10);
        result.Value.MaxBatchesPerProject.Should().Be(5);
        result.Value.IsActive.Should().BeTrue();

        // Verify it was saved
        var saved = await DbContext.BatchSplitRules.FindAsync(result.Value.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingActiveRange_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Existing active rule: 1-100
        var existingRule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.BatchSplitRules.Add(existingRule);
        await DbContext.SaveChangesAsync();

        // Try to create overlapping rule: 50-150
        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 50,
            MaxQty: 150,
            Percent: 50,
            IsActive: true
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("nakłada się");
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingInactiveRange_Succeeds()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Existing inactive rule: 1-100
        var existingRule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.BatchSplitRules.Add(existingRule);
        await DbContext.SaveChangesAsync();

        // Create new inactive overlapping rule: 50-150
        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 50,
            MaxQty: 150,
            Percent: 50,
            IsActive: false // Inactive, so overlap is OK
        );

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithNullRequest_ReturnsValidationFailed()
    {
        // Act
        var result = await _sut.UpdateAsync(1, null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);
        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 50
        );

        // Act
        var result = await _sut.UpdateAsync(1, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentRule_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 1,
            MaxQty: 100,
            Percent: 50
        );

        // Act
        var result = await _sut.UpdateAsync(999, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesRule()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var existingRule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.BatchSplitRules.Add(existingRule);
        await DbContext.SaveChangesAsync();

        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 10,
            MaxQty: 200,
            Percent: 75,
            MinBatchSize: 20,
            MaxBatchesPerProject: 10,
            IsActive: false
        );

        // Act
        var result = await _sut.UpdateAsync(1, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MinQty.Should().Be(10);
        result.Value.MaxQty.Should().Be(200);
        result.Value.Percent.Should().Be(75);
        result.Value.MinBatchSize.Should().Be(20);
        result.Value.MaxBatchesPerProject.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_WithOverlappingRange_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var rule1 = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var rule2 = new BatchSplitRule
        {
            Id = 2,
            MinQty = 200,
            MaxQty = 300,
            Percent = 60,
            MinBatchSize = 15,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.BatchSplitRules.AddRange(rule1, rule2);
        await DbContext.SaveChangesAsync();

        // Try to update rule2 to overlap with rule1
        var request = new UpsertBatchSplitRuleRequest(
            MinQty: 50,
            MaxQty: 150,
            Percent: 60,
            MinBatchSize: 15,
            IsActive: true
        );

        // Act
        var result = await _sut.UpdateAsync(2, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("nakłada się");
    }

    #endregion

    #region SetActiveAsync Tests

    [Fact]
    public async Task SetActiveAsync_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        CurrentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.SetActiveAsync(1, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task SetActiveAsync_NonExistentRule_ReturnsNotFound()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        // Act
        var result = await _sut.SetActiveAsync(999, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task SetActiveAsync_ActivatingWithoutOverlap_Succeeds()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var rule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.BatchSplitRules.Add(rule);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.SetActiveAsync(1, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_ActivatingWithOverlap_ReturnsValidationFailed()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var activeRule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var inactiveRule = new BatchSplitRule
        {
            Id = 2,
            MinQty = 50,
            MaxQty = 150,
            Percent = 60,
            MinBatchSize = 15,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.BatchSplitRules.AddRange(activeRule, inactiveRule);
        await DbContext.SaveChangesAsync();

        // Act - Try to activate rule2 which overlaps with rule1
        var result = await _sut.SetActiveAsync(2, true);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error.Message.Should().Contain("nakłada się");
    }

    [Fact]
    public async Task SetActiveAsync_Deactivating_Succeeds()
    {
        // Arrange
        CurrentUser.UserId.Returns("manager");
        CurrentUser.IsInRole("Manager").Returns(true);

        var rule = new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 50,
            MinBatchSize = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        DbContext.BatchSplitRules.Add(rule);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _sut.SetActiveAsync(1, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }

    #endregion
}
