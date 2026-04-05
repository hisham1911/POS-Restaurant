using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPrintRoutingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintDailyReports",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintOnDebtPayment",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintOnSale",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintRoutingMode",
                table: "Tenants",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "BranchWithFallback");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPrintDailyReports",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutoPrintOnDebtPayment",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AutoPrintOnSale",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PrintRoutingMode",
                table: "Tenants");
        }
    }
}
