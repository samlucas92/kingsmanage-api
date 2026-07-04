using KingsManage;
using KingsManage.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/seasons")]
public class SeasonsController : ControllerBase
{
	private readonly ISeasonService seasonService;

	public SeasonsController(ISeasonService seasonService)
	{
		this.seasonService = seasonService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Season>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var seasons = await seasonService.GetAllAsync(cancellationToken);
		return Ok(seasons);
	}

	[HttpGet("active")]
	public async Task<ActionResult<Season>> GetActive(
		CancellationToken cancellationToken
	)
	{
		var season = await seasonService.GetActiveAsync(cancellationToken);

		if (season is null)
		{
			return NotFound();
		}

		return Ok(season);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<Season>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Season", out var seasonId, out var errorResult))
		{
			return errorResult!;
		}

		var season = await seasonService.GetByIdAsync(seasonId, cancellationToken);

		if (season is null)
		{
			return NotFound();
		}

		return Ok(season);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost]
	public async Task<ActionResult<Season>> Create(
		Season season,
		CancellationToken cancellationToken
	)
	{
		var validationError = ValidateSeason(season);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var createdSeason = await seasonService.CreateAsync(
			season,
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdSeason.Id },
			createdSeason
		);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPost("setup")]
	public async Task<ActionResult<SeasonSetupResult>> SetupSeason(
		SeasonSetupRequest request,
		[FromServices] IPlayerService playerService,
		[FromServices] IFinanceService financeService,
		CancellationToken cancellationToken
	)
	{
		var season = new Season
		{
			Name = request.Name,
			StartDate = request.StartDate,
			EndDate = request.EndDate,
			IsActive = request.MakeActive
		};

		var validationError = ValidateSeason(season);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		if (request.SetStartingFinanceAmount && request.StartingFinanceAmount < 0)
		{
			return BadRequest("Starting finance amount must be 0 or above.");
		}

		var existingSeasons = await seasonService.GetAllAsync(cancellationToken);
		var existingSeason = existingSeasons.FirstOrDefault(existing => string.Equals(
			existing.Name.Trim(),
			request.Name.Trim(),
			StringComparison.OrdinalIgnoreCase
		));

		var createdSeason = false;

		if (existingSeason is null)
		{
			season = await seasonService.CreateAsync(season, cancellationToken);
			createdSeason = true;
		}
		else
		{
			season = existingSeason;

			if (request.MakeActive && !season.IsActive)
			{
				season = await seasonService.SetActiveAsync(
					season.Id,
					cancellationToken
				) ?? season;
			}
		}

		var financeChargesCreatedOrUpdated = 0;

		if (request.SetStartingFinanceAmount)
		{
			var players = await playerService.GetAllAsync(cancellationToken);
			var activePlayers = players.Where(player => player.IsActive).ToList();

			foreach (var player in activePlayers)
			{
				await financeService.SetPlayerAmountOwedAsync(
					player.Id,
					season.Id,
					request.StartingFinanceAmount,
					cancellationToken
				);

				financeChargesCreatedOrUpdated++;
			}
		}

		return Ok(new SeasonSetupResult
		{
			Season = season,
			CreatedSeason = createdSeason,
			FinanceChargesCreatedOrUpdated = financeChargesCreatedOrUpdated
		});
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPut("{id}")]
	public async Task<ActionResult<Season>> Update(
		string id,
		Season season,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Season", out var seasonId, out var errorResult))
		{
			return errorResult!;
		}

		var validationError = ValidateSeason(season);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingSeason = await seasonService.GetByIdAsync(
			seasonId,
			cancellationToken
		);

		if (existingSeason is null)
		{
			return NotFound();
		}

		season.Id = seasonId;
		season.CreatedAt = existingSeason.CreatedAt;

		var updatedSeason = await seasonService.UpdateAsync(
			season,
			cancellationToken
		);

		if (updatedSeason is null)
		{
			return NotFound();
		}

		return Ok(updatedSeason);
	}

	[Authorize(Policy = "ClubAdmin")]
	[HttpPatch("{id}/set-active")]
	public async Task<ActionResult<Season>> SetActive(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Season", out var seasonId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedSeason = await seasonService.SetActiveAsync(
			seasonId,
			cancellationToken
		);

		if (updatedSeason is null)
		{
			return NotFound();
		}

		return Ok(updatedSeason);
	}

	private static string? ValidateSeason(Season season)
	{
		if (string.IsNullOrWhiteSpace(season.Name))
		{
			return "Season name is required.";
		}

		if (season.StartDate == default)
		{
			return "Season start date is required.";
		}

		if (season.EndDate == default)
		{
			return "Season end date is required.";
		}

		if (season.EndDate < season.StartDate)
		{
			return "Season end date cannot be before the start date.";
		}

		return null;
	}

	private bool TryParseGuid(
		string id,
		string entityName,
		out Guid parsedId,
		out BadRequestObjectResult? errorResult
	)
	{
		parsedId = Guid.Empty;
		errorResult = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			errorResult = BadRequest($"{entityName} id is required.");
			return false;
		}

		if (!Guid.TryParse(id, out parsedId))
		{
			errorResult = BadRequest($"{entityName} id must be a valid GUID.");
			return false;
		}

		return true;
	}
}
