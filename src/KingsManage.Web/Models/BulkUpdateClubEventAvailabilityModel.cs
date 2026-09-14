using KingsManage;

namespace KingsManage.Web.Models;

public sealed class BulkUpdateClubEventAvailabilityModel
{
	public List<BulkUpdateClubEventAvailabilityItemModel> Responses { get; set; } = [];
}

public sealed class BulkUpdateClubEventAvailabilityItemModel
{
	public Guid PlayerId { get; set; }
	public ClubEventAvailabilityStatus Status { get; set; }
}
