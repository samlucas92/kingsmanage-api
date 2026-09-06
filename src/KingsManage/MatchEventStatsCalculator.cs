namespace KingsManage;

public static class MatchEventStatsCalculator
{
	public static List<MatchPlayerStats> Calculate(
		IEnumerable<SelectedPlayer> selectedPlayers,
		IEnumerable<MatchTimelineEvent> matchEvents,
		int matchDurationMinutes,
		IEnumerable<MatchPlayerStats>? existingStats = null)
	{
		var duration = Math.Clamp(matchDurationMinutes, 1, 180);
		var savedStats = (existingStats ?? [])
			.GroupBy(stat => stat.PlayerId)
			.ToDictionary(group => group.Key, group => group.First());
		var selected = selectedPlayers
			.Where(player => player.PlayerId != Guid.Empty)
			.GroupBy(player => player.PlayerId)
			.Select(group => group.First())
			.ToList();
		var stats = selected.ToDictionary(
			player => player.PlayerId,
			player => CreatePlayerStats(player, savedStats));
		var activeSince = selected
			.Where(player => player.Area.Equals("pitch", StringComparison.OrdinalIgnoreCase))
			.ToDictionary(player => player.PlayerId, _ => 0);

		var orderedEvents = matchEvents
			.Select((matchEvent, index) => new { MatchEvent = matchEvent, Index = index })
			.OrderBy(item => item.MatchEvent.Minute)
			.ThenBy(item => item.Index)
			.Select(item => item.MatchEvent);

		foreach (var matchEvent in orderedEvents)
		{
			var minute = Math.Clamp(matchEvent.Minute, 0, duration);
			if (!stats.TryGetValue(matchEvent.PlayerId, out var playerStats))
			{
				continue;
			}

			switch (matchEvent.Type)
			{
				case MatchTimelineEventType.Goal:
					playerStats.Goals++;
					if (matchEvent.SecondaryPlayerId.HasValue &&
						stats.TryGetValue(matchEvent.SecondaryPlayerId.Value, out var assistStats))
					{
						assistStats.Assists++;
					}
					break;
				case MatchTimelineEventType.YellowCard:
					playerStats.YellowCards++;
					break;
				case MatchTimelineEventType.RedCard:
					playerStats.RedCards++;
					break;
				case MatchTimelineEventType.Substitution:
					ApplySubstitution(
						matchEvent,
						minute,
						stats,
						activeSince);
					break;
			}
		}

		foreach (var (playerId, enteredAt) in activeSince)
		{
			stats[playerId].Minutes += duration - enteredAt;
		}

		return selected.Select(player => stats[player.PlayerId]).ToList();
	}

	private static MatchPlayerStats CreatePlayerStats(
		SelectedPlayer player,
		IReadOnlyDictionary<Guid, MatchPlayerStats> savedStats)
	{
		savedStats.TryGetValue(player.PlayerId, out var savedStat);
		return new MatchPlayerStats
		{
			PlayerId = player.PlayerId,
			AppearanceType = player.Area.Equals("pitch", StringComparison.OrdinalIgnoreCase)
				? MatchAppearanceType.Started
				: MatchAppearanceType.UnusedSubstitute,
			IsMOTM = savedStat?.IsMOTM ?? false,
			Note = savedStat?.Note ?? string.Empty
		};
	}

	private static void ApplySubstitution(
		MatchTimelineEvent matchEvent,
		int minute,
		IReadOnlyDictionary<Guid, MatchPlayerStats> stats,
		Dictionary<Guid, int> activeSince)
	{
		if (!matchEvent.SecondaryPlayerId.HasValue)
		{
			return;
		}

		var playerOffId = matchEvent.SecondaryPlayerId.Value;
		if (activeSince.Remove(playerOffId, out var enteredAt) &&
			stats.TryGetValue(playerOffId, out var playerOffStats))
		{
			playerOffStats.Minutes += Math.Max(0, minute - enteredAt);
		}

		if (activeSince.ContainsKey(matchEvent.PlayerId))
		{
			return;
		}

		activeSince[matchEvent.PlayerId] = minute;
		var playerOnStats = stats[matchEvent.PlayerId];
		if (playerOnStats.AppearanceType == MatchAppearanceType.UnusedSubstitute)
		{
			playerOnStats.AppearanceType = MatchAppearanceType.SubstituteUsed;
		}
	}
}
