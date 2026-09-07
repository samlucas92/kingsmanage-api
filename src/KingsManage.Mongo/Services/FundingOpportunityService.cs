using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public sealed class FundingOpportunityService : IFundingOpportunityService
{
	private readonly IMongoCollection<FundingOpportunity> opportunities;
	private readonly TenantMongoScope tenant;

	public FundingOpportunityService(MongoContext context, TenantMongoScope tenant)
	{
		opportunities = context.Database.GetCollection<FundingOpportunity>("fundingOpportunities");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<FundingOpportunity>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await opportunities
			.Find(tenant.Filter<FundingOpportunity>())
			.SortBy(opportunity => opportunity.EndDate)
			.ThenBy(opportunity => opportunity.Name)
			.ToListAsync(cancellationToken);
	}

	public async Task<FundingOpportunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await opportunities
			.Find(tenant.Filter<FundingOpportunity>(opportunity => opportunity.Id == id))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<FundingOpportunity> CreateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default)
	{
		PrepareNew(opportunity);
		if (await DuplicateExistsAsync(opportunity.DuplicateKey, null, cancellationToken))
		{
			throw new ArgumentException("This funding opportunity already exists.");
		}
		await opportunities.InsertOneAsync(opportunity, cancellationToken: cancellationToken);
		return opportunity;
	}

	public async Task<IReadOnlyList<FundingOpportunity>> CreateManyAsync(
		IEnumerable<FundingOpportunity> importedOpportunities,
		CancellationToken cancellationToken = default)
	{
		var records = importedOpportunities.ToList();
		foreach (var opportunity in records)
		{
			PrepareNew(opportunity);
		}
		if (records.Select(item => item.DuplicateKey).Distinct().Count() != records.Count)
		{
			throw new ArgumentException("The import contains duplicate funding opportunities.");
		}

		var importKeys = records.Select(item => item.DuplicateKey).ToList();
		if (importKeys.Count > 0 && await opportunities
			.Find(tenant.Filter<FundingOpportunity>() & Builders<FundingOpportunity>.Filter.In(item => item.DuplicateKey, importKeys))
			.AnyAsync(cancellationToken))
		{
			throw new ArgumentException("One or more funding opportunities have already been imported.");
		}

		if (records.Count > 0)
		{
			await opportunities.InsertManyAsync(records, cancellationToken: cancellationToken);
		}

		return records;
	}

	public async Task<FundingOpportunity?> UpdateAsync(FundingOpportunity opportunity, CancellationToken cancellationToken = default)
	{
		var existing = await GetByIdAsync(opportunity.Id, cancellationToken);
		if (existing is null)
		{
			return null;
		}

		Normalise(opportunity);
		if (await DuplicateExistsAsync(opportunity.DuplicateKey, opportunity.Id, cancellationToken))
		{
			throw new ArgumentException("This funding opportunity already exists.");
		}
		opportunity.CreatedAt = existing.CreatedAt;
		opportunity.UpdatedAt = DateTime.UtcNow;
		tenant.Assign(opportunity);

		var result = await opportunities.ReplaceOneAsync(
			tenant.Filter<FundingOpportunity>(item => item.Id == opportunity.Id),
			opportunity,
			cancellationToken: cancellationToken);

		return result.MatchedCount == 0 ? null : opportunity;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var result = await opportunities.DeleteOneAsync(
			tenant.Filter<FundingOpportunity>(opportunity => opportunity.Id == id),
			cancellationToken);
		return result.DeletedCount > 0;
	}

	private void PrepareNew(FundingOpportunity opportunity)
	{
		var now = DateTime.UtcNow;
		opportunity.Id = Guid.NewGuid();
		Normalise(opportunity);
		opportunity.CreatedAt = now;
		opportunity.UpdatedAt = now;
		tenant.Assign(opportunity);
	}

	private static void Normalise(FundingOpportunity opportunity)
	{
		opportunity.Name = opportunity.Name.Trim();
		opportunity.Description = (opportunity.Description ?? string.Empty).Trim();
		opportunity.Amount = (opportunity.Amount ?? string.Empty).Trim();
		opportunity.ApplicationUrl = (opportunity.ApplicationUrl ?? string.Empty).Trim();
		opportunity.StatusDetail = (opportunity.StatusDetail ?? string.Empty).Trim();
		opportunity.Notes = (opportunity.Notes ?? string.Empty).Trim();
		opportunity.DuplicateKey = $"{opportunity.Name.ToLowerInvariant()}|{opportunity.ApplicationUrl.ToLowerInvariant()}";
		opportunity.StartDate = ToUtcDate(opportunity.StartDate);
		opportunity.EndDate = ToUtcDate(opportunity.EndDate);
	}

	private async Task<bool> DuplicateExistsAsync(
		string duplicateKey,
		Guid? exceptId,
		CancellationToken cancellationToken)
	{
		var filter = tenant.Filter<FundingOpportunity>(item => item.DuplicateKey == duplicateKey);
		if (exceptId.HasValue)
		{
			filter &= Builders<FundingOpportunity>.Filter.Ne(item => item.Id, exceptId.Value);
		}
		return await opportunities.Find(filter).AnyAsync(cancellationToken);
	}

	private static DateTime? ToUtcDate(DateTime? value)
	{
		if (!value.HasValue)
		{
			return null;
		}

		return DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
	}
}
