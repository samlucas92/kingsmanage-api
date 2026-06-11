using KingsManage;
using Microsoft.AspNetCore.Mvc;

namespace KingsManage.Web.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
	private readonly IPlayerService _playerService;

	public PlayersController(IPlayerService playerService)
	{
		_playerService = playerService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<Player>>> GetAll(
		CancellationToken cancellationToken
	)
	{
		var players = await _playerService.GetAllAsync(cancellationToken);

		return Ok(players);
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<Player>> GetById(
		string id,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Player", out var playerId, out var errorResult))
		{
			return errorResult!;
		}

		var player = await _playerService.GetByIdAsync(playerId, cancellationToken);

		if (player is null)
		{
			return NotFound();
		}

		return Ok(player);
	}

	[HttpPost]
	public async Task<ActionResult<Player>> Create(
		Player player,
		CancellationToken cancellationToken
	)
	{
		if (string.IsNullOrWhiteSpace(player.Name))
		{
			return BadRequest("Player name is required.");
		}

		var createdPlayer = await _playerService.CreateAsync(
			player,
			cancellationToken
		);

		return CreatedAtAction(
			nameof(GetById),
			new { id = createdPlayer.Id },
			createdPlayer
		);
	}

	[HttpPut("{id}")]
	public async Task<ActionResult<Player>> Update(
		string id,
		Player player,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Player", out var playerId, out var errorResult))
		{
			return errorResult!;
		}

		if (string.IsNullOrWhiteSpace(player.Name))
		{
			return BadRequest("Player name is required.");
		}

		var existingPlayer = await _playerService.GetByIdAsync(
			playerId,
			cancellationToken
		);

		if (existingPlayer is null)
		{
			return NotFound();
		}

		player.Id = playerId;
		player.CreatedAt = existingPlayer.CreatedAt;

		var updatedPlayer = await _playerService.UpdateAsync(
			player,
			cancellationToken
		);

		if (updatedPlayer is null)
		{
			return NotFound();
		}

		return Ok(updatedPlayer);
	}

	[HttpPatch("{id}/active")]
	public async Task<ActionResult<Player>> SetActive(
		string id,
		[FromBody] bool isActive,
		CancellationToken cancellationToken
	)
	{
		if (!TryParseGuid(id, "Player", out var playerId, out var errorResult))
		{
			return errorResult!;
		}

		var updatedPlayer = await _playerService.SetActiveAsync(
			playerId,
			isActive,
			cancellationToken
		);

		if (updatedPlayer is null)
		{
			return NotFound();
		}

		return Ok(updatedPlayer);
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
