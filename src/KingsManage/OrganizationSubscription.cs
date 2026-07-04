namespace KingsManage;

public enum SubscriptionStatus
{
	Trialing,
	Active,
	PastDue,
	GracePeriod,
	Cancelled
}

public sealed class OrganizationSubscription
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid OrganizationId { get; set; }
	public string PlanCode { get; set; } = SubscriptionPlans.CoreCode;
	public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
	public int ClubAllowance { get; set; } = 1;
	public decimal BaseMonthlyPrice { get; set; }
	public decimal AdditionalClubMonthlyPrice { get; set; }
	public string Currency { get; set; } = "GBP";
	public string BillingEmail { get; set; } = string.Empty;
	public DateTime? TrialEndsAt { get; set; }
	public DateTime? CurrentPeriodEndsAt { get; set; }
	public DateTime? GracePeriodEndsAt { get; set; }
	public bool CancelAtPeriodEnd { get; set; }
	public string Provider { get; set; } = "manual";
	public string ProviderCustomerId { get; set; } = string.Empty;
	public string ProviderSubscriptionId { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

	public decimal MonthlyPrice =>
		BaseMonthlyPrice + Math.Max(0, ClubAllowance - 1) * AdditionalClubMonthlyPrice;
}

public sealed class BillingInvoice
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid OrganizationId { get; set; }
	public string Number { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string Currency { get; set; } = "GBP";
	public string Status { get; set; } = "Open";
	public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
	public DateTime? PaidAt { get; set; }
	public string ProviderInvoiceId { get; set; } = string.Empty;
}

public static class SubscriptionPlans
{
	public const string CoreCode = "core";
}

public sealed class BillingSettings
{
	public decimal BaseMonthlyPrice { get; set; } = 15m;
	public decimal AdditionalClubMonthlyPrice { get; set; } = 5m;
	public int TrialDays { get; set; } = 30;
	public int GracePeriodDays { get; set; } = 14;
	public string Currency { get; set; } = "GBP";
}

public sealed class SubscriptionUpdate
{
	public int ClubAllowance { get; set; }
	public string BillingEmail { get; set; } = string.Empty;
	public bool CancelAtPeriodEnd { get; set; }
}

public sealed class SubscriptionStatusUpdate
{
	public SubscriptionStatus Status { get; set; }
	public DateTime? GracePeriodEndsAt { get; set; }
	public DateTime? CurrentPeriodEndsAt { get; set; }
}

public interface IBillingService
{
	Task<OrganizationSubscription> GetCurrentAsync(CancellationToken cancellationToken = default);
	Task<OrganizationSubscription> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
	Task<OrganizationSubscription> UpdateCurrentAsync(SubscriptionUpdate update, CancellationToken cancellationToken = default);
	Task<OrganizationSubscription> SetStatusAsync(Guid organizationId, SubscriptionStatusUpdate update, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(Guid organizationId, CancellationToken cancellationToken = default);
	Task<BillingInvoice> AddInvoiceAsync(BillingInvoice invoice, CancellationToken cancellationToken = default);
	Task<bool> CanAddClubAsync(CancellationToken cancellationToken = default);
}
