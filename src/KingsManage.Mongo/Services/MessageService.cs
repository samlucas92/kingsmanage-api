using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class MessageService : IMessageService
{
	private readonly IMongoCollection<MessageThread> _threads;
	private readonly IMongoCollection<Message> _messages;
	private readonly TenantMongoScope _tenant;

	static MessageService()
	{
		RegisterClassMap<MessageThread>();
		RegisterClassMap<MessageThreadParticipant>();
		RegisterClassMap<Message>();
	}

	public MessageService(MongoContext context, TenantMongoScope tenant)
	{
		_threads = context.Database.GetCollection<MessageThread>("messageThreads");
		_messages = context.Database.GetCollection<Message>("messages");
		_tenant = tenant;

		_threads.Indexes.CreateOne(new CreateIndexModel<MessageThread>(
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

		_messages.Indexes.CreateOne(new CreateIndexModel<Message>(
			Builders<Message>.IndexKeys
				.Ascending(message => message.OrganizationId)
				.Ascending(message => message.ClubId)
				.Ascending(message => message.ThreadId)
				.Descending(message => message.CreatedAt)
		));
	}

	public async Task<IReadOnlyList<MessageThread>> GetThreadsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
	{
		return await _threads
			.Find(_tenant.Filter<MessageThread>(thread => thread.Participants.Any(participant => participant.UserId == userId)))
			.SortByDescending(thread => thread.UpdatedAt)
			.ToListAsync(cancellationToken);
	}

	public async Task<MessageThread?> GetThreadForUserAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default)
	{
		return await _threads
			.Find(_tenant.Filter<MessageThread>(thread => thread.Id == threadId && thread.Participants.Any(participant => participant.UserId == userId)))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<MessageThread> GetOrCreateDirectThreadAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default)
	{
		var pairKey = CreateDirectPairKey(firstUserId, secondUserId);
		var existingThread = await _threads.Find(_tenant.Filter<MessageThread>(thread => thread.DirectPairKey == pairKey)).FirstOrDefaultAsync(cancellationToken);

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
		_tenant.Assign(thread);

		try
		{
			await _threads.InsertOneAsync(thread, cancellationToken: cancellationToken);
			return thread;
		}
		catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
		{
			return await _threads.Find(_tenant.Filter<MessageThread>(currentThread => currentThread.DirectPairKey == pairKey)).FirstAsync(cancellationToken);
		}
	}

	public async Task<IReadOnlyList<Message>> GetMessagesAsync(Guid threadId, CancellationToken cancellationToken = default)
	{
		return await _messages.Find(_tenant.Filter<Message>(message => message.ThreadId == threadId))
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
		_tenant.Assign(message);

		await _messages.InsertOneAsync(message, cancellationToken: cancellationToken);
		await _threads.UpdateOneAsync(
			_tenant.Filter<MessageThread>(thread => thread.Id == message.ThreadId),
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

		await _threads.ReplaceOneAsync(_tenant.Filter<MessageThread>(currentThread => currentThread.Id == threadId), thread, cancellationToken: cancellationToken);
		return thread;
	}

	public async Task<Message?> DeleteOwnMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
	{
		var update = Builders<Message>.Update
			.Set(message => message.Status, MessageStatus.Deleted)
			.Set(message => message.Body, string.Empty)
			.Set(message => message.DeletedAt, DateTime.UtcNow);

		return await _messages.FindOneAndUpdateAsync(
			_tenant.Filter<Message>(message => message.Id == messageId && message.SenderUserId == userId && message.Status == MessageStatus.Active),
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
