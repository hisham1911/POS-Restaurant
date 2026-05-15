using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using KasserPro.Infrastructure.Data;

#nullable disable

namespace KasserPro.Infrastructure.Migrations
{
    /// <summary>
    /// Repairs legacy SQLite Shift schemas that still require TotalCard and carry the stray UserId1 FK.
    /// SQLite cannot drop columns, so we rebuild the table and migrate data to the current model.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260508133000_RebuildShiftsTableForBankAccountTotals")]
    public partial class RebuildShiftsTableForBankAccountTotals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
PRAGMA foreign_keys=OFF;

CREATE TABLE ""Shifts__new"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Shifts"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""BranchId"" INTEGER NOT NULL,
    ""OpeningBalance"" decimal(18,2) NOT NULL,
    ""ClosingBalance"" decimal(18,2) NOT NULL,
    ""ExpectedBalance"" decimal(18,2) NOT NULL,
    ""Difference"" decimal(18,2) NOT NULL,
    ""OpenedAt"" TEXT NOT NULL,
    ""ClosedAt"" TEXT NULL,
    ""IsClosed"" INTEGER NOT NULL,
    ""Notes"" TEXT NULL,
    ""TotalCash"" decimal(18,2) NOT NULL,
    ""TotalBankAccount"" decimal(18,2) NOT NULL DEFAULT 0,
    ""TotalOrders"" INTEGER NOT NULL,
    ""UserId"" INTEGER NOT NULL,
    ""RowVersion"" BLOB NOT NULL,
    ""IsReconciled"" INTEGER NOT NULL,
    ""ReconciledByUserId"" INTEGER NULL,
    ""ReconciledByUserName"" TEXT NULL,
    ""ReconciledAt"" TEXT NULL,
    ""VarianceReason"" TEXT NULL,
    ""LastActivityAt"" TEXT NOT NULL,
    ""IsForceClosed"" INTEGER NOT NULL,
    ""ForceClosedByUserId"" INTEGER NULL,
    ""ForceClosedByUserName"" TEXT NULL,
    ""ForceClosedAt"" TEXT NULL,
    ""ForceCloseReason"" TEXT NULL,
    ""IsHandedOver"" INTEGER NOT NULL,
    ""HandedOverFromUserId"" INTEGER NULL,
    ""HandedOverFromUserName"" TEXT NULL,
    ""HandedOverToUserId"" INTEGER NULL,
    ""HandedOverToUserName"" TEXT NULL,
    ""HandedOverAt"" TEXT NULL,
    ""HandoverBalance"" decimal(18,2) NOT NULL,
    ""HandoverNotes"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NULL,
    ""IsDeleted"" INTEGER NOT NULL,
    CONSTRAINT ""FK_Shifts_Branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES ""Branches"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Tenants_TenantId"" FOREIGN KEY (""TenantId"") REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_ForceClosedByUserId"" FOREIGN KEY (""ForceClosedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_HandedOverFromUserId"" FOREIGN KEY (""HandedOverFromUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_HandedOverToUserId"" FOREIGN KEY (""HandedOverToUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_ReconciledByUserId"" FOREIGN KEY (""ReconciledByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);

INSERT INTO ""Shifts__new"" (
    ""Id"", ""TenantId"", ""BranchId"", ""OpeningBalance"", ""ClosingBalance"", ""ExpectedBalance"", ""Difference"",
    ""OpenedAt"", ""ClosedAt"", ""IsClosed"", ""Notes"", ""TotalCash"", ""TotalBankAccount"", ""TotalOrders"",
    ""UserId"", ""RowVersion"", ""IsReconciled"", ""ReconciledByUserId"", ""ReconciledByUserName"", ""ReconciledAt"",
    ""VarianceReason"", ""LastActivityAt"", ""IsForceClosed"", ""ForceClosedByUserId"", ""ForceClosedByUserName"",
    ""ForceClosedAt"", ""ForceCloseReason"", ""IsHandedOver"", ""HandedOverFromUserId"", ""HandedOverFromUserName"",
    ""HandedOverToUserId"", ""HandedOverToUserName"", ""HandedOverAt"", ""HandoverBalance"", ""HandoverNotes"",
    ""CreatedAt"", ""UpdatedAt"", ""IsDeleted""
)
SELECT
    ""Id"", ""TenantId"", ""BranchId"", ""OpeningBalance"", ""ClosingBalance"", ""ExpectedBalance"", ""Difference"",
    ""OpenedAt"", ""ClosedAt"", ""IsClosed"", ""Notes"", ""TotalCash"",
    COALESCE(""TotalBankAccount"", ""TotalCard"", 0),
    ""TotalOrders"", ""UserId"", ""RowVersion"", ""IsReconciled"", ""ReconciledByUserId"", ""ReconciledByUserName"",
    ""ReconciledAt"", ""VarianceReason"", ""LastActivityAt"", ""IsForceClosed"", ""ForceClosedByUserId"",
    ""ForceClosedByUserName"", ""ForceClosedAt"", ""ForceCloseReason"", ""IsHandedOver"", ""HandedOverFromUserId"",
    ""HandedOverFromUserName"", ""HandedOverToUserId"", ""HandedOverToUserName"", ""HandedOverAt"", ""HandoverBalance"",
    ""HandoverNotes"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted""
FROM ""Shifts"";

DROP TABLE ""Shifts"";
ALTER TABLE ""Shifts__new"" RENAME TO ""Shifts"";

CREATE INDEX ""IX_Shifts_BranchId"" ON ""Shifts"" (""BranchId"");
CREATE INDEX ""IX_Shifts_ForceClosedByUserId"" ON ""Shifts"" (""ForceClosedByUserId"");
CREATE INDEX ""IX_Shifts_HandedOverFromUserId"" ON ""Shifts"" (""HandedOverFromUserId"");
CREATE INDEX ""IX_Shifts_HandedOverToUserId"" ON ""Shifts"" (""HandedOverToUserId"");
CREATE INDEX ""IX_Shifts_IsClosed"" ON ""Shifts"" (""IsClosed"");
CREATE INDEX ""IX_Shifts_OpenedAt"" ON ""Shifts"" (""OpenedAt"");
CREATE INDEX ""IX_Shifts_ReconciledByUserId"" ON ""Shifts"" (""ReconciledByUserId"");
CREATE INDEX ""IX_Shifts_TenantId_BranchId"" ON ""Shifts"" (""TenantId"", ""BranchId"");
CREATE INDEX ""IX_Shifts_UserId"" ON ""Shifts"" (""UserId"");
CREATE INDEX ""IX_Shifts_UserId_IsClosed_OpenedAt"" ON ""Shifts"" (""UserId"", ""IsClosed"", ""OpenedAt"") WHERE IsDeleted = 0;

PRAGMA foreign_keys=ON;
",
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
PRAGMA foreign_keys=OFF;

CREATE TABLE ""Shifts__old"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Shifts"" PRIMARY KEY AUTOINCREMENT,
    ""TenantId"" INTEGER NOT NULL,
    ""BranchId"" INTEGER NOT NULL,
    ""OpeningBalance"" decimal(18,2) NOT NULL,
    ""ClosingBalance"" decimal(18,2) NOT NULL,
    ""ExpectedBalance"" decimal(18,2) NOT NULL,
    ""Difference"" decimal(18,2) NOT NULL,
    ""OpenedAt"" TEXT NOT NULL,
    ""ClosedAt"" TEXT NULL,
    ""IsClosed"" INTEGER NOT NULL,
    ""Notes"" TEXT NULL,
    ""TotalCash"" decimal(18,2) NOT NULL,
    ""TotalCard"" decimal(18,2) NOT NULL,
    ""TotalOrders"" INTEGER NOT NULL,
    ""UserId"" INTEGER NOT NULL,
    ""RowVersion"" BLOB NOT NULL,
    ""IsReconciled"" INTEGER NOT NULL,
    ""ReconciledByUserId"" INTEGER NULL,
    ""ReconciledByUserName"" TEXT NULL,
    ""ReconciledAt"" TEXT NULL,
    ""VarianceReason"" TEXT NULL,
    ""LastActivityAt"" TEXT NOT NULL,
    ""IsForceClosed"" INTEGER NOT NULL,
    ""ForceClosedByUserId"" INTEGER NULL,
    ""ForceClosedByUserName"" TEXT NULL,
    ""ForceClosedAt"" TEXT NULL,
    ""ForceCloseReason"" TEXT NULL,
    ""IsHandedOver"" INTEGER NOT NULL,
    ""HandedOverFromUserId"" INTEGER NULL,
    ""HandedOverFromUserName"" TEXT NULL,
    ""HandedOverToUserId"" INTEGER NULL,
    ""HandedOverToUserName"" TEXT NULL,
    ""HandedOverAt"" TEXT NULL,
    ""HandoverBalance"" decimal(18,2) NOT NULL,
    ""HandoverNotes"" TEXT NULL,
    ""UserId1"" INTEGER NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NULL,
    ""IsDeleted"" INTEGER NOT NULL,
    ""TotalBankAccount"" decimal(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT ""FK_Shifts_Branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES ""Branches"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Tenants_TenantId"" FOREIGN KEY (""TenantId"") REFERENCES ""Tenants"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_ForceClosedByUserId"" FOREIGN KEY (""ForceClosedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_HandedOverFromUserId"" FOREIGN KEY (""HandedOverFromUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_HandedOverToUserId"" FOREIGN KEY (""HandedOverToUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_ReconciledByUserId"" FOREIGN KEY (""ReconciledByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_Shifts_Users_UserId1"" FOREIGN KEY (""UserId1"") REFERENCES ""Users"" (""Id"")
);

INSERT INTO ""Shifts__old"" (
    ""Id"", ""TenantId"", ""BranchId"", ""OpeningBalance"", ""ClosingBalance"", ""ExpectedBalance"", ""Difference"",
    ""OpenedAt"", ""ClosedAt"", ""IsClosed"", ""Notes"", ""TotalCash"", ""TotalCard"", ""TotalOrders"", ""UserId"",
    ""RowVersion"", ""IsReconciled"", ""ReconciledByUserId"", ""ReconciledByUserName"", ""ReconciledAt"",
    ""VarianceReason"", ""LastActivityAt"", ""IsForceClosed"", ""ForceClosedByUserId"", ""ForceClosedByUserName"",
    ""ForceClosedAt"", ""ForceCloseReason"", ""IsHandedOver"", ""HandedOverFromUserId"", ""HandedOverFromUserName"",
    ""HandedOverToUserId"", ""HandedOverToUserName"", ""HandedOverAt"", ""HandoverBalance"", ""HandoverNotes"",
    ""UserId1"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"", ""TotalBankAccount""
)
SELECT
    ""Id"", ""TenantId"", ""BranchId"", ""OpeningBalance"", ""ClosingBalance"", ""ExpectedBalance"", ""Difference"",
    ""OpenedAt"", ""ClosedAt"", ""IsClosed"", ""Notes"", ""TotalCash"", ""TotalBankAccount"", ""TotalOrders"", ""UserId"",
    ""RowVersion"", ""IsReconciled"", ""ReconciledByUserId"", ""ReconciledByUserName"", ""ReconciledAt"",
    ""VarianceReason"", ""LastActivityAt"", ""IsForceClosed"", ""ForceClosedByUserId"", ""ForceClosedByUserName"",
    ""ForceClosedAt"", ""ForceCloseReason"", ""IsHandedOver"", ""HandedOverFromUserId"", ""HandedOverFromUserName"",
    ""HandedOverToUserId"", ""HandedOverToUserName"", ""HandedOverAt"", ""HandoverBalance"", ""HandoverNotes"",
    NULL, ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"", ""TotalBankAccount""
FROM ""Shifts"";

DROP TABLE ""Shifts"";
ALTER TABLE ""Shifts__old"" RENAME TO ""Shifts"";

CREATE INDEX ""IX_Shifts_BranchId"" ON ""Shifts"" (""BranchId"");
CREATE INDEX ""IX_Shifts_ForceClosedByUserId"" ON ""Shifts"" (""ForceClosedByUserId"");
CREATE INDEX ""IX_Shifts_HandedOverFromUserId"" ON ""Shifts"" (""HandedOverFromUserId"");
CREATE INDEX ""IX_Shifts_HandedOverToUserId"" ON ""Shifts"" (""HandedOverToUserId"");
CREATE INDEX ""IX_Shifts_IsClosed"" ON ""Shifts"" (""IsClosed"");
CREATE INDEX ""IX_Shifts_OpenedAt"" ON ""Shifts"" (""OpenedAt"");
CREATE INDEX ""IX_Shifts_ReconciledByUserId"" ON ""Shifts"" (""ReconciledByUserId"");
CREATE INDEX ""IX_Shifts_TenantId_BranchId"" ON ""Shifts"" (""TenantId"", ""BranchId"");
CREATE INDEX ""IX_Shifts_UserId"" ON ""Shifts"" (""UserId"");
CREATE INDEX ""IX_Shifts_UserId_IsClosed_OpenedAt"" ON ""Shifts"" (""UserId"", ""IsClosed"", ""OpenedAt"") WHERE IsDeleted = 0;
CREATE INDEX ""IX_Shifts_UserId1"" ON ""Shifts"" (""UserId1"");

PRAGMA foreign_keys=ON;
",
                suppressTransaction: true);
        }
    }
}
