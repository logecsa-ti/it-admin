namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Application.Common;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using Xunit;

public class SoftwareLicenseTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public void Install_ShouldOccupySeatsUntilExhausted()
    {
        var license = NewLicense(quantity: 2);

        license.Install(assetId: 1, userId: null, Now, Today);
        license.Install(assetId: 2, userId: 7, Now, Today);
        var third = () => license.Install(3, null, Now, Today);

        license.UsedQuantity.Should().Be(2);
        license.AvailableQuantity.Should().Be(0);
        third.Should().Throw<ConflictException>().Which.Code.Should().Be("LICENSE_EXHAUSTED");
        license.UsedQuantity.Should().Be(2, "un intento fallido no consume puestos");
    }

    [Fact]
    public void Install_ExpiredOrInactive_ShouldThrow()
    {
        var expired = NewLicense(quantity: 5);
        expired.ExpirationDate = Today.AddDays(-1);
        var inactive = NewLicense(quantity: 5);
        inactive.IsActive = false;

        expired.Invoking(l => l.Install(1, null, Now, Today))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("LICENSE_EXPIRED");
        inactive.Invoking(l => l.Install(1, null, Now, Today))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("LICENSE_INACTIVE");
    }

    [Fact]
    public void Install_OnExpirationDay_ShouldStillBeAllowed()
    {
        var license = NewLicense(quantity: 1);
        license.ExpirationDate = Today;

        license.Invoking(l => l.Install(1, null, Now, Today)).Should().NotThrow();
    }

    [Fact]
    public void Uninstall_ShouldFreeSeatAndCloseInstallation()
    {
        var license = NewLicense(quantity: 1);
        var installation = license.Install(1, null, Now, Today);

        license.Uninstall(installation, Now.AddDays(10));

        license.UsedQuantity.Should().Be(0);
        installation.IsActive.Should().BeFalse();
        installation.UninstalledAt.Should().Be(Now.AddDays(10));
        license.Invoking(l => l.Uninstall(installation, Now))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("INSTALLATION_NOT_ACTIVE");
    }

    [Fact]
    public void SetQuantity_BelowUsage_ShouldThrowConflict()
    {
        var license = NewLicense(quantity: 3);
        license.Install(1, null, Now, Today);
        license.Install(2, null, Now, Today);

        license.Invoking(l => l.SetQuantity(1))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("LICENSE_QUANTITY_BELOW_USAGE");
        license.Invoking(l => l.SetQuantity(-1))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("INVALID_LICENSE_QUANTITY");
        license.Invoking(l => l.SetQuantity(2)).Should().NotThrow();
    }

    [Fact]
    public void EnsureCanBeDeleted_WithActiveInstallations_ShouldThrow()
    {
        var license = NewLicense(quantity: 1);
        license.Install(1, null, Now, Today);

        license.Invoking(l => l.EnsureCanBeDeleted())
            .Should().Throw<ConflictException>().Which.Code.Should().Be("LICENSE_IN_USE");
    }

    private static SoftwareLicense NewLicense(int quantity)
    {
        var license = new SoftwareLicense { Name = "Office 365", SoftwareId = 1, LicenseType = LicenseType.Subscription };
        license.SetQuantity(quantity);
        return license;
    }
}

public class ContractTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Theory]
    [InlineData(ContractStatus.Draft, 100, ContractStatus.Draft)]
    [InlineData(ContractStatus.Terminated, -5, ContractStatus.Terminated)]
    [InlineData(ContractStatus.Active, 100, ContractStatus.Active)]
    [InlineData(ContractStatus.Active, 30, ContractStatus.Expiring)]
    [InlineData(ContractStatus.Active, 0, ContractStatus.Expiring)]
    [InlineData(ContractStatus.Active, -1, ContractStatus.Expired)]
    public void EffectiveStatus_ShouldDeriveExpiringAndExpiredFromDates(ContractStatus stored, int daysToEnd, ContractStatus expected)
    {
        ContractStatusRules.Effective(stored, Today.AddDays(daysToEnd), renewalNoticeDays: 30, Today).Should().Be(expected);
    }

    [Fact]
    public void SetPeriod_EndBeforeStart_ShouldThrow()
    {
        var contract = new Contract();

        contract.Invoking(c => c.SetPeriod(Today, Today))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("INVALID_CONTRACT_PERIOD");
    }

    [Fact]
    public void Lifecycle_ShouldOnlyAllowValidTransitions()
    {
        var contract = NewContract();

        contract.Activate();
        contract.Invoking(c => c.Activate()).Should().Throw<ConflictException>();
        contract.Terminate();

        contract.Status.Should().Be(ContractStatus.Terminated);
        contract.Invoking(c => c.Terminate()).Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_CONTRACT_TRANSITION");
        contract.Invoking(c => c.Renew("C-2", Today, Today.AddYears(1), null)).Should().Throw<ConflictException>();
    }

    [Fact]
    public void Renew_ShouldKeepOriginalAsRenewedAndCreateActiveSuccessor()
    {
        var contract = NewContract();
        contract.Activate();

        var successor = contract.Renew("C-2027", Today.AddDays(1), Today.AddYears(1), value: 1500m);

        contract.Status.Should().Be(ContractStatus.Renewed);
        contract.EndDate.Should().Be(Today.AddMonths(1), "el contrato original no se modifica");
        successor.Status.Should().Be(ContractStatus.Active);
        successor.Number.Should().Be("C-2027");
        successor.VendorId.Should().Be(contract.VendorId);
        successor.Value.Should().Be(1500m);
        successor.RenewalNoticeDays.Should().Be(contract.RenewalNoticeDays);
    }

    private static Contract NewContract()
    {
        var contract = new Contract { Number = "C-2026", Name = "Soporte", VendorId = 3, Type = ContractType.Support, Value = 1000m };
        contract.SetPeriod(Today.AddYears(-1), Today.AddMonths(1));
        return contract;
    }
}

public class AlertWindowsTests
{
    private static readonly int[] Windows = [90, 60, 30, 15, 7];

    [Theory]
    [InlineData(100, null)]
    [InlineData(90, 90)]
    [InlineData(61, 90)]
    [InlineData(20, 30)]
    [InlineData(7, 7)]
    [InlineData(0, 7)]
    [InlineData(-1, null)]
    public void Resolve_ShouldReturnSmallestCoveringWindow(int daysRemaining, int? expected)
    {
        AlertWindows.Resolve(daysRemaining, Windows).Should().Be(expected);
    }
}
