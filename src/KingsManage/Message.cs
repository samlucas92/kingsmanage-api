namespace KingsManage;

public class Message
{
	public Guid Id { get; set; }
	public Guid ThreadId { get; set; }
	public Guid SenderUserId { get; set; }
	public string SenderUserEmail { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public MessageStatus Status { get; set; } = MessageStatus.Active;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? DeletedAt { get; set; }
}
