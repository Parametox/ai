using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Common;
using KanbanLite.Application.Services;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using NSubstitute;
using Xunit;

namespace KanbanLite.Application.UnitTests.Services;

public class BatchAuditServiceTests
{
    private readonly IBatchAuditRepository _batchAuditRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly ICurrentUser _currentUser;
    private readonly BatchAuditService _service;

    public BatchAuditServiceTests()
    {
        _batchAuditRepository = Substitute.For<IBatchAuditRepository>();
        _batchRepository = Substitute.For<IBatchRepository>();
        _currentUser = Substitute.For<ICurrentUser>();

        _service = new BatchAuditService(_batchAuditRepository, _batchRepository, _currentUser);
    }

    [Fact]
    public async Task GetForBatchAsync_ShouldReturnLogs_WhenBatchExists()
    {
        // Arrange
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(true);

        var batchId = 1;
        var batch = new SupabaseBatch { Id = batchId, BatchNo = 1, Status = "New", Stage = 1 };
        var logs = new List<SupabaseBatchAuditLog>
        {
            new() { Id = 1, BatchId = batchId, OldStatus = "New", NewStatus = "InProgress", ChangedAt = DateTimeOffset.UtcNow },
            new() { Id = 2, BatchId = batchId, OldStage = 1, NewStage = 2, ChangedAt = DateTimeOffset.UtcNow }
        };

        _batchRepository.GetByIdAsync(batchId).Returns(batch);
        _batchAuditRepository.GetForBatchAsync(batchId, 1, 10).Returns((logs, 2));

        // Act
        var result = await _service.GetForBatchAsync(batchId, new PageQuery(1, 10));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        // Verify mapping
        var item1 = result.Value.Items.First(x => x.Id == 1);
        item1.OldStatus.Should().Be(BatchStatus.New);
        item1.NewStatus.Should().Be(BatchStatus.InProgress);
    }

    [Fact]
    public async Task GetForBatchAsync_ShouldReturnNotFound_WhenBatchDoesNotExist()
    {
        // Arrange
        _currentUser.UserId.Returns("user");
        _currentUser.IsInRole("Manager").Returns(true);
        var batchId = 999;
        _batchRepository.GetByIdAsync(batchId).Returns((SupabaseBatch?)null);

        // Act
        var result = await _service.GetForBatchAsync(batchId, new PageQuery(1, 10));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NotFound");
    }
}
