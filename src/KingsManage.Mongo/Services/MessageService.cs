using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class MessageService : IMessageService
{
	private readonly IMongoCollection<MessageThread> threads;
	private readonly IMongoCollection<Message> messages;
	private readonly TenantMongoScope tenant;

	static MessageService()
	{
		RegisterClassMap<MessageThread>();
		RegisterClassMap<MessageThreadParticipant>();
		RegisterClassMap<Message>();
	}

	public MessageService(MongoContext context, TenantMongoScope tenant)
	{
		threads = context.Database.GetCollection<MessageThread>("messageThreads");
		messages = context.Database.GetCollection<Message>("messages");
		this.tenant = tenant;

		threads.Indexes.CreateOne(new CreateIndexModel<MessageThread>(
			Builders<MessageThread>.IndexKeys
				.Ascending(thread => thread.OrganizationId)
				.Ascending(thread => thread.ClubId)
				.Ascending(thread => thread.DirectPairKey),
			new CreateIndexOptions<MessageThread>
			{
				Unique = true,
				PartialFilterExpression = Builders<MessageThread>.Filter.Gt(thread => thread.DirectPairKey, string.Empty)
			}
		));

		messages.Indexes.CreateOne(new CreateIndexModel<Message>(
			Builders<Message>.IndexKeys
				.Ascending(message => message.OrganizationId)
				.Ascending(message => message.ClubId)
				.Ascending(message => message.ThreadId)
				.Descending(message => message.CreatedAt)
		));
	}

	public async Task<IReadOnlyList<MessageThread>> GetThreadsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		return await threads
			.Find(tenant.Filter<MessageThread>(thread => thread.Participants.Any(participant => participant.UserId == userId)))
			.SortByDescending(thread => thread.UpdatedAt)
			.ToListAsync(cancellationToken);
	}

	public async Task<MessageThread?> GetThreadForUserAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default)
	{
		return await threads
			.Find(tenant.Filter<MessageThread>(thread => thread.Id == threadId && thread.Participants.Any(participant => participant.UserId == userId)))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<MessageThread> GetOrCreateDirectThreadAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
	{
		var pairKey = CreateDirectPairKey(firstUserId, secondUserId);
		var existingThread = await threads.Find(tenant.Filter<MessageThread>(thread => thread.DirectPairKey == pairKey)).FirstOrDefaultAsync(cancellationToken);

		if (existingThread is not null)
		{
			return existingThread;
		}

		var now = DateTime.UtcNow;
		var thread = new MessageThread
		{
			Id = Guid.NewGuid(),
			Type = MessageThreadType.Direct,
			DirectPairKey = pairKey,
			Participants =
			[
				new MessageThreadParticipant { UserId = firstUserId, JoinedAt = now, LastReadAt = now },
				new MessageThreadParticipant { UserId = secondUserId, JoinedAt = now, LastReadAt = now }
			],
			CreatedAt = now,
			UpdatedAt = now
		};
		tenant.Assign(thread);

		try
		{
			await threads.InsertOneAsync(thread, cancellationToken: cancellationToken);
			return thread;
		}
		catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
		{
			return await threads.Find(tenant.Filter<MessageThread>(currentThread => currentThread.DirectPairKey == pairKey)).FirstAsync(cancellationToken);
		}
	}

	public async Task<IReadOnlyList<Message>> GetMessagesAsync(Guid threadId, CancellationToken cancellationToken = default)
	{
		return await messages.Find(tenant.Filter<Message>(message => message.ThreadId == threadId))
			.SortBy(message => message.CreatedAt)
			.ToListAsync(cancellationToken);
	}

	public async Task<Message> CreateMessageAsync(Message message, CancellationToken cancellationToken = default)
	{
		message.Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id;
		message.Body = message.Body.Trim();
		message.SenderUserEmail = message.SenderUserEmail.Trim();
		message.CreatedAt = DateTime.UtcNow;
		message.Status = MessageStatus.Active;
		tenant.Assign(message);

		await messages.InsertOneAsync(message, cancellationToken: cancellationToken);
		await threads.UpdateOneAsync(
			tenant.Filter<MessageThread>(thread => thread.Id == message.ThreadId),
			Builders<MessageThread>.Update.Set(thread => thread.UpdatedAt, message.CreatedAt),
			cancellationToken: cancellationToken
		);

		return message;
	}

	public async Task<MessageThread?> MarkReadAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default)
	{
		var thread = await GetThreadForUserAsync(threadId, userId, cancellationToken);

		if (thread is null)
		{
			return null;
		}

		var participant = thread.Participants.First(currentParticipant => currentParticipant.UserId == userId);
		participant.LastReadAt = DateTime.UtcNow;

		await threads.ReplaceOneAsync(tenant.Filter<MessageThread>(currentThread => currentThread.Id == threadId), thread, cancellationToken: cancellationToken);
		return thread;
	}

	public async Task<Message?> DeleteOwnMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
	{
		var update = Builders<Message>.Update
			.Set(message => message.Status, MessageStatus.Deleted)
			.Set(message => message.Body, string.Empty)
			.Set(message => message.DeletedAt, DateTime.UtcNow);

		return await messages.FindOneAndUpdateAsync(
			tenant.Filter<Message>(message => message.Id == messageId && message.SenderUserId == userId && message.Status == MessageStatus.Active),
			update,
			new FindOneAndUpdateOptions<Message> { ReturnDocument = ReturnDocument.After },
			cancellationToken
		);
	}

	private static string CreateDirectPairKey(Guid firstUserId, Guid secondUserId) =>
		string.Join(':', new[] { firstUserId, secondUserId }.OrderBy(id => id));

	private static void RegisterClassMap<T>()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
		{
			BsonClassMap.RegisterClassMap<T>(classMap =>
			{
				classMap.AutoMap();
				classMap.SetIgnoreExtraElements(true);
			});
		}
	}
}
