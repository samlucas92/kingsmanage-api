using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubPostTemplateService : IClubPostTemplateService
{
	private readonly IMongoCollection<ClubPostTemplate> templates;
	private readonly TenantMongoScope tenant;

	public ClubPostTemplateService(MongoContext context, TenantMongoScope tenant)
	{
		templates = context.Database.GetCollection<ClubPostTemplate>("postTemplates");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubPostTemplate>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await templates.Find(tenant.Filter<ClubPostTemplate>())
			.SortBy(template => template.Name)
			.ToListAsync(cancellationToken);

	public async Task<ClubPostTemplate?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	) =>
		await templates.Find(
				tenant.Filter<ClubPostTemplate>() &
				Builders<ClubPostTemplate>.Filter.Eq(template => template.Id, id)
			)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<ClubPostTemplate> CreateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default)
	{
		template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
		tenant.Assign(template);
		template.CreatedAt = DateTime.UtcNow;
		template.UpdatedAt = template.CreatedAt;
		await templates.InsertOneAsync(template, cancellationToken: cancellationToken);
		return template;
	}

	public async Task<ClubPostTemplate?> UpdateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default)
	{
		var filter = tenant.Filter<ClubPostTemplate>() &
			Builders<ClubPostTemplate>.Filter.Eq(item => item.Id, template.Id);
		var existing = await templates.Find(filter).FirstOrDefaultAsync(cancellationToken);
		if (existing is null)
		{
			return null;
		}

		tenant.Assign(template);
		template.CreatedAt = existing.CreatedAt;
		template.UpdatedAt = DateTime.UtcNow;
		var result = await templates.ReplaceOneAsync(
			filter,
			template,
			cancellationToken: cancellationToken
		);
		return result.MatchedCount == 0 ? null : template;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var result = await templates.DeleteOneAsync(
			tenant.Filter<ClubPostTemplate>() & Builders<ClubPostTemplate>.Filter.Eq(template => template.Id, id),
			cancellationToken
		);
		return result.DeletedCount > 0;
	}
}
