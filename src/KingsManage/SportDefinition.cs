namespace KingsManage;

public sealed class SportDefinition
{
	public string Key { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string Surface { get; init; } = string.Empty;
	public int PlayersPerSide { get; init; }
	public IReadOnlyList<SportPositionDefinition> Positions { get; init; } = [];
	public IReadOnlyList<SportFormationDefinition> Formations { get; init; } = [];
}

public sealed class SportPositionDefinition
{
	public string Key { get; init; } = string.Empty;
	public string Label { get; init; } = string.Empty;
	public string Group { get; init; } = string.Empty;
}

public sealed class SportFormationDefinition
{
	public string Key { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
}

public static class SportCatalog
{
	public static readonly IReadOnlyList<SportDefinition> All =
	[
		Create("football", "Football", "football-pitch", 11,
			["GK|Goalkeeper|Goalkeeper", "LB|Left Back|Defence", "CB|Centre Back|Defence", "RB|Right Back|Defence", "LWB|Left Wing Back|Defence", "RWB|Right Wing Back|Defence", "CDM|Defensive Midfielder|Midfield", "CM|Central Midfielder|Midfield", "CAM|Attacking Midfielder|Midfield", "LM|Left Midfielder|Midfield", "RM|Right Midfielder|Midfield", "LW|Left Wing|Attack", "RW|Right Wing|Attack", "ST|Striker|Attack", "CF|Centre Forward|Attack"],
			["4-4-2", "4-3-3", "3-5-2", "4-2-3-1"]),
		Create("rugby-union", "Rugby Union", "rugby-pitch", 15,
			["PR|Prop|Forwards", "HK|Hooker|Forwards", "LK|Lock|Forwards", "FL|Flanker|Forwards", "N8|Number Eight|Forwards", "SH|Scrum Half|Backs", "FH|Fly Half|Backs", "CE|Centre|Backs", "WG|Wing|Backs", "FB|Full Back|Backs"], ["standard-xv"]),
		Create("cricket", "Cricket", "cricket-field", 11,
			["WK|Wicket Keeper|Field", "SL|Slip|Field", "PT|Point|Field", "CV|Cover|Field", "MO|Mid Off|Field", "MN|Mid On|Field", "FL|Fine Leg|Field", "DEEP|Deep Fielder|Field", "BAT|Batter|Batting", "AR|All-rounder|All-rounder", "BOWL|Bowler|Bowling"], ["fielding-standard"]),
		Create("hockey", "Hockey", "hockey-pitch", 11,
			["GK|Goalkeeper|Goalkeeper", "DF|Defender|Defence", "MF|Midfielder|Midfield", "FW|Forward|Attack"], ["3-4-3", "4-3-3"]),
		Create("netball", "Netball", "netball-court", 7,
			["GS|Goal Shooter|Attack", "GA|Goal Attack|Attack", "WA|Wing Attack|Midcourt", "C|Centre|Midcourt", "WD|Wing Defence|Midcourt", "GD|Goal Defence|Defence", "GK|Goal Keeper|Defence"], ["standard-seven"])
	];

	public static SportDefinition? Find(string key) => All.FirstOrDefault(sport => sport.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

	private static SportDefinition Create(string key, string name, string surface, int players, string[] positions, string[] formations) => new()
	{
		Key = key, Name = name, Surface = surface, PlayersPerSide = players,
		Positions = positions.Select(value => value.Split('|')).Select(parts => new SportPositionDefinition { Key = parts[0], Label = parts[1], Group = parts[2] }).ToList(),
		Formations = formations.Select(value => new SportFormationDefinition { Key = value, Name = value }).ToList()
	};
}
