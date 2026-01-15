using DataAccess;
using DataAccess.Entities;
using DataAccess.Enums;
using FluentAssertions;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace KanbanLite.Application.UnitTests;

public sealed class BatchServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_should_return_Forbidden_when_user_not_in_role()
    {
        await using var db = CreateDb();
        await SeedMinimalProjectWithBatch(db, status: BatchStatus.New, stage: ProductionStage.Design);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole(Arg.Any<string>()).Returns(false);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStatusAsync(batchId: 1, new UpdateBatchStatusRequest(BatchStatus.InProgress));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task UpdateStatusAsync_should_return_ValidationFailed_for_invalid_transition()
    {
        await using var db = CreateDb();
        await SeedMinimalProjectWithBatch(db, status: BatchStatus.New, stage: ProductionStage.Design);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStatusAsync(batchId: 1, new UpdateBatchStatusRequest(BatchStatus.Done));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateStatusAsync_should_write_audit_log_on_success()
    {
        await using var db = CreateDb();
        await SeedMinimalProjectWithBatch(db, status: BatchStatus.New, stage: ProductionStage.Design);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStatusAsync(batchId: 1, new UpdateBatchStatusRequest(BatchStatus.InProgress));

        result.IsSuccess.Should().BeTrue();
        (await db.BatchAuditLog.CountAsync()).Should().Be(1);

        var audit = await db.BatchAuditLog.SingleAsync();
        audit.BatchId.Should().Be(1);
        audit.ChangedByUserId.Should().Be("u1");
        audit.OldStatus.Should().Be(BatchStatus.New);
        audit.NewStatus.Should().Be(BatchStatus.InProgress);
        audit.OldStage.Should().BeNull();
        audit.NewStage.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_should_return_soft_limit_warning_when_inprogress_exceeds_20()
    {
        await using var db = CreateDb();

        // seed: 21 batchy InProgress (global soft limit)
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 100, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = 1, Order = order, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        db.ProductFormats.Add(pf);
        db.Orders.Add(order);
        db.Projects.Add(project);

        db.Batches.Add(new Batch { Id = 1, ProjectId = 1, Project = project, BatchNo = 1, Quantity = 1, Status = BatchStatus.New, Stage = ProductionStage.Design, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (var i = 2; i <= 22; i++)
        {
            db.Batches.Add(new Batch
            {
                Id = i,
                ProjectId = 1,
                Project = project,
                BatchNo = i,
                Quantity = 1,
                Status = BatchStatus.InProgress,
                Stage = ProductionStage.Print,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStatusAsync(batchId: 1, new UpdateBatchStatusRequest(BatchStatus.InProgress));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Warnings.Should().ContainSingle(x => x.Code == "InProgressSoftLimitExceeded");
        result.Value!.InProgressCount.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task UpdateStageAsync_should_return_ValidationFailed_when_stage_goes_backwards()
    {
        await using var db = CreateDb();
        await SeedMinimalProjectWithBatch(db, status: BatchStatus.InProgress, stage: ProductionStage.Print);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStageAsync(batchId: 1, new UpdateBatchStageRequest(ProductionStage.Design));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task UpdateStageAsync_should_write_audit_log_on_success()
    {
        await using var db = CreateDb();
        await SeedMinimalProjectWithBatch(db, status: BatchStatus.InProgress, stage: ProductionStage.Print);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchService(db, currentUser);

        var result = await sut.UpdateStageAsync(batchId: 1, new UpdateBatchStageRequest(ProductionStage.Pack));

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProgressPercent.Should().Be(80);

        (await db.BatchAuditLog.CountAsync()).Should().Be(1);
        var audit = await db.BatchAuditLog.SingleAsync();
        audit.OldStage.Should().Be(ProductionStage.Print);
        audit.NewStage.Should().Be(ProductionStage.Pack);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedMinimalProjectWithBatch(AppDbContext db, BatchStatus status, ProductionStage stage)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 100, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = 1, Order = order, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = 1, Project = project, BatchNo = 1, Quantity = 10, Status = status, Stage = stage, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        db.ProductFormats.Add(pf);
        db.Orders.Add(order);
        db.Projects.Add(project);
        db.Batches.Add(batch);

        await db.SaveChangesAsync();
    }
}

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task GetAsync_should_return_only_completed_when_filtered()
    {
        await using var db = CreateDb();

        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var o1 = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 10, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var o2 = new Order { Id = 2, OrderNumber = "ORD-2", Quantity = 10, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 3, 1), CreatedAt = DateTimeOffset.UtcNow };
        db.ProductFormats.Add(pf);
        db.Orders.AddRange(o1, o2);

        db.Projects.Add(new Project { Id = 1, OrderId = 1, Order = o1, ProjectNumber = "PRJ-1", IsCompleted = true, CompletedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
        db.Projects.Add(new Project { Id = 2, OrderId = 2, Order = o2, ProjectNumber = "PRJ-2", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new ProjectService(db, currentUser);

        var result = await sut.GetAsync(new ProjectQuery(IsCompleted: true, Page: 1, PageSize: 50));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value!.Items[0].ProjectNumber.Should().Be("PRJ-1");
    }

    [Fact]
    public async Task GetByIdAsync_should_return_ProjectDetails_and_canShip_true_when_ready()
    {
        await using var db = CreateDb();
        await SeedProjectWithReadyBatches(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new ProjectService(db, currentUser);

        var result = await sut.GetByIdAsync(id: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProjectNumber.Should().Be("PRJ-1");
        result.Value!.Completion.CanShip.Should().BeTrue();
        result.Value!.Batches.Should().HaveCount(2);
        result.Value!.Batches[0].ProgressPercent.Should().Be(100);
    }

    [Fact]
    public async Task ShipToCustomerAsync_should_return_Forbidden_for_non_manager()
    {
        await using var db = CreateDb();
        await SeedProjectWithReadyBatches(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(false);

        var sut = new ProjectService(db, currentUser);

        var result = await sut.ShipToCustomerAsync(projectId: 1);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task ShipToCustomerAsync_should_return_ValidationFailed_when_any_batch_not_ready()
    {
        await using var db = CreateDb();
        await SeedProjectWithNotReadyBatch(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new ProjectService(db, currentUser);

        var result = await sut.ShipToCustomerAsync(projectId: 1);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task ShipToCustomerAsync_should_mark_project_completed_when_ready()
    {
        await using var db = CreateDb();
        await SeedProjectWithReadyBatches(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new ProjectService(db, currentUser);

        var result = await sut.ShipToCustomerAsync(projectId: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCompleted.Should().BeTrue();
        result.Value!.CompletedByUserId.Should().Be("u1");

        var project = await db.Projects.SingleAsync(x => x.Id == 1);
        project.IsCompleted.Should().BeTrue();
        project.CompletedAt.Should().NotBeNull();
        project.CompletedByUserId.Should().Be("u1");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedProjectWithReadyBatches(AppDbContext db)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 100, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = 1, Order = order, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        db.ProductFormats.Add(pf);
        db.Orders.Add(order);
        db.Projects.Add(project);

        db.Batches.Add(new Batch { Id = 1, ProjectId = 1, Project = project, BatchNo = 1, Quantity = 50, Status = BatchStatus.Done, Stage = ProductionStage.Ship, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Batches.Add(new Batch { Id = 2, ProjectId = 1, Project = project, BatchNo = 2, Quantity = 50, Status = BatchStatus.Done, Stage = ProductionStage.Ship, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync();
    }

    private static async Task SeedProjectWithNotReadyBatch(AppDbContext db)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 100, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = 1, Order = order, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };

        db.ProductFormats.Add(pf);
        db.Orders.Add(order);
        db.Projects.Add(project);

        db.Batches.Add(new Batch { Id = 1, ProjectId = 1, Project = project, BatchNo = 1, Quantity = 50, Status = BatchStatus.Done, Stage = ProductionStage.Ship, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        db.Batches.Add(new Batch { Id = 2, ProjectId = 1, Project = project, BatchNo = 2, Quantity = 50, Status = BatchStatus.InProgress, Stage = ProductionStage.Pack, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync();
    }
}

public sealed class BatchAuditServiceTests
{
    [Fact]
    public async Task GetForBatchAsync_should_return_Forbidden_for_user_without_roles()
    {
        await using var db = CreateDb();
        await SeedBatchWithAudit(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole(Arg.Any<string>()).Returns(false);

        var sut = new BatchAuditService(db, currentUser);

        var result = await sut.GetForBatchAsync(batchId: 1, page: new PageQuery(Page: 1, PageSize: 50));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetForBatchAsync_should_page_results_desc_by_changedAt()
    {
        await using var db = CreateDb();
        await SeedBatchWithAudit(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Operator").Returns(true);

        var sut = new BatchAuditService(db, currentUser);

        var result = await sut.GetForBatchAsync(batchId: 1, page: new PageQuery(Page: 1, PageSize: 1));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value!.Total.Should().Be(2);
        result.Value!.Items[0].Id.Should().Be(2);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedBatchWithAudit(AppDbContext db)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var order = new Order { Id = 1, OrderNumber = "ORD-1", Quantity = 100, ProductFormatId = 1, ProductFormat = pf, DueDate = new DateOnly(2026, 2, 1), CreatedAt = DateTimeOffset.UtcNow };
        var project = new Project { Id = 1, OrderId = 1, Order = order, ProjectNumber = "PRJ-1", IsCompleted = false, CreatedAt = DateTimeOffset.UtcNow };
        var batch = new Batch { Id = 1, ProjectId = 1, Project = project, BatchNo = 1, Quantity = 10, Status = BatchStatus.New, Stage = ProductionStage.Design, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        db.ProductFormats.Add(pf);
        db.Orders.Add(order);
        db.Projects.Add(project);
        db.Batches.Add(batch);

        db.BatchAuditLog.Add(new BatchAuditLog
        {
            Id = 1,
            BatchId = 1,
            Batch = batch,
            ChangedAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            ChangedByUserId = "u1",
            OldStatus = BatchStatus.New,
            NewStatus = BatchStatus.InProgress
        });
        db.BatchAuditLog.Add(new BatchAuditLog
        {
            Id = 2,
            BatchId = 1,
            Batch = batch,
            ChangedAt = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero),
            ChangedByUserId = "u2",
            OldStage = ProductionStage.Design,
            NewStage = ProductionStage.Print
        });

        await db.SaveChangesAsync();
    }
}

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_should_return_Forbidden_for_non_manager()
    {
        await using var db = CreateDb();
        await SeedSplitRulesAndProductFormat(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(false);

        var sut = new OrderService(db, currentUser);

        var result = await sut.CreateAsync(new CreateOrderRequest(
            OrderNumber: "ORD-2026-0001",
            Quantity: 200,
            ProductFormatId: 1,
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10))
        ));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task CreateAsync_should_return_ValidationFailed_for_due_date_too_early()
    {
        await using var db = CreateDb();
        await SeedSplitRulesAndProductFormat(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new OrderService(db, currentUser);

        var result = await sut.CreateAsync(new CreateOrderRequest(
            OrderNumber: "ORD-2026-0001",
            Quantity: 200,
            ProductFormatId: 1,
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1))
        ));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
        result.Error!.FieldErrors!.Should().ContainKey("dueDate");
    }

    [Fact]
    public async Task CreateAsync_should_create_order_project_and_batches_using_split_rule()
    {
        await using var db = CreateDb();
        await SeedSplitRulesAndProductFormat(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new OrderService(db, currentUser);

        var result = await sut.CreateAsync(new CreateOrderRequest(
            OrderNumber: "ORD-2026-0001",
            Quantity: 200,
            ProductFormatId: 1,
            DueDate: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10))
        ));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Order.OrderNumber.Should().Be("ORD-2026-0001");
        result.Value!.Project.ProjectNumber.Should().Be("PRJ-2026-0001");
        result.Value!.Batches.Should().NotBeEmpty();
        result.Value!.Batches.Sum(b => b.Quantity).Should().Be(200);
        result.Value!.Batches.Should().OnlyContain(b => b.Status == BatchStatus.New && b.Stage == ProductionStage.Design);

        // split rule: Percent=30% => ceil(200*0.30)=60 => 60,60,60,20
        result.Value!.Batches.Select(b => b.Quantity).Should().Equal([60, 60, 60, 20]);
    }

    [Fact]
    public async Task GetAsync_should_filter_by_project_number_using_search_query()
    {
        await using var db = CreateDb();
        await SeedOrdersForListing(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new OrderService(db, currentUser);

        var result = await sut.GetAsync(new OrderQuery(Q: "PRJ-MATCH", Page: 1, PageSize: 50));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(x => x.OrderNumber == "ORD-MATCH");
    }

    [Fact]
    public async Task GetByIdAsync_should_return_details_with_product_format_and_project_summary()
    {
        await using var db = CreateDb();
        await SeedOrdersForListing(db);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new OrderService(db, currentUser);

        var result = await sut.GetByIdAsync(id: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderNumber.Should().Be("ORD-MATCH");
        result.Value!.ProductFormat.Name.Should().Be("A6");
        result.Value!.Project.ProjectNumber.Should().Be("PRJ-MATCH");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSplitRulesAndProductFormat(AppDbContext db)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.ProductFormats.Add(pf);

        db.BatchSplitRules.Add(new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = null,
            Percent = 30m,
            MinBatchSize = 1,
            MaxBatchesPerProject = null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedOrdersForListing(AppDbContext db)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.ProductFormats.Add(pf);

        var o1 = new Order
        {
            Id = 1,
            OrderNumber = "ORD-MATCH",
            Quantity = 10,
            ProductFormatId = 1,
            ProductFormat = pf,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(20)),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var p1 = new Project
        {
            Id = 1,
            OrderId = 1,
            Order = o1,
            ProjectNumber = "PRJ-MATCH",
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };

        var o2 = new Order
        {
            Id = 2,
            OrderNumber = "ORD-OTHER",
            Quantity = 10,
            ProductFormatId = 1,
            ProductFormat = pf,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(25)),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var p2 = new Project
        {
            Id = 2,
            OrderId = 2,
            Order = o2,
            ProjectNumber = "PRJ-OTHER",
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.Orders.AddRange(o1, o2);
        db.Projects.AddRange(p1, p2);

        await db.SaveChangesAsync();
    }
}

public sealed class ProductFormatServiceTests
{
    [Fact]
    public async Task GetActiveLookupAsync_should_return_only_active_for_authenticated_user()
    {
        await using var db = CreateDb();
        db.ProductFormats.Add(new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow });
        db.ProductFormats.Add(new ProductFormat { Id = 2, Name = "A5", IsActive = false, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole(Arg.Any<string>()).Returns(false);

        var sut = new ProductFormatService(db, currentUser);

        var result = await sut.GetActiveLookupAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(x => x.Id).Should().Equal([1]);
    }

    [Fact]
    public async Task GetAsync_should_return_Forbidden_for_non_manager()
    {
        await using var db = CreateDb();
        db.ProductFormats.Add(new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(false);

        var sut = new ProductFormatService(db, currentUser);

        var result = await sut.GetAsync(new ProductFormatQuery());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}

public sealed class BatchSplitRuleServiceTests
{
    [Fact]
    public async Task CreateAsync_should_fail_when_active_ranges_overlap()
    {
        await using var db = CreateDb();
        db.BatchSplitRules.Add(new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 30m,
            MinBatchSize = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new BatchSplitRuleService(db, currentUser);

        var result = await sut.CreateAsync(new UpsertBatchSplitRuleRequest(
            MinQty: 50,
            MaxQty: 200,
            Percent: 25m,
            MinBatchSize: 1,
            MaxBatchesPerProject: null,
            IsActive: true
        ));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    [Fact]
    public async Task SetActiveAsync_should_fail_when_activation_causes_overlap()
    {
        await using var db = CreateDb();
        db.BatchSplitRules.Add(new BatchSplitRule
        {
            Id = 1,
            MinQty = 1,
            MaxQty = 100,
            Percent = 30m,
            MinBatchSize = 1,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.BatchSplitRules.Add(new BatchSplitRule
        {
            Id = 2,
            MinQty = 50,
            MaxQty = 200,
            Percent = 25m,
            MinBatchSize = 1,
            IsActive = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new BatchSplitRuleService(db, currentUser);

        var result = await sut.SetActiveAsync(id: 2, isActive: true);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ValidationFailed");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetAsync_should_return_Forbidden_for_non_manager()
    {
        await using var db = CreateDb();
        await SeedDashboardData(db, inProgressBatches: 0);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(false);

        var sut = new DashboardService(db, currentUser);

        var result = await sut.GetAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Forbidden");
    }

    [Fact]
    public async Task GetAsync_should_return_counts_urgent_and_soft_limit_warning()
    {
        await using var db = CreateDb();
        await SeedDashboardData(db, inProgressBatches: 21);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns("u1");
        currentUser.IsInRole("Manager").Returns(true);

        var sut = new DashboardService(db, currentUser);

        var result = await sut.GetAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.CountsByStatus[BatchStatus.InProgress].Should().Be(21);
        result.Value!.CountsByStage[ProductionStage.Print].Should().Be(21);

        result.Value!.UrgentOrders.Should().ContainSingle(x => x.OrderNumber == "ORD-URGENT");
        result.Value!.Warnings.Should().ContainSingle(x => x.Code == "InProgressSoftLimitExceeded");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedDashboardData(AppDbContext db, int inProgressBatches)
    {
        var pf = new ProductFormat { Id = 1, Name = "A6", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.ProductFormats.Add(pf);

        var urgentDue = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3));
        var notUrgentDue = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30));

        var urgentOrder = new Order
        {
            Id = 1,
            OrderNumber = "ORD-URGENT",
            Quantity = 100,
            ProductFormatId = 1,
            ProductFormat = pf,
            DueDate = urgentDue,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var nonUrgentOrder = new Order
        {
            Id = 2,
            OrderNumber = "ORD-LATER",
            Quantity = 100,
            ProductFormatId = 1,
            ProductFormat = pf,
            DueDate = notUrgentDue,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var urgentProject = new Project
        {
            Id = 1,
            OrderId = 1,
            Order = urgentOrder,
            ProjectNumber = "PRJ-URGENT",
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var nonUrgentProject = new Project
        {
            Id = 2,
            OrderId = 2,
            Order = nonUrgentOrder,
            ProjectNumber = "PRJ-LATER",
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Orders.AddRange(urgentOrder, nonUrgentOrder);
        db.Projects.AddRange(urgentProject, nonUrgentProject);

        for (var i = 1; i <= inProgressBatches; i++)
        {
            db.Batches.Add(new Batch
            {
                Id = i,
                ProjectId = 1,
                Project = urgentProject,
                BatchNo = i,
                Quantity = 1,
                Status = BatchStatus.InProgress,
                Stage = ProductionStage.Print,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
