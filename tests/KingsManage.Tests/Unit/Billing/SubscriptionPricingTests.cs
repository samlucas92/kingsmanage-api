using KingsManage;

namespace KingsManage.Tests.Unit.Billing;

[TestFixture]
public sealed class SubscriptionPricingTests
{
	[TestCase(1, 15)]
	[TestCase(2, 20)]
	[TestCase(5, 35)]
	public void MonthlyPrice_IncreasesByTheConfiguredPerClubAmount(
		int clubAllowance,
		decimal expected)
	{
		var subscription = new OrganizationSubscription
		{
			ClubAllowance = clubAllowance,
			BaseMonthlyPrice = 15m,
			AdditionalClubMonthlyPrice = 5m
		};

		Assert.That(subscription.MonthlyPrice, Is.EqualTo(expected));
	}
}
