using KingsManage;
using KingsManage.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

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

	[TestCase(typeof(Player))]
	[TestCase(typeof(Match))]
	[TestCase(typeof(ClubEvent))]
	[TestCase(typeof(ClubPost))]
	[TestCase(typeof(FinanceTransaction))]
	[TestCase(typeof(Season))]
	[TestCase(typeof(ClubFile))]
	public void Filter_AlwaysContainsOrganizationAndClubBoundaries(Type resourceType)
	{
		var method = typeof(TenantMongoScopeTests)
			.GetMethod(
				nameof(AssertTenantFilter),
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Static)!
			.MakeGenericMethod(resourceType);

		Assert.That(
			() => method.Invoke(null, null),
			Throws.Nothing,
			resourceType.Name);
	}

	private static void AssertTenantFilter<T>() where T : ITenantOwned
	{
		var organizationId = Guid.NewGuid();
		var clubId = Guid.NewGuid();
		var scope = new TenantMongoScope(new TestTenantContext(organizationId, clubId));
		var serializer = BsonSerializer.LookupSerializer<T>();
		var rendered = scope.Filter<T>().Render(
			new RenderArgs<T>(serializer, BsonSerializer.SerializerRegistry));

		Assert.That(rendered.ToString(), Does.Contain("OrganizationId"));
		Assert.That(rendered.ToString(), Does.Contain("ClubId"));
		Assert.That(
			rendered["OrganizationId"],
			Is.EqualTo(new BsonBinaryData(organizationId, GuidRepresentation.Standard)));
		Assert.That(
			rendered["ClubId"],
			Is.EqualTo(new BsonBinaryData(clubId, GuidRepresentation.Standard)));
	}

	private sealed class TestTenantContext(Guid organizationId, Guid clubId) : ITenantContext
	{
		public bool IsAvailable => true;
		public Guid OrganizationId { get; } = organizationId;
		public Guid ClubId { get; } = clubId;
	}
}
