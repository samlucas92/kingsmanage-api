namespace KingsManage;

public interface IMessageService
{
	Task<IReadOnlyList<MessageThread>> GetThreadsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
	Task<MessageThread?> GetThreadForUserAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default);
	Task<MessageThread> GetOrCreateDirectThreadAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<Message>> GetMessagesAsync(Guid threadId, CancellationToken cancellationToken = default);
	Task<Message> CreateMessageAsync(Message message, CancellationToken cancellationToken = default);
	Task<MessageThread?> MarkReadAsync(Guid threadId, Guid userId, CancellationToken cancellationToken = default);
	Task<Message?> DeleteOwnMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
