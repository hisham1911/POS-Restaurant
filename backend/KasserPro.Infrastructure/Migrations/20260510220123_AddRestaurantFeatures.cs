using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderNumber",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KitchenPrintCount",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastKitchenPrintedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderSource",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableNumberSnapshot",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KitchenPrintedQuantity",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastKitchenPrintedAt",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentOrderItemId",
                table: "OrderItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RestaurantTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantTables_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantTables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedOrderNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedOrderNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedOrderNotes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedOrderNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "BranchId", "OrderSource", "CreatedAt" });

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

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ParentOrderItemId",
                table: "OrderItems",
                column: "ParentOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_BranchId",
                table: "RestaurantTables",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_TenantId_BranchId_Number",
                table: "RestaurantTables",
                columns: new[] { "TenantId", "BranchId", "Number" },
                unique: true,
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_TenantId_BranchId_Status_IsActive",
                table: "RestaurantTables",
                columns: new[] { "TenantId", "BranchId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedOrderNotes_BranchId",
                table: "SavedOrderNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedOrderNotes_TenantId_BranchId_IsActive_SortOrder",
                table: "SavedOrderNotes",
                columns: new[] { "TenantId", "BranchId", "IsActive", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_OrderItems_ParentOrderItemId",
                table: "OrderItems",
                column: "ParentOrderItemId",
                principalTable: "OrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_RestaurantTables_TableId",
                table: "Orders",
                column: "TableId",
                principalTable: "RestaurantTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_OrderItems_ParentOrderItemId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_RestaurantTables_TableId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "RestaurantTables");

            migrationBuilder.DropTable(
                name: "SavedOrderNotes");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TableId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_OrderSource_ExternalOrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_BranchId_TableId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ParentOrderItemId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ExternalOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "KitchenPrintCount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastKitchenPrintedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderSource",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TableNumberSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "KitchenPrintedQuantity",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "LastKitchenPrintedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ParentOrderItemId",
                table: "OrderItems");
        }
    }
}
