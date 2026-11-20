using Hestia.Investors;
using Hestia.Tenancy;

namespace Hestia.Unit.Tests;

public class InvestorUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static InvestorUser Active() => new(TenantId.Parse("kestrel-trust"), "u1", "Ayanda M.", InvestorUserStatus.Active, null);

    [Fact]
    public void Revoking_marks_the_user_revoked_with_the_time()
    {
        var revoked = Active().Revoke(Now);
        Assert.Equal(InvestorUserStatus.Revoked, revoked.Status);
        Assert.Equal(Now, revoked.RevokedAt);
        Assert.False(revoked.HasAccess);
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_time()
    {
        var once = Active().Revoke(Now);
        Assert.Equal(Now, once.Revoke(Now.AddHours(1)).RevokedAt);
    }

    [Fact]
    public void Restoring_gives_access_back() => Assert.True(Active().Revoke(Now).Restore().HasAccess);
}
