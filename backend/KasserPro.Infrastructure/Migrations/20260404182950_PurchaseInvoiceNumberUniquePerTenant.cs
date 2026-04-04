using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseInvoiceNumberUniquePerTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_TenantId_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_TenantId_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true);
        }
    }
}
