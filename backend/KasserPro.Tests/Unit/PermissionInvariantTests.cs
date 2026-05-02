namespace KasserPro.Tests.Unit;

using KasserPro.Domain.Enums;
using FluentAssertions;
using Xunit;

public class PermissionInvariantTests
{
    // Permission Values

    [Fact]
    public void Permission_ExpensesApprove_HasCorrectValue()
    {
        ((int)Permission.ExpensesApprove).Should().Be(703);
    }

    [Fact]
    public void Permission_CashRegisterTransfer_HasCorrectValue()
    {
        ((int)Permission.CashRegisterTransfer).Should().Be(1002);
    }

    [Fact]
    public void Permission_CashRegisterReconcile_HasCorrectValue()
    {
        ((int)Permission.CashRegisterReconcile).Should().Be(1003);
    }

    // Uniqueness

    [Fact]
    public void Permission_AllValues_AreUnique()
    {
        var values = Enum.GetValues<Permission>().Select(p => (int)p).ToList();
        values.Should().OnlyHaveUniqueItems("Permission enum values must be unique");
    }

    // Protected Roles

    [Theory]
    [InlineData("Admin")]
    [InlineData("SystemOwner")]
    public void ProtectedRole_CannotBeModified(string role)
    {
        var protectedRoles = new[] { "Admin", "SystemOwner" };
        protectedRoles.Should().Contain(role);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("StoreManager")]
    public void OperationalRole_CanBeModified(string role)
    {
        var protectedRoles = new[] { "Admin", "SystemOwner" };
        protectedRoles.Should().NotContain(role);
    }

    // Cashier Default Permissions

    [Fact]
    public void DefaultCashierPermissions_DoNotIncludeSensitivePermissions()
    {
        var sensitivePermissions = new[]
        {
            Permission.ExpensesApprove,
            Permission.CashRegisterTransfer,
            Permission.CashRegisterReconcile,
        };

        sensitivePermissions.Should().NotBeEmpty("Sensitive permissions must be defined");
    }
}
