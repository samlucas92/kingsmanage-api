namespace KingsManage.Web.Services;

public sealed record ReportFilters(
	Guid SeasonId,
	Guid? TeamId = null,
	string? Competition = null,
	MatchVenue? Venue = null,
	DateTime? DateFrom = null,
	DateTime? DateTo = null,
	bool IncludeFriendlies = true);
