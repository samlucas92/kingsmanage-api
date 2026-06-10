using KingsManage;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/seasons")]
public class SeasonsController : ControllerBase
{
	private readonly ISeasonService _seasonService;

	public SeasonsController(ISeasonService seasonService)
	{
		_seasonService = seasonService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Season>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var seasons = await _seasonService.GetAllAsync(cancellationToken);

		return Ok(seasons);
	}

	[HttpGet("active")]
	public async Task<ActionResult<Season>> GetActive(
		CancellationToken cancellationToken
	)
	{
		var season = await _seasonService.GetActiveAsync(cancellationToken);

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
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Season id is required.");
		}

		var season = await _seasonService.GetByIdAsync(id, cancellationToken);

		if (season is null)
		{
			return NotFound();
		}

		return Ok(season);
	}

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

		var createdSeason = await _seasonService.CreateAsync(
			season,
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdSeason.Id },
			createdSeason
		);
	}

	[HttpPut("{id}")]
	public async Task<ActionResult<Season>> Update(
		string id,
		Season season,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Season id is required.");
		}

		var validationError = ValidateSeason(season);

		if (validationError is not null)
		{
			return BadRequest(validationError);
		}

		var existingSeason = await _seasonService.GetByIdAsync(
			id,
			cancellationToken
		);

		if (existingSeason is null)
		{
			return NotFound();
		}

		season.Id = id;
		season.CreatedAt = existingSeason.CreatedAt;

		var updatedSeason = await _seasonService.UpdateAsync(
			season,
			cancellationToken
		);

		if (updatedSeason is null)
		{
			return NotFound();
		}

		return Ok(updatedSeason);
	}

	[HttpPatch("{id}/set-active")]
	public async Task<ActionResult<Season>> SetActive(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Season id is required.");
		}

		var updatedSeason = await _seasonService.SetActiveAsync(
			id,
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
}