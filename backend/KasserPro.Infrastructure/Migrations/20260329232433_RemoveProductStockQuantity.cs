using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductStockQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Method",
                table: "Payments",
                columns: new[] { "OrderId", "Method" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_Branch_Status_CompletedAt",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "Status", "CompletedAt" },
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Method",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_Branch_Status_CompletedAt",
                table: "Orders");

            migrationBuilder.AddColumn<int>(
                name: "StockQuantity",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");
        }
    }
}
