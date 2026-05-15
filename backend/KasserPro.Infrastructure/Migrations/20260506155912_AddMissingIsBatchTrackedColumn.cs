using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIsBatchTrackedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBatchTracked",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBatchTracked",
                table: "Products");
        }
    }
}
