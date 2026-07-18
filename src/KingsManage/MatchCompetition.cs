namespace KingsManage;

public static class MatchCompetition
{
	public static bool IsFriendly(string? competition)
	{
		if (string.IsNullOrWhiteSpace(competition))
		{
			return false;
		}

		return competition.Trim().Contains(
			"friendly",
			StringComparison.OrdinalIgnoreCase);
	}
}
