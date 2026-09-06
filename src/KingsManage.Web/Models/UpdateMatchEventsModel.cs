using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UpdateMatchEventsModel
{
	public int MatchDurationMinutes { get; set; } = 90;
	public List<MatchTimelineEvent> MatchEvents { get; set; } = [];
}
