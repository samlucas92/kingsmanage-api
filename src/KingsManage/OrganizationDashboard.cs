namespace KingsManage;

public sealed class OrganizationDashboard
{
	public int ClubCount { get; set; }
	public int TeamCount { get; set; }
	public int UserCount { get; set; }
	public int PlayerCount { get; set; }
	public OrganizationFinanceSummary Finance { get; set; } = new();
	public List<OrganizationClubSummary> Clubs { get; set; } = [];
	public List<OrganizationUpcomingItem> UpcomingFixtures { get; set; } = [];
	public List<OrganizationUpcomingItem> UpcomingEvents { get; set; } = [];
}

public sealed class OrganizationFinanceSummary
{
	public decimal Charged { get; set; }
	public decimal Paid { get; set; }
	public decimal Adjustments { get; set; }
	public decimal Outstanding { get; set; }
}

public sealed class OrganizationClubSummary
{
	public Guid ClubId { get; set; }
	public string ClubName { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int TeamCount { get; set; }
	public int UserCount { get; set; }
	public int PlayerCount { get; set; }
	public decimal OutstandingFinance { get; set; }
	public List<string> Attention { get; set; } = [];
}

public sealed class OrganizationUpcomingItem
{
	public Guid Id { get; set; }
	public Guid ClubId { get; set; }
	public string ClubName { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public DateTime StartsAt { get; set; }
	public string Location { get; set; } = string.Empty;
}

public interface IOrganizationDashboardService
{
	Task<OrganizationDashboard?> GetAsync(
		Guid? clubId = null,
		CancellationToken cancellationToken = default);
}
