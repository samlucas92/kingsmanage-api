using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubPostService : IClubPostService
{
	private readonly IMongoCollection<ClubPost> posts;
	private readonly TenantMongoScope tenant;

	static ClubPostService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubPost)))
		{
			BsonClassMap.RegisterClassMap<ClubPost>(
				classMap =>
				{
					classMap.AutoMap();
					classMap.SetIgnoreExtraElements(true);
				}
			);
		}
	}

	public ClubPostService(MongoContext context, TenantMongoScope tenant)
	{
		posts = context.Database.GetCollection<ClubPost>("posts");
		this.tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubPost>> GetAllAsync(
		CancellationToken cancellationToken = default
	)
	{
		var posts = await this.posts
			.Find(tenant.Filter<ClubPost>())
			.SortByDescending(post => post.IsPinned)
			.ThenByDescending(post => post.CreatedAt)
			.ToListAsync(cancellationToken);

		return posts.Select(NormaliseFromStorage).ToList();
	}

	public async Task<ClubPost?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var post = await posts
			.Find(tenant.Filter<ClubPost>(post => post.Id == id))
			.FirstOrDefaultAsync(cancellationToken);

		return post is null ? null : NormaliseFromStorage(post);
	}

	public async Task<ClubPost> CreateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	)
	{
		post.Id = post.Id == Guid.Empty ? Guid.NewGuid() : post.Id;
		PrepareForSave(post, true);
		tenant.Assign(post);

		await posts.InsertOneAsync(post, cancellationToken: cancellationToken);

		return post;
	}

	public async Task<ClubPost?> UpdateAsync(
		ClubPost post,
		CancellationToken cancellationToken = default
	)
	{
		PrepareForSave(post, false);
		tenant.Assign(post);

		var result = await posts.ReplaceOneAsync(
			tenant.Filter<ClubPost>(existingPost => existingPost.Id == post.Id),
			post,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return post;
	}

	public async Task<bool> DeleteAsync(
		Guid id,
		CancellationToken cancellationToken = default
	)
	{
		var result = await posts.DeleteOneAsync(
			tenant.Filter<ClubPost>(post => post.Id == id),
			cancellationToken
		);

		return result.DeletedCount > 0;
	}

	private static ClubPost NormaliseFromStorage(ClubPost post)
	{
		post.Title ??= string.Empty;
		post.Body ??= string.Empty;
		post.CreatedByUserEmail ??= string.Empty;

		if (post.CreatedAt == default)
		{
			post.CreatedAt = DateTime.UtcNow;
		}

		if (post.UpdatedAt == default)
		{
			post.UpdatedAt = post.CreatedAt;
		}

		return post;
	}

	private static void PrepareForSave(ClubPost post, bool isNew)
	{
		post.Title = post.Title.Trim();
		post.Body = post.Body.Trim();
		post.CreatedByUserEmail = post.CreatedByUserEmail.Trim();

		if (isNew || post.CreatedAt == default)
		{
			post.CreatedAt = DateTime.UtcNow;
		}

		post.UpdatedAt = DateTime.UtcNow;
	}
}
