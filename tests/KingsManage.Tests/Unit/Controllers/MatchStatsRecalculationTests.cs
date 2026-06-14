using KingsManage.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using KingsManage;

namespace KingsManage.Tests.Unit.Controllers;

public class MatchStatsRecalculationTests
{
	private static readonly Guid MatchOneId = Guid.Parse("33333333-3333-3333-3333-333333333333");
	private static readonly Guid SeasonOneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
	private static readonly Guid SeasonTwoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
	private static readonly Guid PlayerOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid PlayerTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	[Test]
	public async Task SetResult_WhenMatchHasSeason_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.SetResult(
			MatchOneId.ToString(),
			new MatchResult { HomeGoals = 3, AwayGoals = 1 },
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task UpdatePlayerStats_WhenCompletedMatchChanges_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		var match = CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent");
		match.SelectedPlayers.Add(new SelectedPlayer
		{
			PlayerId = PlayerOneId,
			Area = "pitch",
			X = 50,
			Y = 50,
			PositionIndex = 1
		});
		match.PlayerStats.Add(new MatchPlayerStats
		{
			PlayerId = PlayerOneId,
			Goals = 1
		});
		matchService.Matches.Add(match);

		var result = await controller.UpdatePlayerStats(
			MatchOneId.ToString(),
			[
				new MatchPlayerStats
				{
					PlayerId = PlayerOneId,
					Goals = 2,
					Assists = 1,
					Minutes = 90,
					IsMOTM = true
				}
			],
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task SetLineup_WhenCompletedMatchLineupChanges_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		var match = CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent");
		match.SelectedPlayers.Add(new SelectedPlayer
		{
			PlayerId = PlayerOneId,
			Area = "pitch",
			X = 50,
			Y = 50,
			PositionIndex = 1
		});
		matchService.Matches.Add(match);

		var result = await controller.SetLineup(
			MatchOneId.ToString(),
			[
				new SelectedPlayer
				{
					PlayerId = PlayerTwoId,
					Area = "bench",
					X = 0,
					Y = 0
				}
			],
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task ClearResult_WhenCompletedMatchIsMadeUpcoming_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.ClearResult(MatchOneId.ToString(), CancellationToken.None);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task Delete_WhenCompletedMatchIsDeleted_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.Delete(MatchOneId.ToString(), CancellationToken.None);

		Assert.That(result, Is.TypeOf<NoContentResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task Update_WhenMatchMovesSeason_ShouldRecalculateOldAndNewSeasons()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.Update(
			MatchOneId.ToString(),
			CreateCompletedMatch(MatchOneId, SeasonTwoId, "Updated Opponent"),
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId, SeasonTwoId }));
	}

	[Test]
	public async Task Postpone_WhenCompletedMatchIsPostponed_ShouldRecalculateThatSeason()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.Postpone(
			MatchOneId.ToString(),
			new PostponeMatchModel
			{
				NewDate = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
				Reason = "Bad weather"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.EqualTo(new[] { SeasonOneId }));
	}

	[Test]
	public async Task UpdateNotes_WhenNotesChange_ShouldNotRecalculateStats()
	{
		var matchService = new FakeMatchService();
		var statsService = new FakeStatsService();
		var controller = new MatchesController(matchService, statsService);

		matchService.Matches.Add(CreateCompletedMatch(MatchOneId, SeasonOneId, "Test Opponent"));

		var result = await controller.UpdateNotes(
			MatchOneId.ToString(),
			new MatchNotes
			{
				Availability = "Availability",
				Tactical = "Tactical",
				Injuries = "Injuries",
				General = "General"
			},
			CancellationToken.None
		);

		Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
		Assert.That(statsService.RecalculatedSeasonIds, Is.Empty);
	}

	private static Match CreateMatch(
		Guid id,
		Guid? seasonId,
		string opponent,
		DateTime? date = null
	)
	{
		return new Match
		{
			Id = id,
			SeasonId = seasonId,
			Team = ClubTeam.First,
			Opponent = opponent,
			Date = date ?? new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
			Venue = MatchVenue.Home,
			State = MatchState.Upcoming,
			Result = null,
			IsCompleted = false,
			IsLineupLocked = false,
			SelectedFormation = LineupFormation.FourThreeThree,
			Notes = new MatchNotes(),
			Postponements = [],
			SelectedPlayers = [],
			PlayerStats = [],
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	private static Match CreateCompletedMatch(
		Guid id,
		Guid? seasonId,
		string opponent
	)
	{
		var match = CreateMatch(id, seasonId, opponent);
		match.State = MatchState.Won;
		match.IsCompleted = true;
		match.Result = new MatchResult
		{
			HomeGoals = 2,
			AwayGoals = 0
		};

		return match;
	}

	private sealed class FakeMatchService : IMatchService
	{
		public List<Match> Matches { get; } = [];

		public Task<IReadOnlyList<Match>> GetAllAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<Match>>(
				Matches.OrderBy(match => match.Date).ToList()
			);
		}

		public Task<IReadOnlyList<Match>> GetBySeasonAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<Match>>(
				Matches
					.Where(match => match.SeasonId == seasonId)
					.OrderBy(match => match.Date)
					.ToList()
			);
		}

		public Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(
				Matches.FirstOrDefault(match => match.Id == id)
			);
		}

		public Task<Match> CreateAsync(Match match, CancellationToken cancellationToken = default)
		{
			match.Id = match.Id == Guid.Empty ? Guid.NewGuid() : match.Id;
			match.Opponent = match.Opponent.Trim();
			match.State = MatchState.Upcoming;
			match.IsCompleted = false;
			match.Result = null;
			match.Notes ??= new MatchNotes();
			match.Postponements ??= [];
			match.SelectedPlayers ??= [];
			match.PlayerStats ??= [];
			match.CreatedAt = DateTime.UtcNow;
			match.UpdatedAt = DateTime.UtcNow;
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

			match.Opponent = match.Opponent.Trim();
			match.UpdatedAt = DateTime.UtcNow;
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

		public Task<Match?> SetResultAsync(
			Guid id,
			MatchResult result,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Result = result;
			match.IsCompleted = true;
			match.State = GetResultState(match.Venue, result);
			match.UpdatedAt = DateTime.UtcNow;
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
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetSelectedPlayersAsync(
			Guid id,
			List<SelectedPlayer> selectedPlayers,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedPlayers = selectedPlayers;
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> SetLineupFormationAsync(
			Guid id,
			LineupFormation formation,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.SelectedFormation = formation;
			match.UpdatedAt = DateTime.UtcNow;
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
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdateNotesAsync(
			Guid id,
			MatchNotes notes,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.Notes = notes;
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> UpdatePlayerStatsAsync(
			Guid id,
			List<MatchPlayerStats> playerStats,
			CancellationToken cancellationToken = default
		)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			match.PlayerStats = playerStats;
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> PostponeAsync(
			Guid id,
			DateTime newDate,
			string? reason,
			CancellationToken cancellationToken = default
		)
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
				Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
				ChangedAt = DateTime.UtcNow
			});
			match.Date = newDate;
			match.State = MatchState.Postponed;
			match.IsCompleted = false;
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		public Task<Match?> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
		{
			var match = Matches.FirstOrDefault(currentMatch => currentMatch.Id == id);

			if (match is null)
			{
				return Task.FromResult<Match?>(null);
			}

			var lastPostponement = match.Postponements.LastOrDefault();

			if (lastPostponement is not null)
			{
				match.Date = lastPostponement.OldDate;
			}

			match.State = MatchState.Upcoming;
			match.IsCompleted = false;
			match.UpdatedAt = DateTime.UtcNow;
			return Task.FromResult<Match?>(match);
		}

		private static MatchState GetResultState(MatchVenue venue, MatchResult result)
		{
			if (result.HomeGoals == result.AwayGoals)
			{
				return MatchState.Draw;
			}

			var homeWin = result.HomeGoals > result.AwayGoals;

			return venue switch
			{
				MatchVenue.Home => homeWin ? MatchState.Won : MatchState.Lost,
				MatchVenue.Away => homeWin ? MatchState.Lost : MatchState.Won,
				_ => MatchState.Upcoming
			};
		}
	}

	private sealed class FakeStatsService : IStatsService
	{
		public List<Guid> RecalculatedSeasonIds { get; } = [];

		public Task<IReadOnlyList<PlayerSeasonStats>> GetSeasonStatsAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<PlayerSeasonStats>>([]);
		}

		public Task<IReadOnlyList<PlayerSeasonStats>> GetAllSeasonStatsAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<PlayerSeasonStats>>([]);
		}

		public Task<IReadOnlyList<PlayerSeasonStats>> GetPlayerSeasonStatsAsync(
			Guid playerId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<PlayerSeasonStats>>([]);
		}

		public Task<IReadOnlyList<PlayerHistoricalStats>> GetHistoricalStatsAsync(
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<IReadOnlyList<PlayerHistoricalStats>>([]);
		}

		public Task<PlayerHistoricalStats?> GetHistoricalStatsByPlayerIdAsync(
			Guid playerId,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult<PlayerHistoricalStats?>(null);
		}

		public Task<PlayerHistoricalStats> UpsertHistoricalStatsAsync(
			PlayerHistoricalStats stats,
			CancellationToken cancellationToken = default
		)
		{
			return Task.FromResult(stats);
		}

		public Task RecalculateSeasonStatsAsync(
			Guid seasonId,
			CancellationToken cancellationToken = default
		)
		{
			RecalculatedSeasonIds.Add(seasonId);
			return Task.CompletedTask;
		}
	}
}
