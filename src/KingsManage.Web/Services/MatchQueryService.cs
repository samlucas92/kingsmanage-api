using KingsManage.Web.Models;

namespace KingsManage.Web.Services;

public sealed class MatchQueryService : IMatchQueryService
{
	private readonly IMatchService matchService;

	public MatchQueryService(IMatchService matchService)
	{
		this.matchService = matchService;
	}

	public async Task<List<MatchViewModel>> GetMatchesAsync(
		Guid? seasonId,
		CancellationToken cancellationToken = default)
	{
		var matches = await GetMatchesForSeasonAsync(
			seasonId,
			cancellationToken);

		return matches
			.Select(MatchViewModel.FromMatch)
			.ToList();
	}

	public async Task<List<PlayerMatchViewModel>> GetPlayerMatchesAsync(
		Guid playerId,
		Guid? seasonId,
		CancellationToken cancellationToken = default)
	{
		var matches = await GetMatchesForSeasonAsync(
			seasonId,
			cancellationToken);

		return matches
			.Where(match =>
				match.IsCompleted &&
				match.SelectedPlayers.Any(selectedPlayer => selectedPlayer.PlayerId == playerId))
			.OrderByDescending(match => match.Date)
			.Select(match => PlayerMatchViewModel.FromMatch(match, playerId))
			.ToList();
	}

	public Task<Match?> GetByIdAsync(
		Guid matchId,
		CancellationToken cancellationToken = default)
	{
		return matchService.GetByIdAsync(
			matchId,
			cancellationToken);
	}

	private async Task<IReadOnlyList<Match>> GetMatchesForSeasonAsync(
		Guid? seasonId,
		CancellationToken cancellationToken)
	{
		if (seasonId is not null)
		{
			return await matchService.GetBySeasonAsync(
				seasonId.Value,
				cancellationToken);
		}

		return await matchService.GetAllAsync(cancellationToken);
	}
}
