using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKanbanCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "uq_orders_order_number",
                table: "orders",
                newName: "ix_orders_order_number_pattern");

            migrationBuilder.CreateIndex(
                name: "ix_projects_is_completed",
                table: "projects",
                column: "is_completed");

            migrationBuilder.CreateIndex(
                name: "ix_batches_kanban_filter",
                table: "batches",
                columns: new[] { "status", "stage", "project_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_batches_updated_at",
                table: "batches",
                column: "updated_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_projects_is_completed",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_batches_kanban_filter",
                table: "batches");

            migrationBuilder.DropIndex(
                name: "ix_batches_updated_at",
                table: "batches");

            migrationBuilder.RenameIndex(
                name: "ix_orders_order_number_pattern",
                table: "orders",
                newName: "uq_orders_order_number");
        }
    }
}
