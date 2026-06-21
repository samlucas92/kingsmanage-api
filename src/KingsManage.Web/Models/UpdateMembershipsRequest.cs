using KingsManage;

namespace KingsManage.Web.Models;

public sealed class UpdateMembershipsRequest
{
	public Guid? DefaultClubId { get; set; }
	public List<UserMembership> Memberships { get; set; } = [];
}
