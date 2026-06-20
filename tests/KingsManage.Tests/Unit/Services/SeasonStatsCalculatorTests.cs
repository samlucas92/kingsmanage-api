using KingsManage;

namespace KingsManage.Tests.Unit.Services;

public class SeasonStatsCalculatorTests
{
	private static readonly Guid SeasonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid StarterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid SubstituteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly Guid UnusedId = Guid.Parse("33333333-3333-3333-3333-333333333333");

	[Test]
	public void Calculate_ShouldDistinguishStartsSubstituteAppearancesAndUnusedSubstitutes()
	{
		var match = CreateCompletedMatch();
		match.SelectedPlayers =
		[
			new SelectedPlayer { PlayerId = StarterId, Area = "pitch" },
			new SelectedPlayer { PlayerId = SubstituteId, Area = "bench" },
			new SelectedPlayer { PlayerId = UnusedId, Area = "bench" }
		];
		match.PlayerStats =
		[
			new MatchPlayerStats
			{
				PlayerId = StarterId,
				AppearanceType = MatchAppearanceType.Started,
				Goals = 1,
				Minutes = 90
			},
			new MatchPlayerStats
			{
				PlayerId = SubstituteId,
				AppearanceType = MatchAppearanceType.SubstituteUsed,
				Assists = 1,
				Minutes = 25
			},
			new MatchPlayerStats
			{
				PlayerId = UnusedId,
				AppearanceType = MatchAppearanceType.UnusedSubstitute
			}
		];

		var result = SeasonStatsCalculator.Calculate(SeasonId, [match]);

		Assert.Multiple(() =>
		{
			var starter = result.Single(stats => stats.PlayerId == StarterId);
			Assert.That(starter.Appearances, Is.EqualTo(1));
			Assert.That(starter.Starts, Is.EqualTo(1));
			Assert.That(starter.Goals, Is.EqualTo(1));
			var substitute = result.Single(stats => stats.PlayerId == SubstituteId);
			Assert.That(substitute.Appearances, Is.EqualTo(1));
			Assert.That(substitute.Bench, Is.EqualTo(1));
			Assert.That(substitute.Assists, Is.EqualTo(1));
			var unused = result.Single(stats => stats.PlayerId == UnusedId);
			Assert.That(unused.Appearances, Is.Zero);
			Assert.That(unused.UnusedSubstitutes, Is.EqualTo(1));
		});
	}

	[Test]
	public void Calculate_WithLegacyReports_ShouldInferAppearanceFromLineupArea()
	{
		var match = CreateCompletedMatch();
		match.SelectedPlayers =
		[
			new SelectedPlayer { PlayerId = StarterId, Area = "pitch" },
			new SelectedPlayer { PlayerId = SubstituteId, Area = "bench" }
		];
		match.PlayerStats =
		[
			new MatchPlayerStats { PlayerId = StarterId },
			new MatchPlayerStats { PlayerId = SubstituteId }
		];

		var result = SeasonStatsCalculator.Calculate(SeasonId, [match]);

		Assert.Multiple(() =>
		{
			Assert.That(result.Single(stats => stats.PlayerId == StarterId).Starts, Is.EqualTo(1));
			Assert.That(result.Single(stats => stats.PlayerId == SubstituteId).Bench, Is.EqualTo(1));
		});
	}

	private static Match CreateCompletedMatch()
	{
		return new Match
		{
			SeasonId = SeasonId,
			Team = ClubTeam.First,
			IsCompleted = true,
			SelectedPlayers = [],
			PlayerStats = []
		};
	}
}
