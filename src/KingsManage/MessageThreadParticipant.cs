namespace KingsManage;

public class MessageThreadParticipant
{
	public Guid UserId { get; set; }
	public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
	public DateTime? LastReadAt { get; set; }
}
