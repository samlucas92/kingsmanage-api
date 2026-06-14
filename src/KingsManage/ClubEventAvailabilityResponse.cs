namespace KingsManage;

public class ClubEventAvailabilityResponse
{
	public Guid PlayerId { get; set; }
	public ClubEventAvailabilityStatus Status { get; set; } = ClubEventAvailabilityStatus.Unanswered;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
