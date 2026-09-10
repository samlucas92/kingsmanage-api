namespace KingsManage;

public interface ILeagueRuleService
{
	Task<IReadOnlyList<LeagueRule>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<LeagueRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<LeagueRule> CreateAsync(LeagueRule rule, CancellationToken cancellationToken = default);
	Task<LeagueRule?> UpdateAsync(LeagueRule rule, CancellationToken cancellationToken = default);
}
