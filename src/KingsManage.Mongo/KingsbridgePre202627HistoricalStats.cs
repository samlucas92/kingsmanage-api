namespace KingsManage.Mongo;

public readonly record struct HistoricalStatsBaseline(int Appearances, int Goals);

public static class KingsbridgePre202627HistoricalStats
{
	private static readonly IReadOnlyDictionary<string, HistoricalStatsBaseline> Baselines =
		new Dictionary<string, HistoricalStatsBaseline>(StringComparer.OrdinalIgnoreCase)
		{
			["Adam Tucker"] = new(205, 10),
			["Alex Wilson"] = new(237, 53),
			["Alhadi Yagob"] = new(14, 4),
			["Arwel Davies"] = new(159, 81),
			["Bamidele Abraham"] = new(12, 0),
			["Chris Davison"] = new(13, 0),
			["Chris Morgan"] = new(287, 220),
			["Chris Ramsell"] = new(305, 39),
			["Corum Davies"] = new(157, 30),
			["Dai Rowe"] = new(311, 50),
			["Daniel Carney"] = new(8, 0),
			["Daniel Martlew"] = new(35, 0),
			["Devon Hough"] = new(7, 0),
			["Jack Davies"] = new(129, 106),
			["Jean-Paul Haba"] = new(10, 6),
			["John Hough"] = new(34, 0),
			["Jordan Stephen"] = new(48, 0),
			["Josh Perkins"] = new(4, 0),
			["Lee Hartnoll"] = new(103, 0),
			["Lee Seager"] = new(228, 40),
			["Luke Barroccu"] = new(19, 3),
			["Mark Corcoran"] = new(242, 85),
			["Mark Newey"] = new(33, 0),
			["Mohammed Alkhammasi"] = new(15, 0),
			["Muhammed Saleh"] = new(28, 8),
			["Mohammed Ali"] = new(87, 40),
			["Moshin Mohammed"] = new(44, 13),
			["Muhitur Rahman"] = new(77, 7),
			["Nick Hopkins"] = new(291, 144),
			["Omer Talal Mubarak"] = new(52, 13),
			["Rabi Hadari"] = new(53, 30),
			["Rhys Andrew"] = new(4, 0),
			["Rhys Richardson"] = new(117, 63),
			["Ryan Thomas"] = new(37, 3),
			["Sam Lucas"] = new(276, 55),
			["Thom Norton"] = new(190, 88),
			["Tom Haynes"] = new(75, 25),
			["Tom Sinnott"] = new(20, 0),
			["Yousif Adulazeez"] = new(15, 2),
			["Cameron Phillips"] = new(0, 0),
			["Ebube Ofong"] = new(0, 0),
			["Daniel Ogunbayo"] = new(0, 0),
			["Phinehas Ofremu"] = new(0, 0),
			["Joe Jones"] = new(0, 0),
			["Chris Avery"] = new(0, 0),
			["Zak Bird"] = new(0, 0),
			["Josh Tate"] = new(0, 0),
			["Kenneth Isaiah"] = new(0, 0),
			["Andrew Richards"] = new(0, 0),
			["Charlie Sleddon-Plant"] = new(0, 0)
		};

	private static readonly IReadOnlyDictionary<string, string> Aliases =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["Mohsin Mohammed"] = "Moshin Mohammed",
			["Muhitr Rahman"] = "Muhitur Rahman",
			["Andrew Richard"] = "Andrew Richards"
		};

	public static int Count => Baselines.Count;

	public static bool TryGetValue(string playerName, out HistoricalStatsBaseline baseline)
	{
		var normalisedName = string.Join(
			' ',
			playerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
		var canonicalName = Aliases.GetValueOrDefault(normalisedName, normalisedName);

		return Baselines.TryGetValue(canonicalName, out baseline);
	}
}
