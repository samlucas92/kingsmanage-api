using KingsManage;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace KingsManage.Mongo.Services;

public class ClubNotificationService : IClubNotificationService
{
	private readonly IMongoCollection<ClubNotification> _notifications;
	private readonly TenantMongoScope _tenant;

	static ClubNotificationService()
	{
		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubNotification)))
		{
			BsonClassMap.RegisterClassMap<ClubNotification>(
				classMap =>
				{
					classMap.AutoMap();
					classMap.SetIgnoreExtraElements(true);
				}
			);
		}

		if (!BsonClassMap.IsClassMapRegistered(typeof(ClubNotificationRecipient)))
		{
			BsonClassMap.RegisterClassMap<ClubNotificationRecipient>(
				classMap =>
				{
					classMap.AutoMap();
					classMap.SetIgnoreExtraElements(true);
				}
			);
		}
	}

	public ClubNotificationService(MongoContext context, TenantMongoScope tenant)
	{
		_notifications = context.Database.GetCollection<ClubNotification>("notifications");
		_tenant = tenant;
	}

	public async Task<IReadOnlyList<ClubNotification>> GetForUserAsync(
		Guid userId,
		bool unreadOnly = false,
		CancellationToken cancellationToken = default
	)
	{
		var filter = unreadOnly
			? Builders<ClubNotification>.Filter.ElemMatch(
				notification => notification.Recipients,
				recipient => recipient.UserId == userId && recipient.Status == NotificationStatus.Unread
			)
			: Builders<ClubNotification>.Filter.ElemMatch(
				notification => notification.Recipients,
				recipient => recipient.UserId == userId
			);

		var notifications = await _notifications
			.Find(_tenant.Filter<ClubNotification>() & filter)
			.SortByDescending(notification => notification.CreatedAt)
			.ToListAsync(cancellationToken);

		return notifications.Select(NormaliseFromStorage).ToList();
	}

	public async Task<int> GetUnreadCountAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var filter = Builders<ClubNotification>.Filter.ElemMatch(
			notification => notification.Recipients,
			recipient => recipient.UserId == userId && recipient.Status == NotificationStatus.Unread
		);

		return (int)await _notifications.CountDocumentsAsync(
			_tenant.Filter<ClubNotification>() & filter,
			cancellationToken: cancellationToken);
	}

	public async Task<ClubNotification> CreateAsync(
		ClubNotification notification,
		CancellationToken cancellationToken = default
	)
	{
		notification.Id = notification.Id == Guid.Empty ? Guid.NewGuid() : notification.Id;
		PrepareForSave(notification);
		_tenant.Assign(notification);

		await _notifications.InsertOneAsync(notification, cancellationToken: cancellationToken);

		return notification;
	}

	public async Task<ClubNotification?> MarkReadAsync(
		Guid notificationId,
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var notification = await _notifications
			.Find(_tenant.Filter<ClubNotification>(currentNotification => currentNotification.Id == notificationId))
			.FirstOrDefaultAsync(cancellationToken);

		if (notification is null)
		{
			return null;
		}

		notification = NormaliseFromStorage(notification);

		var recipient = notification.Recipients.FirstOrDefault(currentRecipient => currentRecipient.UserId == userId);

		if (recipient is null)
		{
			return null;
		}

		recipient.Status = NotificationStatus.Read;
		recipient.ReadAt ??= DateTime.UtcNow;

		var result = await _notifications.ReplaceOneAsync(
			_tenant.Filter<ClubNotification>(currentNotification => currentNotification.Id == notificationId),
			notification,
			cancellationToken: cancellationToken
		);

		if (result.MatchedCount == 0)
		{
			return null;
		}

		return notification;
	}

	public async Task<int> MarkAllReadAsync(
		Guid userId,
		CancellationToken cancellationToken = default
	)
	{
		var notifications = await _notifications
			.Find(_tenant.Filter<ClubNotification>(notification => notification.Recipients.Any(recipient => recipient.UserId == userId)))
			.ToListAsync(cancellationToken);

		var updatedCount = 0;

		foreach (var notification in notifications.Select(NormaliseFromStorage))
		{
			var recipient = notification.Recipients.FirstOrDefault(currentRecipient => currentRecipient.UserId == userId);

			if (recipient is null || recipient.Status == NotificationStatus.Read)
			{
				continue;
			}

			recipient.Status = NotificationStatus.Read;
			recipient.ReadAt = DateTime.UtcNow;

			await _notifications.ReplaceOneAsync(
				_tenant.Filter<ClubNotification>(currentNotification => currentNotification.Id == notification.Id),
				notification,
				cancellationToken: cancellationToken
			);

			updatedCount++;
		}

		return updatedCount;
	}

	private static ClubNotification NormaliseFromStorage(ClubNotification notification)
	{
		notification.Title ??= string.Empty;
		notification.Message ??= string.Empty;
		notification.ActionPath ??= string.Empty;
		notification.CreatedByUserEmail ??= string.Empty;
		notification.Recipients ??= [];

		if (notification.CreatedAt == default)
		{
			notification.CreatedAt = DateTime.UtcNow;
		}

		foreach (var recipient in notification.Recipients)
		{
			if (recipient.Status == NotificationStatus.Read && !recipient.ReadAt.HasValue)
			{
				recipient.ReadAt = DateTime.UtcNow;
			}
		}

		return notification;
	}

	private static void PrepareForSave(ClubNotification notification)
	{
		notification.Title = notification.Title.Trim();
		notification.Message = notification.Message.Trim();
		notification.ActionPath = notification.ActionPath.Trim();
		notification.CreatedByUserEmail = notification.CreatedByUserEmail.Trim();
		notification.Recipients = notification.Recipients
			.GroupBy(recipient => recipient.UserId)
			.Select(group => group.First())
			.Where(recipient => recipient.UserId != Guid.Empty)
			.ToList();

		if (notification.CreatedAt == default)
		{
			notification.CreatedAt = DateTime.UtcNow;
		}
	}
}
