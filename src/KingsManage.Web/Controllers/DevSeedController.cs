using System.Security.Cryptography;
using System.Text;
using KingsManage;
using KingsManage.Mongo;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/dev-seed")]
public class DevSeedController : ControllerBase
{
	private readonly IHostEnvironment _environment;
	private readonly IMongoCollection<Player> _players;
	private readonly IMongoCollection<Season> _seasons;
	private readonly IMongoCollection<Match> _matches;

	public DevSeedController(
		IHostEnvironment environment,
		MongoContext context
	)
	{
		_environment = environment;
		_players = context.Database.GetCollection<Player>("players");
		_seasons = context.Database.GetCollection<Season>("seasons");
		_matches = context.Database.GetCollection<Match>("matches");
	}

	[HttpPost("all")]
	public async Task<ActionResult<SeedImportResult>> ImportAll(
		[FromBody] SeedImportRequest request,
		CancellationToken cancellationToken
	)
	{
		if (!_environment.IsDevelopment())
		{
			return NotFound();
		}

		var result = await ImportAllInternal(
			request,
			clearExisting: false,
			cancellationToken
		);

		return Ok(result);
	}

	[HttpPost("replace-all")]
	public async Task<ActionResult<SeedImportResult>> ReplaceAll(
		[FromBody] SeedImportRequest request,
		CancellationToken cancellationToken
	)
	{
		if (!_environment.IsDevelopment())
		{
			return NotFound();
		}

		var result = await ImportAllInternal(
			request,
			clearExisting: true,
			cancellationToken
		);

		return Ok(result);
	}

	private async Task<SeedImportResult> ImportAllInternal(
		SeedImportRequest request,
		bool clearExisting,
		CancellationToken cancellationToken
	)
	{
		if (clearExisting)
		{
			await _players.DeleteManyAsync(_ => true, cancellationToken);
			await _matches.DeleteManyAsync(_ => true, cancellationToken);
			await _seasons.DeleteManyAsync(_ => true, cancellationToken);
		}

		var result = new SeedImportResult();
		var seasonIdMap = new Dictionary<string, Guid>();
		var playerIdMap = new Dictionary<string, Guid>();

		if (request.Seasons is not null)
		{
			var seasonResult = await ImportSeasonsInternal(
				request.Seasons,
				seasonIdMap,
				cancellationToken
			);

			result.SeasonsCreated = seasonResult.Created;
			result.SeasonsUpdated = seasonResult.Updated;
		}

		if (request.Players is not null)
		{
			var playerResult = await ImportPlayersInternal(
				request.Players,
				playerIdMap,
				cancellationToken
			);

			result.PlayersCreated = playerResult.Created;
			result.PlayersUpdated = playerResult.Updated;
		}

		if (request.Matches is not null)
		{
			var matchResult = await ImportMatchesInternal(
				request.Matches,
				seasonIdMap,
				playerIdMap,
				cancellationToken
			);

			result.MatchesCreated = matchResult.Created;
			result.MatchesUpdated = matchResult.Updated;
		}

		return result;
	}

	private async Task<SeedImportCounter> ImportPlayersInternal(
		List<SeedPlayer> seedPlayers,
		Dictionary<string, Guid> playerIdMap,
		CancellationToken cancellationToken
	)
	{
		var counter = new SeedImportCounter();

		foreach (var seedPlayer in seedPlayers)
		{
			if (
				string.IsNullOrWhiteSpace(seedPlayer.Id) ||
				string.IsNullOrWhiteSpace(seedPlayer.Name)
			)
			{
				continue;
			}

			var playerId = CreateStableGuid("player", seedPlayer.Id);
			playerIdMap[seedPlayer.Id] = playerId;

			var player = new Player
			{
				Id = playerId,
				Name = seedPlayer.Name.Trim(),
				Positions = seedPlayer.Positions ?? [],
				Appearances = seedPlayer.Appearances,
				Goals = seedPlayer.Goals,
				Number = seedPlayer.Number,
				IsActive = seedPlayer.IsActive,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			var result = await _players.ReplaceOneAsync(
				existingPlayer => existingPlayer.Id == player.Id,
				player,
				new ReplaceOptions
				{
					IsUpsert = true
				},
				cancellationToken
			);

			if (result.MatchedCount == 0)
			{
				counter.Created++;
				continue;
			}

			counter.Updated++;
		}

		return counter;
	}

	private async Task<SeedImportCounter> ImportSeasonsInternal(
		List<SeedSeason> seedSeasons,
		Dictionary<string, Guid> seasonIdMap,
		CancellationToken cancellationToken
	)
	{
		var counter = new SeedImportCounter();

		foreach (var seedSeason in seedSeasons)
		{
			if (
				string.IsNullOrWhiteSpace(seedSeason.Id) ||
				string.IsNullOrWhiteSpace(seedSeason.Name)
			)
			{
				continue;
			}

			var seasonId = CreateStableGuid("season", seedSeason.Id);
			seasonIdMap[seedSeason.Id] = seasonId;

			var season = new Season
			{
				Id = seasonId,
				Name = seedSeason.Name.Trim(),
				StartDate = seedSeason.StartDate,
				EndDate = seedSeason.EndDate,
				IsActive = seedSeason.IsActive,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			var result = await _seasons.ReplaceOneAsync(
				existingSeason => existingSeason.Id == season.Id,
				season,
				new ReplaceOptions
				{
					IsUpsert = true
				},
				cancellationToken
			);

			if (result.MatchedCount == 0)
			{
				counter.Created++;
				continue;
			}

			counter.Updated++;
		}

		return counter;
	}

	private async Task<SeedImportCounter> ImportMatchesInternal(
		List<SeedMatch> seedMatches,
		Dictionary<string, Guid> seasonIdMap,
		Dictionary<string, Guid> playerIdMap,
		CancellationToken cancellationToken
	)
	{
		var counter = new SeedImportCounter();

		foreach (var seedMatch in seedMatches)
		{
			if (
				string.IsNullOrWhiteSpace(seedMatch.Id) ||
				string.IsNullOrWhiteSpace(seedMatch.Opponent)
			)
			{
				continue;
			}

			var venue = MapVenue(seedMatch.Venue);
			var result = seedMatch.Result;
			var isCompleted = IsCompleted(seedMatch);
			var state = GetSeedMatchState(
				venue,
				result,
				isCompleted,
				seedMatch.State
			);

			var match = new Match
			{
				Id = CreateStableGuid("match", seedMatch.Id),
				SeasonId = GetSeasonId(seedMatch.SeasonId, seasonIdMap),
				Team = MapTeam(seedMatch.Team),
				Opponent = seedMatch.Opponent.Trim(),
				Date = seedMatch.Date,
				Venue = venue,
				State = state,
				Result = result,
				IsCompleted = isCompleted,
				IsLineupLocked = seedMatch.IsLineupLocked,
				SelectedFormation = MapFormation(seedMatch.SelectedFormation),
				Notes = seedMatch.Notes ?? new MatchNotes(),
				Postponements = MapPostponements(seedMatch),
				SelectedPlayers = MapSelectedPlayers(
					seedMatch.SelectedPlayers,
					playerIdMap
				),
				PlayerStats = MapPlayerStats(
					seedMatch.PlayerStats,
					playerIdMap
				),
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			var replaceResult = await _matches.ReplaceOneAsync(
				existingMatch => existingMatch.Id == match.Id,
				match,
				new ReplaceOptions
				{
					IsUpsert = true
				},
				cancellationToken
			);

			if (replaceResult.MatchedCount == 0)
			{
				counter.Created++;
				continue;
			}

			counter.Updated++;
		}

		return counter;
	}

	private static Guid? GetSeasonId(
		string? seasonId,
		Dictionary<string, Guid> seasonIdMap
	)
	{
		if (string.IsNullOrWhiteSpace(seasonId))
		{
			return seasonIdMap.TryGetValue("2025-2026", out var defaultSeasonId)
				? defaultSeasonId
				: CreateStableGuid("season", "2025-2026");
		}

		if (seasonIdMap.TryGetValue(seasonId, out var mappedSeasonId))
		{
			return mappedSeasonId;
		}

		return CreateStableGuid("season", seasonId);
	}

	private static List<SelectedPlayer> MapSelectedPlayers(
		List<SeedSelectedPlayer>? selectedPlayers,
		Dictionary<string, Guid> playerIdMap
	)
	{
		if (selectedPlayers is null)
		{
			return [];
		}

		return selectedPlayers
			.Where(selectedPlayer => !string.IsNullOrWhiteSpace(selectedPlayer.PlayerId))
			.Select(selectedPlayer => new SelectedPlayer
			{
				PlayerId = GetPlayerId(selectedPlayer.PlayerId, playerIdMap),
				X = selectedPlayer.X,
				Y = selectedPlayer.Y,
				Area = string.IsNullOrWhiteSpace(selectedPlayer.Area)
					? "pitch"
					: selectedPlayer.Area,
				PositionIndex = selectedPlayer.PositionIndex
			})
			.ToList();
	}

	private static List<MatchPlayerStats> MapPlayerStats(
		List<SeedMatchPlayerStat>? playerStats,
		Dictionary<string, Guid> playerIdMap
	)
	{
		if (playerStats is null)
		{
			return [];
		}

		return playerStats
			.Where(playerStat => !string.IsNullOrWhiteSpace(playerStat.PlayerId))
			.Select(playerStat => new MatchPlayerStats
			{
				PlayerId = GetPlayerId(playerStat.PlayerId, playerIdMap),
				Goals = playerStat.Goals,
				Assists = playerStat.Assists,
				YellowCards = playerStat.YellowCards,
				RedCards = playerStat.RedCards,
				Minutes = playerStat.Minutes,
				IsMOTM = playerStat.IsMOTM,
				Note = playerStat.Note ?? ""
			})
			.ToList();
	}

	private static Guid GetPlayerId(
		string playerId,
		Dictionary<string, Guid> playerIdMap
	)
	{
		if (playerIdMap.TryGetValue(playerId, out var mappedPlayerId))
		{
			return mappedPlayerId;
		}

		return CreateStableGuid("player", playerId);
	}

	private static List<PostponementAudit> MapPostponements(SeedMatch seedMatch)
	{
		if (seedMatch.Postponements is null)
		{
			return [];
		}

		return seedMatch.Postponements
			.Select(postponement => new PostponementAudit
			{
				Id = CreateStableGuid(
					"postponement",
					$"{seedMatch.Id}:{postponement.Id}:{postponement.OldDate:O}:{postponement.NewDate:O}"
				),
				OldDate = postponement.OldDate,
				NewDate = postponement.NewDate,
				Reason = string.IsNullOrWhiteSpace(postponement.Reason)
					? null
					: postponement.Reason.Trim(),
				ChangedAt = postponement.ChangedAt == default
					? DateTime.UtcNow
					: postponement.ChangedAt
			})
			.ToList();
	}

	private static bool IsCompleted(SeedMatch seedMatch)
	{
		if (seedMatch.Result is not null)
		{
			return true;
		}

		if (seedMatch.IsCompleted)
		{
			return true;
		}

		return seedMatch.State.Trim().ToLowerInvariant() switch
		{
			"won" => true,
			"lost" => true,
			"draw" => true,
			_ => false
		};
	}

	private static MatchState GetSeedMatchState(
		MatchVenue venue,
		MatchResult? result,
		bool isCompleted,
		string? seedState
	)
	{
		if (isCompleted && result is not null)
		{
			return GetResultState(venue, result);
		}

		return MapState(seedState);
	}

	private static ClubTeam MapTeam(string? team)
	{
		return team?.Trim().ToLowerInvariant() switch
		{
			"second" => ClubTeam.Second,
			"2nd" => ClubTeam.Second,
			_ => ClubTeam.First
		};
	}

	private static MatchVenue MapVenue(string? venue)
	{
		return venue?.Trim().ToLowerInvariant() switch
		{
			"away" => MatchVenue.Away,
			_ => MatchVenue.Home
		};
	}

	private static MatchState MapState(string? state)
	{
		return state?.Trim().ToLowerInvariant() switch
		{
			"won" => MatchState.Won,
			"lost" => MatchState.Lost,
			"draw" => MatchState.Draw,
			"postponed" => MatchState.Postponed,
			_ => MatchState.Upcoming
		};
	}

	private static LineupFormation MapFormation(string? formation)
	{
		return formation?.Trim() switch
		{
			"4-4-2" => LineupFormation.FourFourTwo,
			"FourFourTwo" => LineupFormation.FourFourTwo,
			"3-5-2" => LineupFormation.ThreeFiveTwo,
			"ThreeFiveTwo" => LineupFormation.ThreeFiveTwo,
			"4-2-3-1" => LineupFormation.FourTwoThreeOne,
			"FourTwoThreeOne" => LineupFormation.FourTwoThreeOne,
			"4-3-3" => LineupFormation.FourThreeThree,
			"FourThreeThree" => LineupFormation.FourThreeThree,
			_ => LineupFormation.FourThreeThree
		};
	}

	private static MatchState GetResultState(
		MatchVenue venue,
		MatchResult result
	)
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

	private static Guid CreateStableGuid(string scope, string value)
	{
		if (Guid.TryParse(value, out var existingGuid))
		{
			return existingGuid;
		}

		var source = $"{scope}:{value.Trim().ToLowerInvariant()}";
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
		var guidBytes = hash.Take(16).ToArray();

		return new Guid(guidBytes);
	}

	public class SeedImportRequest
	{
		public List<SeedPlayer>? Players { get; set; }
		public List<SeedSeason>? Seasons { get; set; }
		public List<SeedMatch>? Matches { get; set; }
	}

	public class SeedImportResult
	{
		public int PlayersCreated { get; set; }
		public int PlayersUpdated { get; set; }
		public int SeasonsCreated { get; set; }
		public int SeasonsUpdated { get; set; }
		public int MatchesCreated { get; set; }
		public int MatchesUpdated { get; set; }
	}

	private class SeedImportCounter
	{
		public int Created { get; set; }
		public int Updated { get; set; }
	}

	public class SeedPlayer
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public List<string> Positions { get; set; } = [];
		public int Appearances { get; set; }
		public int Goals { get; set; }
		public int Number { get; set; }
		public bool IsActive { get; set; } = true;
	}

	public class SeedSeason
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public bool IsActive { get; set; }
	}

	public class SeedMatch
	{
		public string Id { get; set; } = "";
		public string? SeasonId { get; set; }
		public string Team { get; set; } = "first";
		public string Opponent { get; set; } = "";
		public DateTime Date { get; set; }
		public string Venue { get; set; } = "home";
		public string State { get; set; } = "upcoming";
		public MatchResult? Result { get; set; }
		public bool IsCompleted { get; set; }
		public bool IsLineupLocked { get; set; }
		public string SelectedFormation { get; set; } = "4-3-3";
		public MatchNotes? Notes { get; set; }
		public List<SeedPostponementAudit>? Postponements { get; set; }
		public List<SeedSelectedPlayer>? SelectedPlayers { get; set; }
		public List<SeedMatchPlayerStat>? PlayerStats { get; set; }
	}

	public class SeedPostponementAudit
	{
		public string Id { get; set; } = "";
		public DateTime OldDate { get; set; }
		public DateTime NewDate { get; set; }
		public string? Reason { get; set; }
		public DateTime ChangedAt { get; set; }
	}

	public class SeedSelectedPlayer
	{
		public string PlayerId { get; set; } = "";
		public decimal X { get; set; }
		public decimal Y { get; set; }
		public string Area { get; set; } = "pitch";
		public int? PositionIndex { get; set; }
	}

	public class SeedMatchPlayerStat
	{
		public string PlayerId { get; set; } = "";
		public int Goals { get; set; }
		public int Assists { get; set; }
		public int YellowCards { get; set; }
		public int RedCards { get; set; }
		public int Minutes { get; set; }
		public bool IsMOTM { get; set; }
		public string? Note { get; set; }
	}
}
