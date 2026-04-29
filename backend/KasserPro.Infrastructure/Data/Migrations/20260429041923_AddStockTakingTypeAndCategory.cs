using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTakingTypeAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "StockTakings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "StockTakings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StockTakings_CategoryId",
                table: "StockTakings",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakings_Categories_CategoryId",
                table: "StockTakings",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTakings_Categories_CategoryId",
                table: "StockTakings");

            migrationBuilder.DropIndex(
                name: "IX_StockTakings_CategoryId",
                table: "StockTakings");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "StockTakings");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "StockTakings");

        }
    }
}
