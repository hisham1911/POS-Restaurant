using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentMethodSchemaFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support RenameColumn; add new column and copy data instead.
            migrationBuilder.Sql(@"ALTER TABLE Shifts ADD COLUMN TotalBankAccount decimal(18,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql(@"UPDATE Shifts SET TotalBankAccount = TotalCard;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse data copy for rollback (cannot drop columns in SQLite easily).
            migrationBuilder.Sql(@"UPDATE Shifts SET TotalCard = TotalBankAccount;");
        }
    }
}
