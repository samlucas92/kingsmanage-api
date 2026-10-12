namespace KingsManage;

public class PlayerSponsorship : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = string.Empty;
	public string GoCode { get; set; } = string.Empty;
	public PlayerSponsorshipStatus Status { get; set; } = PlayerSponsorshipStatus.Open;
	public string ClubName { get; set; } = string.Empty;
	public string ClubPrimaryColor { get; set; } = "#0f766e";
	public string ClubSecondaryColor { get; set; } = "#facc15";
	public string SponsorName { get; set; } = string.Empty;
	public string SponsorDescription { get; set; } = string.Empty;
	public string SponsorImageDataUrl { get; set; } = string.Empty;
	public bool TermsAccepted { get; set; }
	public string AcceptedTermsText { get; set; } = string.Empty;
	public DateTime? TermsAcceptedAt { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid CreatedByUserId { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PlayerSponsorshipStatus
{
	Open,
	Completed,
	Closed
}
