using KingsManage;

namespace KingsManage.Web.Models;

public sealed class CreateHandoverModel
{
	public Guid OperationalRoleId { get; set; }
	public Guid? OutgoingUserId { get; set; }
	public Guid? IncomingUserId { get; set; }
	public DateTime? DueAt { get; set; }
	public string Notes { get; set; } = string.Empty;
	public List<string> AccessTransfers { get; set; } = [];
	public List<string> AdditionalItems { get; set; } = [];
}

public sealed class SetHandoverItemStatusModel
{
	public HandoverItemStatus Status { get; set; }
	public string Notes { get; set; } = string.Empty;
}

public sealed class ConfirmHandoverItemModel
{
	public bool Confirmed { get; set; } = true;
	public string Notes { get; set; } = string.Empty;
}

public sealed class SetHandoverStatusModel
{
	public HandoverStatus Status { get; set; }
}
