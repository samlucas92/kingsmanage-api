using KingsManage;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubPostTemplateService : IClubPostTemplateService
{
	private readonly IMongoCollection<ClubPostTemplate> _templates;
	private readonly TenantMongoScope _tenant;

	public ClubPostTemplateService(MongoContext context, TenantMongoScope tenant)
	{
		_templates = context.Database.GetCollection<ClubPostTemplate>("postTemplates");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubPostTemplate>> GetAllAsync(CancellationToken cancellationToken = default) =>
		await _templates.Find(_tenant.Filter<ClubPostTemplate>())
			.SortBy(template => template.Name)
			.ToListAsync(cancellationToken);

	public async Task<ClubPostTemplate?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	) =>
		await _templates.Find(
				_tenant.Filter<ClubPostTemplate>() &
				Builders<ClubPostTemplate>.Filter.Eq(template => template.Id, id)
			)
			.FirstOrDefaultAsync(cancellationToken);

	public async Task<ClubPostTemplate> CreateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default)
	{
		template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
		_tenant.Assign(template);
		template.CreatedAt = DateTime.UtcNow;
		template.UpdatedAt = template.CreatedAt;
		await _templates.InsertOneAsync(template, cancellationToken: cancellationToken);
		return template;
	}

	public async Task<ClubPostTemplate?> UpdateAsync(ClubPostTemplate template, CancellationToken cancellationToken = default)
	{
		var filter = _tenant.Filter<ClubPostTemplate>() &
			Builders<ClubPostTemplate>.Filter.Eq(item => item.Id, template.Id);
		var existing = await _templates.Find(filter).FirstOrDefaultAsync(cancellationToken);
		if (existing is null)
		{
			return null;
		}

		_tenant.Assign(template);
		template.CreatedAt = existing.CreatedAt;
		template.UpdatedAt = DateTime.UtcNow;
		var result = await _templates.ReplaceOneAsync(
			filter,
			template,
			cancellationToken: cancellationToken
		);
		return result.MatchedCount == 0 ? null : template;
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var result = await _templates.DeleteOneAsync(
			_tenant.Filter<ClubPostTemplate>() & Builders<ClubPostTemplate>.Filter.Eq(template => template.Id, id),
			cancellationToken
		);
		return result.DeletedCount > 0;
	}
}
