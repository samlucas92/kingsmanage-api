namespace KingsManage;

public enum MatchCompetitionType
{
	Unknown,
	League,
	Cup,
	Friendly,
	Tournament
}

public static class MatchCompetition
{
	public const string NoCompetitionDisplayName = "No competition";

	public static string DisplayName(string? competition)
	{
		return string.IsNullOrWhiteSpace(competition)
			? NoCompetitionDisplayName
			: competition.Trim();
	}

	public static bool IsFriendly(string? competition)
	{
		return GetCompetitionType(competition) == MatchCompetitionType.Friendly;
	}

	public static MatchCompetitionType GetCompetitionType(string? competition)
	{
		var displayName = DisplayName(competition);

		if (displayName.Equals(NoCompetitionDisplayName, StringComparison.OrdinalIgnoreCase))
		{
			return MatchCompetitionType.Unknown;
		}

		if (displayName.Contains("friendly", StringComparison.OrdinalIgnoreCase))
		{
			return MatchCompetitionType.Friendly;
		}

		if (displayName.Contains("cup", StringComparison.OrdinalIgnoreCase))
		{
			return MatchCompetitionType.Cup;
		}

		if (displayName.Contains("tournament", StringComparison.OrdinalIgnoreCase))
		{
			return MatchCompetitionType.Tournament;
		}

		return MatchCompetitionType.League;
	}
}
