namespace KingsManage;

public sealed class FundingOpportunity : ITenantOwned
{
	public Guid OrganizationId { get; set; }
	public Guid ClubId { get; set; }
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Amount { get; set; } = string.Empty;
	public string ApplicationUrl { get; set; } = string.Empty;
	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public FundingOpportunityStatus Status { get; set; } = FundingOpportunityStatus.Open;
	public string StatusDetail { get; set; } = string.Empty;
	public FundingApplicationState ApplicationState { get; set; } = FundingApplicationState.Investigating;
	public string Notes { get; set; } = string.Empty;
	public string DuplicateKey { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum FundingOpportunityStatus
{
	Open,
	Upcoming,
	Rolling,
	Restricted,
	Closed
}

public enum FundingApplicationState
{
	Investigating,
	Preparing,
	Submitted,
	Completed,
	Failed,
	NotPursuing
}
