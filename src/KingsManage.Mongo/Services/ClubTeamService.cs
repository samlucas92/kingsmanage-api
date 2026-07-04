using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubTeamService : IClubTeamService
{
	private readonly IMongoCollection<ClubTeamProfile> clubTeams;
	private readonly IMongoCollection<Match> matches;
	private readonly IMongoCollection<ClubEvent> events;
	private readonly IMongoCollection<PlayerSeasonStats> playerSeasonStats;
	private readonly TenantMongoScope tenant;

	public ClubTeamService(MongoContext context, TenantMongoScope tenant)
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubTeamProfile)))
		{
			BsonClassMap.RegisterClassMap<ClubTeamProfile>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
		clubTeams = context.Database.GetCollection<ClubTeamProfile>("clubTeamProfiles");
		matches = context.Database.GetCollection<Match>("matches");
		events = context.Database.GetCollection<ClubEvent>("events");
		playerSeasonStats = context.Database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubTeamProfile>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		var storedProfiles = await clubTeams.Find(tenant.Filter<ClubTeamProfile>()).ToListAsync(cancellationToken);
		var profilesById = storedProfiles
			.Where(profile => profile.Id != Guid.Empty)
			.GroupBy(profile => profile.Id)
			.ToDictionary(group => group.Key, group => group.First());

		return GetDefaults()
			.Select(defaultProfile => profilesById.GetValueOrDefault(defaultProfile.Id) ?? defaultProfile)
			.Concat(storedProfiles.Where(profile =>
				profile.Id != DefaultClubTeams.FirstTeamId &&
				profile.Id != DefaultClubTeams.SecondTeamId
			))
			.Where(profile => !profile.IsDeleted)
			.OrderBy(profile => profile.SortOrder)
			.ThenBy(profile => profile.DisplayName)
			.ToList();
	}

	public async Task<ClubTeamProfile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var profile = await clubTeams
			.Find(tenant.Filter<ClubTeamProfile>(existingProfile => existingProfile.Id == id))
			.FirstOrDefaultAsync(cancellationToken);

		if (profile?.IsDeleted == true) return null;
		return profile ?? GetDefaults().SingleOrDefault(defaultProfile => defaultProfile.Id == id);
	}

	public async Task<ClubTeamProfile> CreateAsync(
		ClubTeamProfile profile,
		CancellationToken cancellationToken = default
	)
	{
		var now = DateTime.UtcNow;
		profile.Id = Guid.NewGuid();
		profile.IsDeleted = false;
		Normalise(profile);
		profile.CreatedAt = now;
		profile.UpdatedAt = now;
		tenant.Assign(profile);
		await clubTeams.InsertOneAsync(profile, cancellationToken: cancellationToken);
		return profile;
	}

	public async Task<ClubTeamDeleteResult> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var existingProfile = await GetByIdAsync(id, cancellationToken);
		if (existingProfile is null)
		{
			return ClubTeamDeleteResult.NotFound;
		}

		var legacyTeam = id == DefaultClubTeams.FirstTeamId
			? ClubTeam.First
			: ClubTeam.Second;
		var isLegacyDefault = id == DefaultClubTeams.FirstTeamId ||
			id == DefaultClubTeams.SecondTeamId;
		var matchInUse = await matches.Find(tenant.Filter<Match>(match =>
			match.TeamId == id ||
			(isLegacyDefault && match.TeamId == null && match.Team == legacyTeam)))
			.AnyAsync(cancellationToken);
		var eventInUse = await events.Find(tenant.Filter<ClubEvent>(clubEvent =>
			clubEvent.TeamIds.Contains(id) ||
			clubEvent.MatchLinks.Any(link => link.TeamId == id) ||
			(isLegacyDefault &&
				(clubEvent.TeamScope == ClubEventTeamScope.Both ||
					(id == DefaultClubTeams.FirstTeamId &&
						clubEvent.TeamScope == ClubEventTeamScope.First) ||
					(id == DefaultClubTeams.SecondTeamId &&
						clubEvent.TeamScope == ClubEventTeamScope.Second)))
		)).AnyAsync(cancellationToken);
		var statsInUse = await playerSeasonStats.Find(tenant.Filter<PlayerSeasonStats>(stats => stats.TeamId == id))
			.AnyAsync(cancellationToken);

		if (matchInUse || eventInUse || statsInUse)
		{
			return ClubTeamDeleteResult.InUse;
		}

		if (id == DefaultClubTeams.FirstTeamId || id == DefaultClubTeams.SecondTeamId)
		{
			existingProfile.IsDeleted = true;
			existingProfile.IsActive = false;
			existingProfile.UpdatedAt = DateTime.UtcNow;
			tenant.Assign(existingProfile);
			await clubTeams.ReplaceOneAsync(
				tenant.Filter<ClubTeamProfile>(profile => profile.Id == id),
				existingProfile,
				new ReplaceOptions { IsUpsert = true },
				cancellationToken);
			return ClubTeamDeleteResult.Deleted;
		}

		await clubTeams.DeleteOneAsync(tenant.Filter<ClubTeamProfile>(profile => profile.Id == id), cancellationToken);
		return ClubTeamDeleteResult.Deleted;
	}

	public async Task<ClubTeamProfile> UpdateAsync(
		Guid id,
		ClubTeamProfile profile,
		CancellationToken cancellationToken = default
	)
	{
		var existingProfile = await GetByIdAsync(id, cancellationToken);
		var now = DateTime.UtcNow;
		profile.Id = id;
		profile.IsDeleted = false;
		Normalise(profile);
		profile.CreatedAt = existingProfile?.CreatedAt ?? now;
		profile.UpdatedAt = now;
		tenant.Assign(profile);

		await clubTeams.ReplaceOneAsync(
			tenant.Filter<ClubTeamProfile>(currentProfile => currentProfile.Id == id),
			profile,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken
		);
		return profile;
	}

	private static void Normalise(ClubTeamProfile profile)
	{
		profile.DisplayName = profile.DisplayName.Trim();
		profile.ShortName = profile.ShortName.Trim();
		profile.Competitions = (profile.Competitions ?? [])
			.Select(competition => competition.Trim())
			.Where(competition => !string.IsNullOrWhiteSpace(competition))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static IReadOnlyList<ClubTeamProfile> GetDefaults()
	{
		return
		[
			new ClubTeamProfile
			{
				Id = DefaultClubTeams.FirstTeamId,
				DisplayName = "First Team",
				ShortName = "First",
				SortOrder = 0
			},
			new ClubTeamProfile
			{
				Id = DefaultClubTeams.SecondTeamId,
				DisplayName = "Second Team",
				ShortName = "Second",
				SortOrder = 1
			}
		];
	}
}
