using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class LeagueRuleService : ILeagueRuleService
{
	private readonly IMongoCollection<LeagueRule> rules;
	private readonly TenantMongoScope tenant;

	public LeagueRuleService(MongoContext context, TenantMongoScope tenant)
	{
		rules = context.Database.GetCollection<LeagueRule>("leagueRules");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<LeagueRule>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await rules.Find(tenant.Filter<LeagueRule>()).SortBy(rule => rule.Name).ToListAsync(cancellationToken);

	public async Task<LeagueRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await rules.Find(tenant.Filter<LeagueRule>(rule => rule.Id == id)).FirstOrDefaultAsync(cancellationToken);

	public async Task<LeagueRule> CreateAsync(LeagueRule rule, CancellationToken cancellationToken = default)
	{
		var now = DateTime.UtcNow;
		rule.Id = Guid.NewGuid();
		rule.CreatedAt = now;
		rule.UpdatedAt = now;
		Normalise(rule);
		tenant.Assign(rule);
		await rules.InsertOneAsync(rule, cancellationToken: cancellationToken);
		return rule;
	}

	public async Task<LeagueRule?> UpdateAsync(LeagueRule rule, CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(rule.Id, cancellationToken);
		if (existing is null) return null;
		rule.CreatedAt = existing.CreatedAt;
		rule.UpdatedAt = DateTime.UtcNow;
		Normalise(rule);
		tenant.Assign(rule);
		var result = await rules.ReplaceOneAsync(
			tenant.Filter<LeagueRule>(item => item.Id == rule.Id), rule, cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : rule;
	}

	private static void Normalise(LeagueRule rule)
	{
		rule.Name = (rule.Name ?? string.Empty).Trim();
		rule.HigherTeamCompetitions = NormaliseCompetitions(rule.HigherTeamCompetitions);
		rule.RestrictedTeamCompetitions = NormaliseCompetitions(rule.RestrictedTeamCompetitions);
		if (rule.RuleType == LeagueRuleType.CupTied) rule.MaxPlayers = null;
	}

	private static List<string> NormaliseCompetitions(IEnumerable<string>? values) =>
		(values ?? []).Select(value => value.Trim()).Where(value => value.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
