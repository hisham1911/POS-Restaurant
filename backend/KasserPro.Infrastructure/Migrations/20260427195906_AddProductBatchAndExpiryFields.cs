using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBatchAndExpiryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowExpiredSales",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryAlertDays",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "StockMovements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "PurchaseInvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "PurchaseInvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionDate",
                table: "PurchaseInvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "OrderItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PurchaseInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CostPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    SupplierName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StatusUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductBatches_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_BatchId",
                table: "OrderItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BranchId_ProductId_Status_ExpiryDate",
                table: "ProductBatches",
                columns: new[] { "BranchId", "ProductId", "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_PurchaseInvoiceId",
                table: "ProductBatches",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_TenantId_BranchId_ProductId_ExpiryDate",
                table: "ProductBatches",
                columns: new[] { "TenantId", "BranchId", "ProductId", "ExpiryDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductBatches_BatchId",
                table: "OrderItems",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ProductBatches_BatchId",
                table: "StockMovements",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductBatches_BatchId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ProductBatches_BatchId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_BatchId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "AllowExpiredSales",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ExpiryAlertDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProductionDate",
                table: "PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "OrderItems");
        }
    }
}
