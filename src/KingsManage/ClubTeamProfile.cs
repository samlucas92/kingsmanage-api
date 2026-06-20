namespace KingsManage;

public class ClubTeamProfile
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string DisplayName { get; set; } = string.Empty;
	public string ShortName { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public int SortOrder { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class DefaultClubTeams
{
	public static readonly Guid FirstTeamId = Guid.Parse("11111111-1111-1111-1111-111111111101");
	public static readonly Guid SecondTeamId = Guid.Parse("22222222-2222-2222-2222-222222222202");

	public static Guid FromLegacy(ClubTeam team)
	{
		return team == ClubTeam.Second ? SecondTeamId : FirstTeamId;
	}
}
