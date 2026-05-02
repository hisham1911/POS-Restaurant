using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchSellingPriceAndOnHoldStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SellingPrice column to ProductBatches table
            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                table: "ProductBatches",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove SellingPrice column from ProductBatches table
            migrationBuilder.DropColumn(
                name: "SellingPrice",
                table: "ProductBatches");
        }
    }
}
