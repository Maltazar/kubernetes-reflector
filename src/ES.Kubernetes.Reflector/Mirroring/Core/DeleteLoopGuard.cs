using System.Collections.Concurrent;
using ES.FX.Additions.KubernetesClient.Models;

namespace ES.Kubernetes.Reflector.Mirroring.Core;

/// <summary>
///     Guards against delete/recreate hot loops. When reflector repeatedly deletes the same
///     target only for another controller to immediately recreate it, this detects the loop
///     and suppresses further deletes for a cooldown period to avoid unbounded CPU usage.
/// </summary>
public sealed class DeleteLoopGuard
{
    public const int DefaultThreshold = 5;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _cooldown;
    private readonly ConcurrentDictionary<NamespacedName, State> _states = new();
    private readonly int _threshold;
    private readonly TimeSpan _window;

    public DeleteLoopGuard(int threshold, TimeSpan window, TimeSpan cooldown)
    {
        _threshold = threshold;
        _window = window;
        _cooldown = cooldown;
    }

    public DeleteLoopGuard() : this(DefaultThreshold, DefaultWindow, DefaultCooldown)
    {
    }

    /// <summary>
    ///     Records a delete attempt for <paramref name="target" /> and decides whether it may proceed.
    /// </summary>
    /// <param name="target">The resource being deleted.</param>
    /// <param name="now">The current time (injected for deterministic testing).</param>
    /// <param name="justTripped">
    ///     True only on the call that first trips suppression, so callers can log a single warning.
    /// </param>
    /// <returns>True if the delete may proceed; false if it is suppressed due to a detected loop.</returns>
    public bool TryBeginDelete(NamespacedName target, DateTimeOffset now, out bool justTripped)
    {
        justTripped = false;
        var state = _states.GetOrAdd(target, _ => new State { WindowStart = now });
        lock (state)
        {
            if (state.SuppressedUntil.HasValue)
            {
                if (now < state.SuppressedUntil.Value) return false;

                // Cooldown elapsed — reset and allow a fresh attempt.
                state.SuppressedUntil = null;
                state.WindowStart = now;
                state.Count = 0;
            }

            if (now - state.WindowStart > _window)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            state.Count++;
            if (state.Count > _threshold)
            {
                state.SuppressedUntil = now + _cooldown;
                justTripped = true;
                return false;
            }

            return true;
        }
    }

    /// <summary>Forgets all tracked state (e.g. on watcher restart).</summary>
    public void Clear() => _states.Clear();

    /// <summary>Forgets tracked state for a single target.</summary>
    public void Forget(NamespacedName target) => _states.TryRemove(target, out _);

    private sealed class State
    {
        public int Count;
        public DateTimeOffset? SuppressedUntil;
        public DateTimeOffset WindowStart;
    }
}
