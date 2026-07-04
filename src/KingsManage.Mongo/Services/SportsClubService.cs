using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class SportsClubService : ISportsClubService
{
	private readonly IMongoCollection<SportsClub> _clubs;
	private readonly ITenantContext _tenant;

	public SportsClubService(MongoContext context, ITenantContext tenant)
	{
		_clubs = context.Database.GetCollection<SportsClub>("clubs");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<SportsClub>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await _clubs.Find(club => club.OrganizationId == _tenant.OrganizationId)
			.SortBy(club => club.Name)
			.ToListAsync(cancellationToken);

	public async Task<SportsClub?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await _clubs.Find(club => club.Id == id && club.OrganizationId == _tenant.OrganizationId)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<SportsClub> CreateAsync(SportsClub club, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		club.Id = Guid.NewGuid();
		club.OrganizationId = _tenant.OrganizationId;
		Normalise(club);
		club.CreatedAt = now;
		club.UpdatedAt = now;
		await _clubs.InsertOneAsync(club, cancellationToken: cancellationToken);
		return club;
	}

	public async Task<SportsClub?> UpdateAsync(Guid id, SportsClub club, CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(id, cancellationToken);
		if (existing is null) return null;

		existing.Name = club.Name;
		existing.Slug = club.Slug;
		existing.SportKey = club.SportKey;
		existing.PrimaryColor = club.PrimaryColor;
		existing.SecondaryColor = club.SecondaryColor;
		existing.ContactEmail = club.ContactEmail;
		existing.ContactPhone = club.ContactPhone;
		existing.WebsiteUrl = club.WebsiteUrl;
		existing.Venues = club.Venues;
		existing.SetupStep = club.SetupStep;
		existing.SetupCompletedAt = club.SetupCompletedAt;
		existing.CustomFormations = club.CustomFormations;
		existing.LogoFileId = club.LogoFileId;
		Normalise(existing);
		existing.UpdatedAt = DateTime.UtcNow;

		var result = await _clubs.ReplaceOneAsync(
			current => current.Id == id && current.OrganizationId == _tenant.OrganizationId,
			existing,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : existing;
	}

	public async Task<SportsClub?> SetLogoFileAsync(
		Guid id,
		Guid? logoFileId,
		CancellationToken cancellationToken = default
	)
	{
		return await _clubs.FindOneAndUpdateAsync(
			club => club.Id == id && club.OrganizationId == _tenant.OrganizationId,
			Builders<SportsClub>.Update
				.Set(club => club.LogoFileId, logoFileId)
				.Set(club => club.UpdatedAt, DateTime.UtcNow),
			new FindOneAndUpdateOptions<SportsClub>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}

	public async Task<SportsClub?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
	{
		var update = Builders<SportsClub>.Update
			.Set(club => club.IsActive, isActive)
			.Set(club => club.UpdatedAt, DateTime.UtcNow);

		return await _clubs.FindOneAndUpdateAsync(
			club => club.Id == id && club.OrganizationId == _tenant.OrganizationId,
			update,
			new FindOneAndUpdateOptions<SportsClub> { ReturnDocument = ReturnDocument.After },
			cancellationToken);
	}

	private static void Normalise(SportsClub club)
	{
		club.Name = club.Name.Trim();
		club.Slug = club.Slug.Trim().ToLowerInvariant();
		club.SportKey = club.SportKey.Trim().ToLowerInvariant();
		club.PrimaryColor = NormaliseColor(club.PrimaryColor, "#0f766e");
		club.SecondaryColor = NormaliseColor(club.SecondaryColor, "#d9f99d");
		club.ContactEmail = club.ContactEmail.Trim().ToLowerInvariant();
		club.ContactPhone = club.ContactPhone.Trim();
		club.WebsiteUrl = club.WebsiteUrl.Trim();
		club.Venues ??= [];
		foreach (var venue in club.Venues)
		{
			venue.Id = venue.Id == Guid.Empty ? Guid.NewGuid() : venue.Id;
			venue.Name = venue.Name?.Trim() ?? string.Empty;
			venue.Address = venue.Address?.Trim() ?? string.Empty;
			venue.MapUrl = venue.MapUrl?.Trim() ?? string.Empty;
		}
		if (club.Venues.Count > 0 && club.Venues.All(venue => !venue.IsDefault))
		{
			club.Venues[0].IsDefault = true;
		}
		var defaultVenueSeen = false;
		foreach (var venue in club.Venues)
		{
			if (!venue.IsDefault) continue;
			if (!defaultVenueSeen) defaultVenueSeen = true;
			else venue.IsDefault = false;
		}
		club.SetupStep = Math.Clamp(club.SetupStep, 0, 4);
		club.CustomFormations ??= [];
		foreach (var formation in club.CustomFormations)
		{
			formation.Key = formation.Key.Trim().ToLowerInvariant();
			formation.Name = formation.Name.Trim();
			formation.Slots ??= [];
			foreach (var slot in formation.Slots)
			{
				slot.Key = slot.Key.Trim().ToLowerInvariant();
				slot.Label = slot.Label.Trim().ToUpperInvariant();
			}
		}
	}

	private static string NormaliseColor(string? value, string fallback) =>
		string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
}
