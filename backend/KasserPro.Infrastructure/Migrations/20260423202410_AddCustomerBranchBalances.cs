using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBranchBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerBranchBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountDue = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBranchBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerBranchBalances_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranchBalances_CustomerId_BranchId_TenantId",
                table: "CustomerBranchBalances",
                columns: new[] { "CustomerId", "BranchId", "TenantId" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO CustomerBranchBalances (CustomerId, BranchId, TenantId, AmountDue, CreatedAt, IsDeleted)
                SELECT
                    o.CustomerId,
                    o.BranchId,
                    o.TenantId,
                    ROUND(SUM(o.AmountDue), 2),
                    CURRENT_TIMESTAMP,
                    0
                FROM Orders o
                WHERE o.IsDeleted = 0
                  AND o.CustomerId IS NOT NULL
                  AND o.AmountDue > 0
                  AND o.OrderType <> 3
                  AND o.Status IN (2, 4, 5)
                GROUP BY o.CustomerId, o.BranchId, o.TenantId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBranchBalances");
        }
    }
}
