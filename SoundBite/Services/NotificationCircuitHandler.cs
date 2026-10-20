using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace SoundBite.Services;

public sealed class NotificationCircuitHandler(ILogger<NotificationCircuitHandler> logger) : CircuitHandler
{
    private readonly ConcurrentDictionary<Guid, Func<Task>> subscribers = new();

    // Registers a callback to refresh notification state when the circuit reconnects.
    public IDisposable Subscribe(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var subscriptionId = Guid.NewGuid();
        subscribers[subscriptionId] = callback;
        return new Subscription(() => subscribers.TryRemove(subscriptionId, out _));
    }

    // Runs each registered refresh callback after a Blazor circuit reconnects.
    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        foreach (var (subscriptionId, callback) in subscribers.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await callback();
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Unable to refresh notification state after circuit reconnection for subscriber {SubscriptionId}",
                    subscriptionId);
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? disposeAction = dispose;

        // Removes this callback once, even if the subscription is disposed more than once.
        public void Dispose() => Interlocked.Exchange(ref disposeAction, null)?.Invoke();
    }
}
