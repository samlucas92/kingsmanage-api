using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class BillingService : IBillingService
{
	private readonly IMongoCollection<OrganizationSubscription> _subscriptions;
	private readonly IMongoCollection<BillingInvoice> _invoices;
	private readonly IMongoCollection<SportsClub> _clubs;
	private readonly ITenantContext _tenant;
	private readonly BillingSettings _settings;

	public BillingService(
		MongoContext context,
		ITenantContext tenant,
		BillingSettings settings)
	{
		_subscriptions = context.Database.GetCollection<OrganizationSubscription>("organizationSubscriptions");
		_invoices = context.Database.GetCollection<BillingInvoice>("billingInvoices");
		_clubs = context.Database.GetCollection<SportsClub>("clubs");
		_tenant = tenant;
		_settings = settings;
	}

	public Task<OrganizationSubscription> GetCurrentAsync(CancellationToken cancellationToken = default) =>
		GetByOrganizationAsync(_tenant.OrganizationId, cancellationToken);

	public async Task<OrganizationSubscription> GetByOrganizationAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		var existing = await _subscriptions
			.Find(subscription => subscription.OrganizationId == organizationId)
			.FirstOrDefaultAsync(cancellationToken);
		if (existing is not null) return ApplyStatusTransitions(existing);

		var existingClubCount = await _clubs.CountDocumentsAsync(
			club => club.OrganizationId == organizationId && club.IsActive,
			cancellationToken: cancellationToken);
		var now = DateTime.UtcNow;
		var subscription = new OrganizationSubscription
		{
			OrganizationId = organizationId,
			ClubAllowance = Math.Max(1, (int)existingClubCount),
			BaseMonthlyPrice = _settings.BaseMonthlyPrice,
			AdditionalClubMonthlyPrice = _settings.AdditionalClubMonthlyPrice,
			Currency = _settings.Currency,
			TrialEndsAt = now.AddDays(Math.Max(1, _settings.TrialDays)),
			CreatedAt = now,
			UpdatedAt = now
		};
		try
		{
			await _subscriptions.InsertOneAsync(subscription, cancellationToken: cancellationToken);
			return subscription;
		}
		catch (MongoWriteException exception) when (
			exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			return await _subscriptions
				.Find(item => item.OrganizationId == organizationId)
				.FirstAsync(cancellationToken);
		}
	}

	public async Task<OrganizationSubscription> UpdateCurrentAsync(
		SubscriptionUpdate update,
		CancellationToken cancellationToken = default)
	{
		var subscription = await GetCurrentAsync(cancellationToken);
		var activeClubCount = await _clubs.CountDocumentsAsync(
			club => club.OrganizationId == _tenant.OrganizationId && club.IsActive,
			cancellationToken: cancellationToken);
		if (update.ClubAllowance < Math.Max(1, activeClubCount))
			throw new ArgumentException("Club allowance cannot be lower than the number of active clubs.");
		if (update.ClubAllowance > 100)
			throw new ArgumentException("Club allowance cannot exceed 100.");
		if (!string.IsNullOrWhiteSpace(update.BillingEmail) &&
			!System.Net.Mail.MailAddress.TryCreate(update.BillingEmail.Trim(), out _))
			throw new ArgumentException("Billing email is invalid.");

		subscription.ClubAllowance = update.ClubAllowance;
		subscription.BillingEmail = update.BillingEmail.Trim().ToLowerInvariant();
		subscription.CancelAtPeriodEnd = update.CancelAtPeriodEnd;
		subscription.UpdatedAt = DateTime.UtcNow;
		await ReplaceAsync(subscription, cancellationToken);
		return subscription;
	}

	public async Task<OrganizationSubscription> SetStatusAsync(
		Guid organizationId,
		SubscriptionStatusUpdate update,
		CancellationToken cancellationToken = default)
	{
		var subscription = await GetByOrganizationAsync(organizationId, cancellationToken);
		subscription.Status = update.Status;
		subscription.CurrentPeriodEndsAt = update.CurrentPeriodEndsAt;
		subscription.GracePeriodEndsAt = update.Status == SubscriptionStatus.GracePeriod
			? update.GracePeriodEndsAt ?? DateTime.UtcNow.AddDays(Math.Max(1, _settings.GracePeriodDays))
			: update.GracePeriodEndsAt;
		subscription.UpdatedAt = DateTime.UtcNow;
		await ReplaceAsync(subscription, cancellationToken);
		return subscription;
	}

	public async Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default) =>
		await _invoices.Find(invoice => invoice.OrganizationId == organizationId)
			.SortByDescending(invoice => invoice.IssuedAt)
			.ToListAsync(cancellationToken);

	public async Task<BillingInvoice> AddInvoiceAsync(
		BillingInvoice invoice,
		CancellationToken cancellationToken = default)
	{
		invoice.Id = Guid.NewGuid();
		invoice.Number = invoice.Number.Trim();
		invoice.Status = invoice.Status.Trim();
		invoice.Currency = string.IsNullOrWhiteSpace(invoice.Currency)
			? _settings.Currency
			: invoice.Currency.Trim().ToUpperInvariant();
		await _invoices.InsertOneAsync(invoice, cancellationToken: cancellationToken);
		return invoice;
	}

	public async Task<bool> CanAddClubAsync(CancellationToken cancellationToken = default)
	{
		var subscription = await GetCurrentAsync(cancellationToken);
		var activeClubCount = await _clubs.CountDocumentsAsync(
			club => club.OrganizationId == _tenant.OrganizationId && club.IsActive,
			cancellationToken: cancellationToken);
		return IsWriteEnabled(subscription) && activeClubCount < subscription.ClubAllowance;
	}

	private OrganizationSubscription ApplyStatusTransitions(OrganizationSubscription subscription)
	{
		var now = DateTime.UtcNow;
		if (subscription.Status == SubscriptionStatus.Trialing &&
			subscription.TrialEndsAt <= now)
		{
			subscription.Status = SubscriptionStatus.GracePeriod;
			subscription.GracePeriodEndsAt = now.AddDays(Math.Max(1, _settings.GracePeriodDays));
		}
		if (subscription.Status == SubscriptionStatus.GracePeriod &&
			subscription.GracePeriodEndsAt <= now)
		{
			subscription.Status = SubscriptionStatus.PastDue;
		}
		return subscription;
	}

	private static bool IsWriteEnabled(OrganizationSubscription subscription) =>
		subscription.Status is SubscriptionStatus.Trialing or
			SubscriptionStatus.Active or
			SubscriptionStatus.GracePeriod;

	private async Task ReplaceAsync(
		OrganizationSubscription subscription,
		CancellationToken cancellationToken) =>
		await _subscriptions.ReplaceOneAsync(
			item => item.OrganizationId == subscription.OrganizationId,
			subscription,
			new ReplaceOptions { IsUpsert = true },
			cancellationToken);
}
