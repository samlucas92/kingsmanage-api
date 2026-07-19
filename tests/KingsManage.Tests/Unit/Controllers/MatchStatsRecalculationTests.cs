using KingsManage;
using KingsManage.Tests.Fakes;
using KingsManage.Web.Controllers;
using KingsManage.Web.Models;
using KingsManage.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Tests.Unit.Controllers;

public class MatchStatsRecalculationTests
{
	private static readonly Guid MatchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid SeasonOneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid SeasonTwoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly Guid PlayerOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	[Test]
	public async Task SetResult_WhenMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateMatch(MatchId, SeasonOneId));

		await controller.SetResult(
			MatchId.ToString(),
			new MatchResult { HomeGoals = 3, AwayGoals = 1 },
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task UpdatePlayerStats_WhenMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.UpdatePlayerStats(
			MatchId.ToString(),
			new List<MatchPlayerStats>
			{
				new() { PlayerId = PlayerOneId, Goals = 2, Minutes = 90 }
			},
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task UpdatePlayerStats_WithMoreThanOneMotm_ShouldReturnBadRequest()
	{
		var matchService = new FakeMatchService();
		var controller = CreateController(matchService, new FakeStatsService());
		var match = CreateCompletedMatch(MatchId, SeasonOneId);
		var playerTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		match.SelectedPlayers.Add(new SelectedPlayer { PlayerId = playerTwoId, Area = "bench" });
		matchService.Matches.Add(match);

		var result = await controller.UpdatePlayerStats(
			MatchId.ToString(),
			[
				new MatchPlayerStats { PlayerId = PlayerOneId, IsMOTM = true },
				new MatchPlayerStats { PlayerId = playerTwoId, IsMOTM = true }
			],
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
	}

	[Test]
	public async Task SetLineup_WhenMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.SetLineup(
			MatchId.ToString(),
			new List<SelectedPlayer>
			{
				new() { PlayerId = PlayerOneId, Area = "pitch", X = 50, Y = 60, PositionIndex = 1 }
			},
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task ClearResult_WhenMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.ClearResult(MatchId.ToString(), CancellationToken.None);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task Delete_WhenMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.Delete(MatchId.ToString(), CancellationToken.None);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task Update_WhenMatchMovesSeason_ShouldRecalculateOldAndNewSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.Update(
			MatchId.ToString(),
			CreateCompletedMatch(MatchId, SeasonTwoId),
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EquivalentTo(new[] { SeasonOneId, SeasonTwoId }));
	}

	[Test]
	public async Task Postpone_WhenCompletedMatchChanges_ShouldRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.Postpone(
			MatchId.ToString(),
			new PostponeMatchModel
			{
				NewDate = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
				Reason = "Bad weather"
			},
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task UpdateNotes_WhenOnlyNotesChange_ShouldNotRecalculateSeasonStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = CreateController(matchService, statsService);
		matchService.Matches.Add(CreateCompletedMatch(MatchId, SeasonOneId));

		await controller.UpdateNotes(
			MatchId.ToString(),
			new MatchNotes { General = "Only a note" },
			CancellationToken.None
		);

		Assert.That(statsService.RecalculatedSeasonIds, Is.Empty);
	}

	private static MatchesController CreateController(
		FakeMatchService matchService,
		FakeStatsService statsService)
	{
		return new MatchesController(
			new MatchQueryService(matchService),
			matchService,
			statsService);
	}

	private static Match CreateMatch(Guid id, Guid seasonId)
	{
		return new Match
		{
			Id = id,
			SeasonId = seasonId,
			Team = ClubTeam.First,
			Opponent = "Test Opponent",
			Competition = "League",
			Location = "The Rec",
			Date = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
			Venue = MatchVenue.Home,
			State = MatchState.Upcoming,
			SelectedFormation = LineupFormation.FourThreeThree,
			SelectedPlayers = [],
			PlayerStats = [],
			Postponements = [],
			Notes = new MatchNotes(),
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	private static Match CreateCompletedMatch(Guid id, Guid seasonId)
	{
		var match = CreateMatch(id, seasonId);
		match.Result = new MatchResult { HomeGoals = 2, AwayGoals = 0 };
		match.IsCompleted = true;
		match.State = MatchState.Won;
		match.SelectedPlayers =
		[
			new SelectedPlayer { PlayerId = PlayerOneId, Area = "pitch", X = 50, Y = 60, PositionIndex = 1 }
		];
		match.PlayerStats =
		[
			new MatchPlayerStats { PlayerId = PlayerOneId, Goals = 1, Minutes = 90 }
		];

		return match;
	}

	private sealed class FakeMatchService : IMatchService
	{
		public List<Match> Matches { get; } = [];

		public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Match>>(Matches.ToList());
		}

		public Task<IReadOnlyList<Match>> GetBySeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Match>>(
				Matches.Where(match => match.SeasonId == seasonId).ToList()
			);
		}

		public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(Matches.FirstOrDefault(match => match.Id == id));
		}

		public Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default)
		{
			match.Id = match.Id == Guid.Empty ? Guid.NewGuid() : match.Id;
			Matches.Add(match);

			return Task.FromResult(match);
		}

		public Task<Match?> UpdateAsync(Match match, CancellationToken cancellationToken = default)
		{
			var index = Matches.FindIndex(existingMatch => existingMatch.Id == match.Id);

			if (index == -1)
			{
				return Task.FromResult<Match?>(null);
			}

			Matches[index] = match;

			return Task.FromResult<Match?>(match);
		}

		public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult(false);
			}

			Matches.Remove(match);

			return Task.FromResult(true);
		}

		public Task<Match?> SetResultAsync(Guid id, MatchResult result, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Result = result;
			match.IsCompleted = true;
			match.State = MatchState.Won;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> ClearResultAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Result = null;
			match.IsCompleted = false;
			match.State = MatchState.Upcoming;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetSelectedPlayersAsync(Guid id, List<SelectedPlayer> selectedPlayers, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedPlayers = selectedPlayers;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetLineupFormationAsync(Guid id, LineupFormation formation, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedFormation = formation;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> ToggleLineupLockedAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.IsLineupLocked = !match.IsLineupLocked;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdateNotesAsync(Guid id, MatchNotes notes, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Notes = notes;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdatePlayerStatsAsync(Guid id, List<MatchPlayerStats> playerStats, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.PlayerStats = playerStats;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> PostponeAsync(Guid id, DateTime newDate, string? reason, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Postponements.Add(new PostponementAudit
			{
				Id = Guid.NewGuid(),
				OldDate = match.Date,
				NewDate = newDate,
				Reason = reason,
				ChangedAt = DateTime.UtcNow
			});
			match.Date = newDate;
			match.State = MatchState.Postponed;
			match.IsCompleted = false;

			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.State = MatchState.Upcoming;
			match.IsCompleted = false;

			return Task.FromResult<Match?>(match);
		}
	}
}
