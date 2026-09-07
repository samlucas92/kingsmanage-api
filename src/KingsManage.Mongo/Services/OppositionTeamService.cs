using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class OppositionTeamService : IOppositionTeamService
{
	private readonly IMongoCollection<OppositionTeam> teams;
	private readonly TenantMongoScope tenant;

	public OppositionTeamService(MongoContext context, TenantMongoScope tenant)
	{
		teams = context.Database.GetCollection<OppositionTeam>("oppositionTeams");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<OppositionTeam>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await teams.Find(tenant.Filter<OppositionTeam>()).SortBy(team => team.Name).ToListAsync(cancellationToken);

	public async Task<OppositionTeam?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await teams.Find(tenant.Filter<OppositionTeam>(team => team.Id == id)).FirstOrDefaultAsync(cancellationToken);

	public async Task<OppositionTeam> CreateAsync(OppositionTeam team, CancellationToken cancellationToken = default)
	{
		Normalise(team);
		if (await NameExistsAsync(team.NormalizedName, null, cancellationToken))
		{
			throw new ArgumentException("An opposition team with this name already exists.");
		}

		var now = DateTime.UtcNow;
		team.Id = Guid.NewGuid();
		team.BadgeFileId = null;
		team.CreatedAt = now;
		team.UpdatedAt = now;
		tenant.Assign(team);
		await teams.InsertOneAsync(team, cancellationToken: cancellationToken);
		return team;
	}

	public async Task<OppositionTeam?> UpdateAsync(OppositionTeam team, CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(team.Id, cancellationToken);
		if (existing is null) return null;

		Normalise(team);
		if (await NameExistsAsync(team.NormalizedName, team.Id, cancellationToken))
		{
			throw new ArgumentException("An opposition team with this name already exists.");
		}

		team.BadgeFileId = existing.BadgeFileId;
		team.CreatedAt = existing.CreatedAt;
		team.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(team);
		var result = await teams.ReplaceOneAsync(
			tenant.Filter<OppositionTeam>(item => item.Id == team.Id),
			team,
			cancellationToken: cancellationToken);
		return result.MatchedCount == 0 ? null : team;
	}

	public async Task<OppositionTeam?> SetBadgeFileAsync(Guid id, Guid? badgeFileId, CancellationToken cancellationToken = default)
	{
		return await teams.FindOneAndUpdateAsync(
			tenant.Filter<OppositionTeam>(team => team.Id == id),
			Builders<OppositionTeam>.Update
				.Set(team => team.BadgeFileId, badgeFileId)
				.Set(team => team.UpdatedAt, DateTime.UtcNow),
			new FindOneAndUpdateOptions<OppositionTeam> { ReturnDocument = ReturnDocument.After },
			cancellationToken);
	}

	private async Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken)
	{
		var filter = tenant.Filter<OppositionTeam>(team => team.NormalizedName == normalizedName);
		if (exceptId.HasValue)
		{
			filter &= Builders<OppositionTeam>.Filter.Ne(team => team.Id, exceptId.Value);
		}
		return await teams.Find(filter).AnyAsync(cancellationToken);
	}

	private static void Normalise(OppositionTeam team)
	{
		team.Name = (team.Name ?? string.Empty).Trim();
		team.Location = (team.Location ?? string.Empty).Trim();
		team.NormalizedName = team.Name.ToUpperInvariant();
	}
}
