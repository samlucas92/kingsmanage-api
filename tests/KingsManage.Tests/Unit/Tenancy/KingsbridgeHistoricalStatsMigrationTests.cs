using KingsManage.Mongo;

namespace KingsManage.Tests.Unit.Tenancy;

public class KingsbridgeHistoricalStatsMigrationTests
{
	[Test]
	public void Baseline_ShouldContainEveryCsvPlayer()
	{
		Assert.That(KingsbridgePre202627HistoricalStats.Count, Is.EqualTo(50));
	}

	[TestCase("Sam Lucas", 276, 55)]
	[TestCase(" sam   lucas ", 276, 55)]
	[TestCase("Mohsin Mohammed", 44, 13)]
	[TestCase("Muhitr Rahman", 77, 7)]
	[TestCase("Andrew Richard", 0, 0)]
	public void TryGetValue_ShouldResolveCsvValuesAndKnownNameAliases(
		string playerName,
		int expectedAppearances,
		int expectedGoals)
	{
		var found = KingsbridgePre202627HistoricalStats.TryGetValue(
			playerName,
			out var baseline);

		Assert.Multiple(() =>
		{
			Assert.That(found, Is.True);
			Assert.That(baseline.Appearances, Is.EqualTo(expectedAppearances));
			Assert.That(baseline.Goals, Is.EqualTo(expectedGoals));
		});
	}

	[Test]
	public void TryGetValue_WhenPlayerIsNotInCsv_ShouldNotCreateABaseline()
	{
		var found = KingsbridgePre202627HistoricalStats.TryGetValue(
			"Not In Import",
			out _);

		Assert.That(found, Is.False);
	}
}
