namespace KingsManage;

public class MessageThread : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public MessageThreadType Type { get; set; } = MessageThreadType.Direct;
	public string Title { get; set; } = string.Empty;
	public string DirectPairKey { get; set; } = string.Empty;
	public Guid? TeamId { get; set; }
	public Guid? EventId { get; set; }
	public List<MessageThreadParticipant> Participants { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
