using KasserPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasserPro.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260508152000_DisableTaxInclusivePricing")]
public partial class DisableTaxInclusivePricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE Products
            SET TaxInclusive = 0
            WHERE TaxInclusive <> 0;
            """);

        migrationBuilder.Sql("""
            UPDATE Branches
            SET DefaultTaxInclusive = 0
            WHERE DefaultTaxInclusive <> 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE Products
            SET TaxInclusive = 1
            WHERE TaxInclusive <> 1;
            """);

        migrationBuilder.Sql("""
            UPDATE Branches
            SET DefaultTaxInclusive = 1
            WHERE DefaultTaxInclusive <> 1;
            """);
    }
}
