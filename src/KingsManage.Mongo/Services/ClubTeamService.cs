using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubTeamService : IClubTeamService
{
	private readonly IMongoCollection<ClubTeamProfile> _clubTeams;
	private readonly IMongoCollection<Match> _matches;
	private readonly IMongoCollection<ClubEvent> _events;
	private readonly IMongoCollection<PlayerSeasonStats> _playerSeasonStats;

	public ClubTeamService(MongoContext context)
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubTeamProfile)))
		{
			BsonClassMap.RegisterClassMap<ClubTeamProfile>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
		_clubTeams = context.Database.GetCollection<ClubTeamProfile>("clubTeamProfiles");
		_matches = context.Database.GetCollection<Match>("matches");
		_events = context.Database.GetCollection<ClubEvent>("events");
		_playerSeasonStats = context.Database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
	}

	public async Task<IReadOnlyList<ClubTeamProfile>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		var storedProfiles = await _clubTeams.Find(_ => true).ToListAsync(cancellationToken);
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
			.OrderBy(profile => profile.SortOrder)
			.ThenBy(profile => profile.DisplayName)
			.ToList();
	}

	public async Task<ClubTeamProfile?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var profile = await _clubTeams
			.Find(existingProfile => existingProfile.Id == id)
			.FirstOrDefaultAsync(cancellationToken);

		return profile ?? GetDefaults().SingleOrDefault(defaultProfile => defaultProfile.Id == id);
	}

	public async Task<ClubTeamProfile> CreateAsync(
		ClubTeamProfile profile,
		CancellationToken cancellationToken = default
	)
	{
		var now = DateTime.UtcNow;
		profile.Id = Guid.NewGuid();
		Normalise(profile);
		profile.CreatedAt = now;
		profile.UpdatedAt = now;
		await _clubTeams.InsertOneAsync(profile, cancellationToken: cancellationToken);
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

		if (id == DefaultClubTeams.FirstTeamId || id == DefaultClubTeams.SecondTeamId)
		{
			return ClubTeamDeleteResult.InUse;
		}

		var matchInUse = await _matches.Find(match => match.TeamId == id).AnyAsync(cancellationToken);
		var eventInUse = await _events.Find(clubEvent =>
			clubEvent.TeamIds.Contains(id) ||
			clubEvent.MatchLinks.Any(link => link.TeamId == id)
		).AnyAsync(cancellationToken);
		var statsInUse = await _playerSeasonStats.Find(stats => stats.TeamId == id)
			.AnyAsync(cancellationToken);

		if (matchInUse || eventInUse || statsInUse)
		{
			return ClubTeamDeleteResult.InUse;
		}

		await _clubTeams.DeleteOneAsync(profile => profile.Id == id, cancellationToken);
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
		Normalise(profile);
		profile.CreatedAt = existingProfile?.CreatedAt ?? now;
		profile.UpdatedAt = now;

		await _clubTeams.ReplaceOneAsync(
			currentProfile => currentProfile.Id == id,
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
