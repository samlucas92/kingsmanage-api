using KingsManage;
using KingsManage.Mongo;

namespace KingsManage.Tests.Unit.Tenancy;

[TestFixture]
public sealed class TenantMongoScopeTests
{
	[Test]
	public void Assign_OverridesClientSuppliedTenantIds()
	{
		var organizationId = Guid.NewGuid();
		var clubId = Guid.NewGuid();
		var scope = new TenantMongoScope(new TestTenantContext(organizationId, clubId));
		var player = new Player
		{
			OrganizationId = Guid.NewGuid(),
			ClubId = Guid.NewGuid()
		};

		scope.Assign(player);

		Assert.That(player.OrganizationId, Is.EqualTo(organizationId));
		Assert.That(player.ClubId, Is.EqualTo(clubId));
	}

	private sealed class TestTenantContext(Guid organizationId, Guid clubId) : ITenantContext
	{
		public bool IsAvailable => true;
		public Guid OrganizationId { get; } = organizationId;
		public Guid ClubId { get; } = clubId;
	}
}
