namespace KingsManage;

public interface IPlatformOrganizationOnboardingService
{
	Task<PlatformOrganizationOnboardingOutcome> CreateAsync(
		PlatformOrganizationOnboardingInput input,
		CancellationToken cancellationToken = default);
}

public sealed class PlatformOrganizationOnboardingInput
{
	public string OrganizationName { get; set; } = string.Empty;
	public string OrganizationSlug { get; set; } = string.Empty;
	public string ClubName { get; set; } = string.Empty;
	public string ClubSlug { get; set; } = string.Empty;
	public string SportKey { get; set; } = string.Empty;
	public string PrimaryColor { get; set; } = "#0f766e";
	public string SecondaryColor { get; set; } = "#d9f99d";
	public string ClubContactEmail { get; set; } = string.Empty;
	public string AdministratorEmail { get; set; } = string.Empty;
	public string TemporaryPassword { get; set; } = string.Empty;
	public int ClubAllowance { get; set; } = 1;
	public string BillingEmail { get; set; } = string.Empty;
	public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trialing;
}

public sealed class PlatformOrganizationOnboardingResult
{
	public Organization Organization { get; set; } = new();
	public SportsClub Club { get; set; } = new();
	public string AdministratorEmail { get; set; } = string.Empty;
	public OrganizationSubscription Subscription { get; set; } = new();
}

public enum PlatformOrganizationOnboardingStatus
{
	Created,
	OrganizationSlugExists,
	ClubSlugExists,
	AdministratorEmailExists
}

public sealed record PlatformOrganizationOnboardingOutcome(
	PlatformOrganizationOnboardingStatus Status,
	PlatformOrganizationOnboardingResult? Result = null);
