using DataAccess.Entities;
using DataAccess.Enums;
using DataAccess.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DataAccess;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProductFormat> ProductFormats => Set<ProductFormat>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchSplitRule> BatchSplitRules => Set<BatchSplitRule>();
    public DbSet<BatchAuditLog> BatchAuditLog => Set<BatchAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var batchStatusToString = new EnumToStringConverter<BatchStatus>();
        var nullableBatchStatusToString = new ValueConverter<BatchStatus?, string?>(
            v => v.HasValue ? v.Value.ToString() : null,
            v => v == null ? null : Enum.Parse<BatchStatus>(v));

        var stageToShort = new ValueConverter<ProductionStage, short>(
            v => (short)v,
            v => (ProductionStage)v);
        var nullableStageToShort = new ValueConverter<ProductionStage?, short?>(
            v => v.HasValue ? (short)v.Value : null,
            v => v.HasValue ? (ProductionStage)v.Value : null);

        modelBuilder.Entity<ProductFormat>(b =>
        {
            b.ToTable("product_formats");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.Name).HasColumnName("name").HasColumnType("text").IsRequired();
            b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

            b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_product_formats_name");
        });

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.OrderNumber).HasColumnName("order_number").HasColumnType("text").IsRequired();
            b.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
            b.Property(x => x.ProductFormatId).HasColumnName("product_format_id").IsRequired();
            b.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

            b.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("uq_orders_order_number");
            b.HasIndex(x => x.DueDate).HasDatabaseName("ix_orders_due_date");
            b.HasIndex(x => x.OrderNumber).HasDatabaseName("ix_orders_order_number_pattern");

            b.HasCheckConstraint("ck_orders_quantity_range", "\"quantity\" BETWEEN 1 AND 100000");

            b.HasOne(x => x.ProductFormat)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.ProductFormatId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
            b.Property(x => x.ProjectNumber).HasColumnName("project_number").HasColumnType("text").IsRequired();
            b.Property(x => x.IsCompleted).HasColumnName("is_completed").HasDefaultValue(false).IsRequired();
            b.Property(x => x.CompletedAt).HasColumnName("completed_at");
            b.Property(x => x.CompletedByUserId).HasColumnName("completed_by_user_id").HasColumnType("text");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

            b.HasIndex(x => x.ProjectNumber).IsUnique().HasDatabaseName("uq_projects_project_number");
            b.HasIndex(x => x.IsCompleted).HasDatabaseName("ix_projects_is_completed");
            b.HasIndex(x => x.Id).HasDatabaseName("ix_projects_active").HasFilter("\"is_completed\" = FALSE");

            b.HasCheckConstraint(
                "ck_projects_completed_consistency",
                "(NOT \"is_completed\" AND \"completed_at\" IS NULL) OR (\"is_completed\" AND \"completed_at\" IS NOT NULL)");

            b.HasOne(x => x.Order)
                .WithMany(x => x.Projects)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CompletedByUser)
                .WithMany()
                .HasForeignKey(x => x.CompletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Batch>(b =>
        {
            b.ToTable("batches");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.ProjectId).HasColumnName("project_id").IsRequired();
            b.Property(x => x.BatchNo).HasColumnName("batch_no").IsRequired();
            b.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();

            b.Property(x => x.Status)
                .HasColumnName("status")
                .HasColumnType("text")
                .HasConversion(batchStatusToString)
                .IsRequired();

            b.Property(x => x.Stage)
                .HasColumnName("stage")
                .HasColumnType("smallint")
                .HasConversion(stageToShort)
                .IsRequired();

            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

            b.HasIndex(x => new { x.ProjectId, x.BatchNo }).IsUnique().HasDatabaseName("uq_batches_project_batch_no");
            b.HasIndex(x => x.ProjectId).HasDatabaseName("ix_batches_project_id");
            b.HasIndex(x => new { x.Status, x.Stage, x.ProjectId }).HasDatabaseName("ix_batches_kanban_filter").IncludeProperties(new[] { nameof(Batch.UpdatedAt) });
            b.HasIndex(x => x.UpdatedAt).HasDatabaseName("ix_batches_updated_at");
            b.HasIndex(x => x.Id).HasDatabaseName("ix_batches_inprogress").HasFilter("\"status\" = 'InProgress'");

            b.HasCheckConstraint("ck_batches_quantity_positive", "\"quantity\" > 0");
            b.HasCheckConstraint("ck_batches_status_enum", "\"status\" IN ('New','InProgress','Done')");
            b.HasCheckConstraint("ck_batches_stage_range", "\"stage\" BETWEEN 1 AND 5");

            b.HasOne(x => x.Project)
                .WithMany(x => x.Batches)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BatchSplitRule>(b =>
        {
            b.ToTable("batch_split_rules");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.MinQty).HasColumnName("min_qty").IsRequired();
            b.Property(x => x.MaxQty).HasColumnName("max_qty");
            b.Property(x => x.Percent).HasColumnName("percent").HasColumnType("numeric(5,2)").IsRequired();
            b.Property(x => x.MinBatchSize).HasColumnName("min_batch_size").HasDefaultValue(1).IsRequired();
            b.Property(x => x.MaxBatchesPerProject).HasColumnName("max_batches_per_project");
            b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

            b.HasCheckConstraint("ck_batch_split_rules_min_qty", "\"min_qty\" >= 1");
            b.HasCheckConstraint("ck_batch_split_rules_max_qty", "\"max_qty\" IS NULL OR \"max_qty\" >= \"min_qty\"");
            b.HasCheckConstraint("ck_batch_split_rules_percent", "\"percent\" > 0 AND \"percent\" <= 100");
            b.HasCheckConstraint("ck_batch_split_rules_min_batch_size", "\"min_batch_size\" >= 1");
            b.HasCheckConstraint("ck_batch_split_rules_max_batches", "\"max_batches_per_project\" IS NULL OR \"max_batches_per_project\" >= 1");
        });

        modelBuilder.Entity<BatchAuditLog>(b =>
        {
            b.ToTable("batch_audit_log");

            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();

            b.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
            b.Property(x => x.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("now()").IsRequired();

            b.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id").HasColumnType("text").IsRequired();

            b.Property(x => x.OldStatus)
                .HasColumnName("old_status")
                .HasColumnType("text")
                .HasConversion(nullableBatchStatusToString);

            b.Property(x => x.NewStatus)
                .HasColumnName("new_status")
                .HasColumnType("text")
                .HasConversion(nullableBatchStatusToString);

            b.Property(x => x.OldStage)
                .HasColumnName("old_stage")
                .HasColumnType("smallint")
                .HasConversion(nullableStageToShort);

            b.Property(x => x.NewStage)
                .HasColumnName("new_stage")
                .HasColumnType("smallint")
                .HasConversion(nullableStageToShort);

            b.HasIndex(x => new { x.BatchId, x.ChangedAt }).HasDatabaseName("ix_batch_audit_batch_changed_at");

            b.HasCheckConstraint("ck_batch_audit_status_enum_old", "\"old_status\" IS NULL OR \"old_status\" IN ('New','InProgress','Done')");
            b.HasCheckConstraint("ck_batch_audit_status_enum_new", "\"new_status\" IS NULL OR \"new_status\" IN ('New','InProgress','Done')");
            b.HasCheckConstraint("ck_batch_audit_stage_range_old", "\"old_stage\" IS NULL OR (\"old_stage\" BETWEEN 1 AND 5)");
            b.HasCheckConstraint("ck_batch_audit_stage_range_new", "\"new_stage\" IS NULL OR (\"new_stage\" BETWEEN 1 AND 5)");
            b.HasCheckConstraint(
                "ck_batch_audit_has_change",
                "(\"old_status\" IS DISTINCT FROM \"new_status\") OR (\"old_stage\" IS DISTINCT FROM \"new_stage\")");

            b.HasOne(x => x.Batch)
                .WithMany(x => x.AuditLog)
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ChangedByUser)
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

