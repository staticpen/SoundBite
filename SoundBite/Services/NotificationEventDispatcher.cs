namespace SoundBite.Services;

public sealed class NotificationEventDispatcher(ILogger<NotificationEventDispatcher> logger)
{
    private readonly object subscribersLock = new();
    private readonly Dictionary<string, Dictionary<Guid, Func<Task>>> subscribers = new();

    public IDisposable Subscribe(string userId, Func<Task> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(callback);

        var subscriptionId = Guid.NewGuid();
        lock (subscribersLock)
        {
            if (!subscribers.TryGetValue(userId, out var userSubscribers))
            {
                userSubscribers = [];
                subscribers.Add(userId, userSubscribers);
            }
            userSubscribers.Add(subscriptionId, callback);
        }
        return new Subscription(() =>
        {
            lock (subscribersLock)
            {
                if (subscribers.TryGetValue(userId, out var userSubscribers) &&
                    userSubscribers.Remove(subscriptionId) &&
                    userSubscribers.Count == 0)
                {
                    subscribers.Remove(userId);
                }
            }
        });
    }

    public async Task PublishAsync(string userId)
    {
        Func<Task>[] callbacks;
        lock (subscribersLock)
        {
            callbacks = subscribers.TryGetValue(userId, out var userSubscribers)
                ? userSubscribers.Values.ToArray()
                : [];
        }

        foreach (var callback in callbacks)
        {
            try
            {
                await callback();
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Unable to deliver notification update to a subscriber for user {UserId}",
                    userId);
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? disposeAction = dispose;

        public void Dispose() => Interlocked.Exchange(ref disposeAction, null)?.Invoke();
    }
}
