using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixBatchAndDeliveryStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DeliveryStatus",
                table: "Orders",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_TenantId_BranchId_ProductId_BatchNumber",
                table: "ProductBatches",
                columns: new[] { "TenantId", "BranchId", "ProductId", "BatchNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_TenantId_BranchId_ProductId_BatchNumber",
                table: "ProductBatches");

            migrationBuilder.AlterColumn<int>(
                name: "DeliveryStatus",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
