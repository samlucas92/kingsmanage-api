namespace KingsManage;

public class ClubEvent : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public ClubEventType Type { get; set; }
	public ClubEventTeamScope TeamScope { get; set; } = ClubEventTeamScope.Both;
	public List<Guid> TeamIds { get; set; } = [];
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime StartDateTime { get; set; }
	public DateTime? EndDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public Guid? RecurrenceSeriesId { get; set; }
	public ClubEventRecurrence? Recurrence { get; set; }
	public List<ClubEventMatchLink> MatchLinks { get; set; } = [];
	public List<ClubEventAvailabilityResponse> AvailabilityResponses { get; set; } = [];
	public List<ClubEventSeenStatus> SeenBy { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
