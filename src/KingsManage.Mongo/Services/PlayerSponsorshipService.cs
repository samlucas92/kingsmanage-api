using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class PlayerSponsorshipService : IPlayerSponsorshipService
{
	private readonly IMongoCollection<PlayerSponsorship> sponsorships;
	private readonly TenantMongoScope tenant;

	static PlayerSponsorshipService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(PlayerSponsorship)))
		{
			BsonClassMap.RegisterClassMap<PlayerSponsorship>(map =>
			{
				map.AutoMap();
				map.SetIgnoreExtraElements(true);
			});
		}
	}

	public PlayerSponsorshipService(MongoContext context, TenantMongoScope tenant)
	{
		sponsorships = context.Database.GetCollection<PlayerSponsorship>("playerSponsorships");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<PlayerSponsorship>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var items = await sponsorships
			.Find(tenant.Filter<PlayerSponsorship>())
			.SortByDescending(item => item.UpdatedAt)
			.ToListAsync(cancellationToken);
		return items.Select(Normalise).ToList();
	}

	public async Task<PlayerSponsorship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var item = await sponsorships
			.Find(tenant.Filter<PlayerSponsorship>(current => current.Id == id))
			.FirstOrDefaultAsync(cancellationToken);
		return item is null ? null : Normalise(item);
	}

	public async Task<PlayerSponsorship?> GetByGoCodeAsync(string goCode, CancellationToken cancellationToken = default)
	{
		var code = NormaliseCode(goCode);
		if (string.IsNullOrWhiteSpace(code)) return null;
		var item = await sponsorships.Find(current => current.GoCode == code).FirstOrDefaultAsync(cancellationToken);
		return item is null ? null : Normalise(item);
	}

	public async Task<PlayerSponsorship> CreateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default)
	{
		sponsorship.Id = sponsorship.Id == Guid.Empty ? Guid.NewGuid() : sponsorship.Id;
		sponsorship.GoCode = string.IsNullOrWhiteSpace(sponsorship.GoCode) ? GenerateCode() : NormaliseCode(sponsorship.GoCode);
		Prepare(sponsorship, true);
		tenant.Assign(sponsorship);
		await sponsorships.InsertOneAsync(sponsorship, cancellationToken: cancellationToken);
		return sponsorship;
	}

	public async Task<PlayerSponsorship?> UpdateAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default)
	{
		Prepare(sponsorship, false);
		tenant.Assign(sponsorship);
		var result = await sponsorships.ReplaceOneAsync(
			tenant.Filter<PlayerSponsorship>(current => current.Id == sponsorship.Id),
			sponsorship,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : sponsorship;
	}

	public async Task<PlayerSponsorship?> SubmitAsync(PlayerSponsorship sponsorship, CancellationToken cancellationToken = default)
	{
		Prepare(sponsorship, false);
		var filter = Builders<PlayerSponsorship>.Filter.Where(current =>
			current.Id == sponsorship.Id &&
			current.OrganizationId == sponsorship.OrganizationId &&
			current.ClubId == sponsorship.ClubId &&
			current.Status == PlayerSponsorshipStatus.Open &&
			current.SubmittedAt == null);
		var result = await sponsorships.ReplaceOneAsync(filter, sponsorship, cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : sponsorship;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var result = await sponsorships.DeleteOneAsync(
			tenant.Filter<PlayerSponsorship>(current => current.Id == id),
			cancellationToken);
		return result.DeletedCount > 0;
	}

	private static PlayerSponsorship Normalise(PlayerSponsorship item)
	{
		item.PlayerName ??= string.Empty;
		item.ClubName ??= string.Empty;
		item.ClubPrimaryColor ??= "#0f766e";
		item.ClubSecondaryColor ??= "#facc15";
		item.SponsorName ??= string.Empty;
		item.SponsorDescription ??= string.Empty;
		item.SponsorImageDataUrl ??= string.Empty;
		item.AcceptedTermsText ??= string.Empty;
		item.GoCode = NormaliseCode(item.GoCode);
		return item;
	}

	private static void Prepare(PlayerSponsorship item, bool isNew)
	{
		Normalise(item);
		item.PlayerName = item.PlayerName.Trim();
		item.ClubName = item.ClubName.Trim();
		item.SponsorName = item.SponsorName.Trim();
		item.SponsorDescription = item.SponsorDescription.Trim();
		if (isNew || item.CreatedAt == default) item.CreatedAt = DateTime.UtcNow;
		item.UpdatedAt = DateTime.UtcNow;
	}

	private static string NormaliseCode(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant();

	private static string GenerateCode() => Convert.ToHexString(Guid.NewGuid().ToByteArray())[..12].ToLowerInvariant();
}
