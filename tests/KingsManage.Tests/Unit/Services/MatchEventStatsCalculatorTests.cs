using KingsManage;

namespace KingsManage.Tests.Unit.Services;

public class MatchEventStatsCalculatorTests
{
	private static readonly Guid StarterId = Guid.NewGuid();
	private static readonly Guid SecondStarterId = Guid.NewGuid();
	private static readonly Guid SubstituteId = Guid.NewGuid();

	[Test]
	public void Calculate_AssumesUnsubstitutedStartersPlayedFullMatch()
	{
		var stats = MatchEventStatsCalculator.Calculate(
			SelectedPlayers(),
			[],
			90);

		Assert.Multiple(() =>
		{
			Assert.That(Find(stats, StarterId).Minutes, Is.EqualTo(90));
			Assert.That(Find(stats, StarterId).AppearanceType, Is.EqualTo(MatchAppearanceType.Started));
			Assert.That(Find(stats, SubstituteId).Minutes, Is.Zero);
			Assert.That(Find(stats, SubstituteId).AppearanceType, Is.EqualTo(MatchAppearanceType.UnusedSubstitute));
		});
	}

	[Test]
	public void Calculate_DerivesSubstitutionMinutesGoalsAssistsAndCards()
	{
		var events = new List<MatchTimelineEvent>
		{
			new()
			{
				Type = MatchTimelineEventType.Goal,
				Minute = 21,
				PlayerId = StarterId,
				SecondaryPlayerId = SecondStarterId
			},
			new()
			{
				Type = MatchTimelineEventType.YellowCard,
				Minute = 44,
				PlayerId = StarterId
			},
			new()
			{
				Type = MatchTimelineEventType.Substitution,
				Minute = 62,
				PlayerId = SubstituteId,
				SecondaryPlayerId = StarterId
			},
			new()
			{
				Type = MatchTimelineEventType.Goal,
				Minute = 80,
				PlayerId = SubstituteId
			}
		};

		var stats = MatchEventStatsCalculator.Calculate(SelectedPlayers(), events, 90);

		Assert.Multiple(() =>
		{
			Assert.That(Find(stats, StarterId).Minutes, Is.EqualTo(62));
			Assert.That(Find(stats, StarterId).Goals, Is.EqualTo(1));
			Assert.That(Find(stats, StarterId).YellowCards, Is.EqualTo(1));
			Assert.That(Find(stats, SecondStarterId).Assists, Is.EqualTo(1));
			Assert.That(Find(stats, SubstituteId).Minutes, Is.EqualTo(28));
			Assert.That(Find(stats, SubstituteId).Goals, Is.EqualTo(1));
			Assert.That(Find(stats, SubstituteId).AppearanceType, Is.EqualTo(MatchAppearanceType.SubstituteUsed));
		});
	}

	[Test]
	public void Calculate_PreservesManualMotmAndNotes()
	{
		var stats = MatchEventStatsCalculator.Calculate(
			SelectedPlayers(),
			[],
			90,
			[
				new MatchPlayerStats
				{
					PlayerId = StarterId,
					IsMOTM = true,
					Note = "Excellent performance"
				}
			]);

		Assert.Multiple(() =>
		{
			Assert.That(Find(stats, StarterId).IsMOTM, Is.True);
			Assert.That(Find(stats, StarterId).Note, Is.EqualTo("Excellent performance"));
		});
	}

	private static List<SelectedPlayer> SelectedPlayers() =>
	[
		new SelectedPlayer { PlayerId = StarterId, Area = "pitch" },
		new SelectedPlayer { PlayerId = SecondStarterId, Area = "pitch" },
		new SelectedPlayer { PlayerId = SubstituteId, Area = "bench" }
	];

	private static MatchPlayerStats Find(IEnumerable<MatchPlayerStats> stats, Guid playerId) =>
		stats.Single(stat => stat.PlayerId == playerId);
}
