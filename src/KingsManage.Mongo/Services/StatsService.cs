using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class StatsService : IStatsService
{
	private readonly IMongoCollection<Match> _matches;
	private readonly IMongoCollection<PlayerSeasonStats> _playerSeasonStats;
	private readonly IMongoCollection<PlayerHistoricalStats> _playerHistoricalStats;

	public StatsService(MongoContext context)
	{
		_matches = context.Database.GetCollection<Match>("matches");
		_playerSeasonStats = context.Database.GetCollection<PlayerSeasonStats>("playerSeasonStats");
		_playerHistoricalStats = context.Database.GetCollection<PlayerHistoricalStats>("playerHistoricalStats");
	}

	public async Task<IReadOnlyList<PlayerSeasonStats>> GetSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(stats => stats.SeasonId == seasonId)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<PlayerSeasonStats>> GetAllSeasonStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(_ => true)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerSeasonStats
			.Find(stats => stats.PlayerId == playerId)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<PlayerHistoricalStats>> GetHistoricalStatsAsync(
		CancellationToken cancellationToken = default
	)
	{
		return await _playerHistoricalStats
			.Find(_ => true)
			.ToListAsync(cancellationToken);
	}

	public async Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
		Guid playerId,
		CancellationToken cancellationToken = default
	)
	{
		return await _playerHistoricalStats
			.Find(stats => stats.PlayerId == playerId)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
		PlayerHistoricalStats stats,
		CancellationToken cancellationToken = default
	)
	{
		var existingStats = await GetHistoricalStatsByPlayerIdAsync(
			stats.PlayerId,
			cancellationToken
		);

		stats.Appearances = NormaliseStat(stats.Appearances);
		stats.Goals = NormaliseStat(stats.Goals);
		stats.UpdatedAt = DateTime.UtcNow;

		if (existingStats is not null)
		{
			stats.Id = existingStats.Id;
			stats.CreatedAt = existingStats.CreatedAt;
		}
		else if (stats.Id == Guid.Empty)
		{
			stats.Id = Guid.NewGuid();
			stats.CreatedAt = DateTime.UtcNow;
		}

		await _playerHistoricalStats.ReplaceOneAsync(
			existingStats => existingStats.PlayerId == stats.PlayerId,
			stats,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken
		);

		return stats;
	}

	public async Task RecalculateSeasonStatsAsync(
		Guid seasonId,
		CancellationToken cancellationToken = default
	)
	{
		var completedMatches = await _matches
			.Find(match => match.SeasonId == seasonId && match.IsCompleted)
			.ToListAsync(cancellationToken);

		var groupedStats = new Dictionary<PlayerSeasonStatsKey, PlayerSeasonStats>();

		foreach (var match in completedMatches)
		{
			ApplyMatchToSeasonStats(match, seasonId, groupedStats);
		}

		await _playerSeasonStats.DeleteManyAsync(
			stats => stats.SeasonId == seasonId,
			cancellationToken
		);

		var seasonStats = groupedStats.Values.ToList();

		if (seasonStats.Count == 0)
		{
			return;
		}

		await _playerSeasonStats.InsertManyAsync(
			seasonStats,
			cancellationToken: cancellationToken
		);
	}

	private static void ApplyMatchToSeasonStats(
		Match match,
		Guid seasonId,
		Dictionary<PlayerSeasonStatsKey, PlayerSeasonStats> groupedStats
	)
	{
		var selectedPlayers = match.SelectedPlayers ?? [];
		var playerStats = match.PlayerStats ?? [];
		var playerIds = selectedPlayers
			.Select(selectedPlayer => selectedPlayer.PlayerId)
			.Concat(playerStats.Select(stats => stats.PlayerId))
			.Where(playerId => playerId != Guid.Empty)
			.Distinct()
			.ToList();

		foreach (var playerId in playerIds)
		{
			var key = new PlayerSeasonStatsKey(playerId, seasonId, match.Team);
			var stats = GetOrCreateStats(groupedStats, key);
			var playerSelections = selectedPlayers
				.Where(selectedPlayer => selectedPlayer.PlayerId == playerId)
				.ToList();
			var selectedPlayer = playerSelections.FirstOrDefault();
			var matchPlayerStats = playerStats.FirstOrDefault(
				playerStat => playerStat.PlayerId == playerId
			);

			if (selectedPlayer is not null)
			{
				stats.Appearances++;

				if (selectedPlayer.Area.Equals(
					"pitch",
					StringComparison.OrdinalIgnoreCase
				))
				{
					stats.Starts++;
				}
				else
				{
					stats.Bench++;
				}
			}

			if (matchPlayerStats is null)
			{
				continue;
			}

			stats.Goals += NormaliseStat(matchPlayerStats.Goals);
			stats.Assists += NormaliseStat(matchPlayerStats.Assists);
			stats.Minutes += NormaliseStat(matchPlayerStats.Minutes);
			stats.YellowCards += NormaliseStat(matchPlayerStats.YellowCards);
			stats.RedCards += NormaliseStat(matchPlayerStats.RedCards);

			if (matchPlayerStats.IsMOTM)
			{
				stats.Motm++;
			}
		}
	}

	private static PlayerSeasonStats GetOrCreateStats(
		Dictionary<PlayerSeasonStatsKey, PlayerSeasonStats> groupedStats,
		PlayerSeasonStatsKey key
	)
	{
		if (groupedStats.TryGetValue(key, out var existingStats))
		{
			return existingStats;
		}

		var stats = new PlayerSeasonStats
		{
			Id = Guid.NewGuid(),
			PlayerId = key.PlayerId,
			SeasonId = key.SeasonId,
			Team = key.Team,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		groupedStats[key] = stats;
		return stats;
	}

	private static int NormaliseStat(int value)
	{
		return value < 0 ? 0 : value;
	}

	private readonly record struct PlayerSeasonStatsKey(
		Guid PlayerId,
		Guid SeasonId,
		ClubTeam Team
	);
}
