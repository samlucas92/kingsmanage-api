namespace KingsManage;

public class ClubEventSeenStatus
{
	public Guid PlayerId { get; set; }
	public DateTime SeenAt { get; set; } = DateTime.UtcNow;
}
