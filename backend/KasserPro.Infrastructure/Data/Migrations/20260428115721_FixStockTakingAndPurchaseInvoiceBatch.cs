using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixStockTakingAndPurchaseInvoiceBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockTakingItems_StockTakingId_ProductId",
                table: "StockTakingItems");

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "PurchaseInvoiceItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakingItems_StockTakingId_ProductId_BatchId",
                table: "StockTakingItems",
                columns: new[] { "StockTakingId", "ProductId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceItems_BatchId",
                table: "PurchaseInvoiceItems",
                column: "BatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceItems_ProductBatches_BatchId",
                table: "PurchaseInvoiceItems",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceItems_ProductBatches_BatchId",
                table: "PurchaseInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_StockTakingItems_StockTakingId_ProductId_BatchId",
                table: "StockTakingItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceItems_BatchId",
                table: "PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PurchaseInvoiceItems");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakingItems_StockTakingId_ProductId",
                table: "StockTakingItems",
                columns: new[] { "StockTakingId", "ProductId" },
                unique: true);
        }
    }
}
