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
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Player id is required.");
		}

		var player = await _playerService.GetByIdAsync(id, cancellationToken);

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
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Player id is required.");
		}

		if (string.IsNullOrWhiteSpace(player.Name))
		{
			return BadRequest("Player name is required.");
		}

		var existingPlayer = await _playerService.GetByIdAsync(
			id,
			cancellationToken
		);

		if (existingPlayer is null)
		{
			return NotFound();
		}

		player.Id = id;
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
		if (string.IsNullOrWhiteSpace(id))
		{
			return BadRequest("Player id is required.");
		}

		var updatedPlayer = await _playerService.SetActiveAsync(
			id,
			isActive,
			cancellationToken
		);

		if (updatedPlayer is null)
		{
			return NotFound();
		}

		return Ok(updatedPlayer);
	}
}