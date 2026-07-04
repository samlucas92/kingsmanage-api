using KingsManage;
using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

[TestFixture]
public sealed class BillingControllerTests
{
	[Test]
	public async Task UpdateSubscription_ReturnsUpdatedClubAllowance()
	{
		var service = new StubBillingService();
		var controller = new BillingController(service, new StubTenantContext());

		var result = await controller.UpdateSubscription(
			new SubscriptionUpdate
			{
				ClubAllowance = 3,
				BillingEmail = "accounts@example.com"
			},
			CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		Assert.That(ok?.Value, Is.TypeOf<OrganizationSubscription>());
		Assert.That(((OrganizationSubscription)ok!.Value!).ClubAllowance, Is.EqualTo(3));
	}

	[Test]
	public async Task PlatformStatus_CanApplyGracePeriod()
	{
		var service = new StubBillingService();
		var controller = new PlatformBillingController(service);

		var result = await controller.SetStatus(
			DefaultTenant.OrganizationId,
			new SubscriptionStatusUpdate { Status = SubscriptionStatus.GracePeriod },
			CancellationToken.None);

		var ok = result.Result as OkObjectResult;
		Assert.That(
			((OrganizationSubscription)ok!.Value!).Status,
			Is.EqualTo(SubscriptionStatus.GracePeriod));
	}

	private sealed class StubTenantContext : ITenantContext
	{
		public bool IsAvailable => true;
		public Guid OrganizationId => DefaultTenant.OrganizationId;
		public Guid ClubId => DefaultTenant.ClubId;
	}

	private sealed class StubBillingService : IBillingService
	{
		private readonly OrganizationSubscription _subscription = new()
		{
			OrganizationId = DefaultTenant.OrganizationId,
			ClubAllowance = 1,
			BaseMonthlyPrice = 15,
			AdditionalClubMonthlyPrice = 5
		};

		public Task<OrganizationSubscription> GetCurrentAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(_subscription);
		public Task<OrganizationSubscription> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
			Task.FromResult(_subscription);
		public Task<OrganizationSubscription> UpdateCurrentAsync(SubscriptionUpdate update, CancellationToken cancellationToken = default)
		{
			_subscription.ClubAllowance = update.ClubAllowance;
			_subscription.BillingEmail = update.BillingEmail;
			return Task.FromResult(_subscription);
		}
		public Task<OrganizationSubscription> SetStatusAsync(Guid organizationId, SubscriptionStatusUpdate update, CancellationToken cancellationToken = default)
		{
			_subscription.Status = update.Status;
			return Task.FromResult(_subscription);
		}
		public Task<IReadOnlyList<BillingInvoice>> GetInvoicesAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<BillingInvoice>>([]);
		public Task<BillingInvoice> AddInvoiceAsync(BillingInvoice invoice, CancellationToken cancellationToken = default) =>
			Task.FromResult(invoice);
		public Task<bool> CanAddClubAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(true);
	}
}
