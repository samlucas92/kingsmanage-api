using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KingsManage;

[JsonConverter(typeof(StringEnumConverter))]
public enum MatchState
{
	Upcoming,
	Won,
	Lost,
	Draw,
	Postponed
}