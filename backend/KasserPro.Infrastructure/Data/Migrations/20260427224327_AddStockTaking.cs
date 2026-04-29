using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockTakings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    StockTakingNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTakings_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakings_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTakingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockTakingId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTakingItems_ProductBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockTakingItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTakingItems_StockTakings_StockTakingId",
                        column: x => x.StockTakingId,
                        principalTable: "StockTakings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTakingItems_BatchId",
                table: "StockTakingItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakingItems_ProductId",
                table: "StockTakingItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakingItems_StockTakingId_ProductId",
                table: "StockTakingItems",
                columns: new[] { "StockTakingId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_BranchId",
                table: "StockTakings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_CompletedByUserId",
                table: "StockTakings",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_CreatedByUserId",
                table: "StockTakings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_StockTakingNumber",
                table: "StockTakings",
                column: "StockTakingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_TenantId_BranchId_Status",
                table: "StockTakings",
                columns: new[] { "TenantId", "BranchId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTakingItems");

            migrationBuilder.DropTable(
                name: "StockTakings");
        }
    }
}
