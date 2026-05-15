using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRestaurantOrderStatusIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_ExternalOrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_TableId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_ExternalOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "OrderSource", "ExternalOrderNumber" },
                unique: true,
                filter: "ExternalOrderNumber IS NOT NULL AND Status <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_TableId",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "TableId" },
                unique: true,
                filter: "TableId IS NOT NULL AND OrderType = 0 AND Status IN (0,1,6,7,8) AND IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_ExternalOrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_TableId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_ExternalOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "OrderSource", "ExternalOrderNumber" },
                unique: true,
                filter: "ExternalOrderNumber IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_TableId",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "TableId" },
                unique: true,
                filter: "TableId IS NOT NULL AND OrderType = 0 AND Status IN (0,1) AND IsDeleted = 0");
        }
    }
}
