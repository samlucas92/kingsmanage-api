using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class MatchService : IMatchService
{
	private readonly IMongoCollection<Match> matches;
	private readonly TenantMongoScope tenant;
	private readonly ITeamAccessContext teamAccess;

	public MatchService(
		MongoContext context,
		TenantMongoScope tenant,
		ITeamAccessContext teamAccess)
	{
		matches = context.Database.GetCollection<Match>("matches");
		this.tenant = tenant;
		this.teamAccess = teamAccess;
	}

	public async Task<IReadOnlyList<Match>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await matches
			.Find(ReadFilter())
			.SortBy(match => match.Date)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<Match>> GetBySeasonAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await matches
			.Find(ReadFilter() & Builders<Match>.Filter.Eq(match => match.SeasonId, seasonId))
			.SortBy(match => match.Date)
			.ToListAsync(cancellationToken);
	}

	public async Task<Match?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		return await matches
			.Find(ReadFilter() & Builders<Match>.Filter.Eq(match => match.Id, id))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Match> CreateAsync(
		Match match,
		CancellationToken cancellationToken = default
	)
	{
		if (!CanAccessMatch(match))
		{
			throw new UnauthorizedAccessException("The match belongs to a team outside this membership.");
		}

		match.Id = match.Id == Guid.Empty
			? Guid.NewGuid()
			: match.Id;

		match.Opponent = (match.Opponent ?? string.Empty).Trim();
		match.Competition = (match.Competition ?? string.Empty).Trim();
		match.Location = (match.Location ?? string.Empty).Trim();
		match.State = MatchState.Upcoming;
		match.IsCompleted = false;
		match.Result = null;
		match.Notes ??= new MatchNotes();
		match.Postponements ??= [];
		match.SelectedPlayers ??= [];
		match.PlayerStats ??= [];
		match.CreatedAt = DateTime.UtcNow;
		match.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(match);

		await matches.InsertOneAsync(match, cancellationToken: cancellationToken);

		return match;
	}

	public async Task<Match?> UpdateAsync(
		Match match,
		CancellationToken cancellationToken = default
	)
	{
		if (!CanAccessMatch(match)) return null;

		match.Opponent = (match.Opponent ?? string.Empty).Trim();
		match.Competition = (match.Competition ?? string.Empty).Trim();
		match.Location = (match.Location ?? string.Empty).Trim();
		match.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(match);

		var result = await matches.ReplaceOneAsync(
			ManagementFilter() & Builders<Match>.Filter.Eq(existingMatch => existingMatch.Id, match.Id),
			match,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return match;
	}

	public async Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var result = await matches.DeleteOneAsync(
			ManagementFilter() & Builders<Match>.Filter.Eq(match => match.Id, id),
			cancellationToken
		);

		return result.DeletedCount > 0;
	}

	public async Task<Match?> SetResultAsync(
		Guid id,
		MatchResult result,
		CancellationToken cancellationToken = default
	)
	{
		var match = await GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return null;
		}

		match.Result = result;
		match.IsCompleted = true;
		match.State = GetResultState(match.Venue, result);
		match.UpdatedAt = DateTime.UtcNow;

		return await ReplaceAndReturnAsync(match, cancellationToken);
	}

	public async Task<Match?> ClearResultAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var match = await GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return null;
		}

		match.Result = null;
		match.IsCompleted = false;
		match.State = MatchState.Upcoming;
		match.UpdatedAt = DateTime.UtcNow;

		return await ReplaceAndReturnAsync(match, cancellationToken);
	}

	public async Task<Match?> SetSelectedPlayersAsync(
		Guid id,
		List<SelectedPlayer> selectedPlayers,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<Match>.Update
			.Set(match => match.SelectedPlayers, selectedPlayers)
			.Set(match => match.UpdatedAt, DateTime.UtcNow);

		return await FindOneAndUpdateAsync(id, update, cancellationToken);
	}

	public async Task<Match?> SetLineupFormationAsync(
		Guid id,
		LineupFormation formation,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<Match>.Update
			.Set(match => match.SelectedFormation, formation)
			.Set(match => match.UpdatedAt, DateTime.UtcNow);

		return await FindOneAndUpdateAsync(id, update, cancellationToken);
	}

	public async Task<Match?> SetLineupFormationKeyAsync(Guid id, string formationKey, CancellationToken cancellationToken = default)
	{
		var update = Builders<Match>.Update
			.Set(match => match.FormationKey, formationKey.Trim().ToLowerInvariant())
			.Set(match => match.UpdatedAt, DateTime.UtcNow);
		return await FindOneAndUpdateAsync(id, update, cancellationToken);
	}

	public async Task<Match?> ToggleLineupLockedAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var match = await GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return null;
		}

		match.IsLineupLocked = !match.IsLineupLocked;
		match.UpdatedAt = DateTime.UtcNow;

		return await ReplaceAndReturnAsync(match, cancellationToken);
	}

	public async Task<Match?> UpdateNotesAsync(
		Guid id,
		MatchNotes notes,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<Match>.Update
			.Set(match => match.Notes, notes)
			.Set(match => match.UpdatedAt, DateTime.UtcNow);

		return await FindOneAndUpdateAsync(id, update, cancellationToken);
	}

	public async Task<Match?> UpdatePlayerStatsAsync(
		Guid id,
		List<MatchPlayerStats> playerStats,
		CancellationToken cancellationToken = default
	)
	{
		var update = Builders<Match>.Update
			.Set(match => match.PlayerStats, playerStats)
			.Set(match => match.UpdatedAt, DateTime.UtcNow);

		return await FindOneAndUpdateAsync(id, update, cancellationToken);
	}

	public async Task<Match?> PostponeAsync(
		Guid id,
		DateTime newDate,
		string? reason,
		CancellationToken cancellationToken = default
	)
	{
		var match = await GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return null;
		}

		match.Postponements.Add(new PostponementAudit
		{
			Id = Guid.NewGuid(),
			OldDate = match.Date,
			NewDate = newDate,
			Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
			ChangedAt = DateTime.UtcNow
		});

		match.Date = newDate;
		match.State = MatchState.Postponed;
		match.IsCompleted = false;
		match.UpdatedAt = DateTime.UtcNow;

		return await ReplaceAndReturnAsync(match, cancellationToken);
	}

	public async Task<Match?> RestoreAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var match = await GetByIdAsync(id, cancellationToken);

		if (match is null)
		{
			return null;
		}

		var lastPostponement = match.Postponements.LastOrDefault();

		if (lastPostponement is not null)
		{
			match.Date = lastPostponement.OldDate;
		}

		match.State = MatchState.Upcoming;
		match.IsCompleted = false;
		match.UpdatedAt = DateTime.UtcNow;

		return await ReplaceAndReturnAsync(match, cancellationToken);
	}

	private async Task<Match?> ReplaceAndReturnAsync(
		Match match,
		CancellationToken cancellationToken
	)
	{
		tenant.Assign(match);
		var result = await matches.ReplaceOneAsync(
			ManagementFilter() & Builders<Match>.Filter.Eq(existingMatch => existingMatch.Id, match.Id),
			match,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return match;
	}

	private async Task<Match?> FindOneAndUpdateAsync(
		Guid id,
		UpdateDefinition<Match> update,
		CancellationToken cancellationToken
	)
	{
		return await matches.FindOneAndUpdateAsync(
			ManagementFilter() & Builders<Match>.Filter.Eq(match => match.Id, id),
			update,
			new FindOneAndUpdateOptions<Match>
			{
				ReturnDocument = ReturnDocument.After
			},
			cancellationToken
		);
	}

	private FilterDefinition<Match> ReadFilter() => tenant.Filter<Match>();

	private FilterDefinition<Match> ManagementFilter()
	{
		var filter = tenant.Filter<Match>();
		if (teamAccess.HasClubWideAccess) return filter;

		var teamIds = teamAccess.TeamIds.ToList();
		var access = Builders<Match>.Filter.In(
			match => match.TeamId,
			teamIds.Select(id => (Guid?)id));
		if (teamIds.Contains(DefaultClubTeams.FirstTeamId))
		{
			access |= Builders<Match>.Filter.And(
				Builders<Match>.Filter.Eq(match => match.TeamId, null),
				Builders<Match>.Filter.Eq(match => match.Team, ClubTeam.First));
		}
		if (teamIds.Contains(DefaultClubTeams.SecondTeamId))
		{
			access |= Builders<Match>.Filter.And(
				Builders<Match>.Filter.Eq(match => match.TeamId, null),
				Builders<Match>.Filter.Eq(match => match.Team, ClubTeam.Second));
		}
		return filter & access;
	}

	private bool CanAccessMatch(Match match) =>
		teamAccess.CanAccessTeam(
			match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team));

	private static MatchState GetResultState(MatchVenue venue, MatchResult result)
	{
		if (result.HomeGoals == result.AwayGoals)
		{
			return MatchState.Draw;
		}

		var homeWin = result.HomeGoals > result.AwayGoals;

		return venue switch
		{
			MatchVenue.Home => homeWin ? MatchState.Won : MatchState.Lost,
			MatchVenue.Away => homeWin ? MatchState.Lost : MatchState.Won,
			_ => MatchState.Upcoming
		};
	}
}
