namespace KingsManage;

public static class SeasonStatsCalculator
{
	public static List<PlayerSeasonStats> Calculate(Guid seasonId, IEnumerable<Match> matches)
	{
		var groupedStats = new Dictionary<PlayerSeasonStatsKey, PlayerSeasonStats>();

		foreach (var match in matches.Where(match => match.SeasonId == seasonId && match.IsCompleted))
		{
			ApplyMatch(match, seasonId, groupedStats);
		}

		return groupedStats.Values.ToList();
	}

	private static void ApplyMatch(
		Match match,
		Guid seasonId,
		Dictionary<PlayerSeasonStatsKey, PlayerSeasonStats> groupedStats
	)
	{
		var selectedPlayers = match.SelectedPlayers ?? [];
		var playerStats = match.PlayerStats ?? [];

		foreach (var selectedPlayer in selectedPlayers.Where(player => player.PlayerId != Guid.Empty))
		{
			var key = new PlayerSeasonStatsKey(
				selectedPlayer.PlayerId,
				seasonId,
				match.TeamId ?? DefaultClubTeams.FromLegacy(match.Team),
				match.Team
			);
			var stats = GetOrCreateStats(groupedStats, key);
			var matchPlayerStats = playerStats.FirstOrDefault(
				playerStat => playerStat.PlayerId == selectedPlayer.PlayerId
			);
			var appearanceType = ResolveAppearanceType(selectedPlayer, matchPlayerStats);

			switch (appearanceType)
			{
				case MatchAppearanceType.Started:
					stats.Appearances++;
					stats.Starts++;
					break;
				case MatchAppearanceType.SubstituteUsed:
					stats.Appearances++;
					stats.Bench++;
					break;
				case MatchAppearanceType.UnusedSubstitute:
					stats.UnusedSubstitutes++;
					break;
			}

			if (matchPlayerStats is null || appearanceType == MatchAppearanceType.UnusedSubstitute)
			{
				continue;
			}

			stats.Goals += NormaliseStat(matchPlayerStats.Goals);
			stats.Assists += NormaliseStat(matchPlayerStats.Assists);
			stats.Minutes += NormaliseStat(matchPlayerStats.Minutes);
			stats.YellowCards += NormaliseStat(matchPlayerStats.YellowCards);
			stats.RedCards += NormaliseStat(matchPlayerStats.RedCards);
			stats.Motm += matchPlayerStats.IsMOTM ? 1 : 0;
		}
	}

	private static MatchAppearanceType ResolveAppearanceType(
		SelectedPlayer selectedPlayer,
		MatchPlayerStats? playerStats
	)
	{
		if (playerStats is not null && playerStats.AppearanceType != MatchAppearanceType.Unspecified)
		{
			return playerStats.AppearanceType;
		}

		return selectedPlayer.Area.Equals("pitch", StringComparison.OrdinalIgnoreCase)
			? MatchAppearanceType.Started
			: MatchAppearanceType.SubstituteUsed;
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
			PlayerId = key.PlayerId,
			SeasonId = key.SeasonId,
			TeamId = key.TeamId,
			Team = key.Team
		};
		groupedStats[key] = stats;
		return stats;
	}

	private static int NormaliseStat(int value) => Math.Max(value, 0);

	private readonly record struct PlayerSeasonStatsKey(
		Guid PlayerId,
		Guid SeasonId,
		Guid TeamId,
		ClubTeam Team
	);
}
