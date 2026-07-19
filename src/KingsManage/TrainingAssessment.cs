namespace KingsManage;

public class TrainingAssessment : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid EventId { get; set; }
	public Guid PlayerId { get; set; }
	public TrainingPlayerRole PlayerRole { get; set; }
	public List<TrainingMetricRating> Metrics { get; set; } = [];
	public string Notes { get; set; } = string.Empty;
	public Guid AssessedByUserId { get; set; }
	public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
