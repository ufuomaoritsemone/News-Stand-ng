using System.Reflection;

namespace NigerianNewGrid.Services;

/// <summary>
/// Singleton implementation of <see cref="IAppEventBus"/> featuring weak-target delegate dispatch.
/// Prevents memory leaks by ensuring event subscriptions do not root subscriber objects (e.g. transient ViewModels).
/// </summary>
public class AppEventBus : IAppEventBus
{
    private static readonly Lazy<AppEventBus> _instance = new(() => new AppEventBus());
    public static AppEventBus Instance => _instance.Value;

    private readonly WeakEventSubscriptionList<Action> _playAudioBriefingSubscriptions = new();
    private readonly WeakEventSubscriptionList<Action<int>> _newStoriesAvailableSubscriptions = new();
    private readonly WeakEventSubscriptionList<Action> _appResumedSubscriptions = new();

    public event Action? PlayAudioBriefingRequested
    {
        add => _playAudioBriefingSubscriptions.Add(value);
        remove => _playAudioBriefingSubscriptions.Remove(value);
    }

    public event Action<int>? NewStoriesAvailable
    {
        add => _newStoriesAvailableSubscriptions.Add(value);
        remove => _newStoriesAvailableSubscriptions.Remove(value);
    }

    public event Action? AppResumed
    {
        add => _appResumedSubscriptions.Add(value);
        remove => _appResumedSubscriptions.Remove(value);
    }

    public void TriggerAutoPlayAudioBriefing()
    {
        _playAudioBriefingSubscriptions.Invoke();
    }

    public void TriggerAppResumed()
    {
        _appResumedSubscriptions.Invoke();
    }

    public void NotifyNewStoriesAvailable(int count)
    {
        if (count > 0)
        {
            _newStoriesAvailableSubscriptions.Invoke(count);
        }
    }

    private sealed class WeakEventSubscriptionList<TDelegate> where TDelegate : Delegate
    {
        private readonly List<WeakSubscription> _subscriptions = [];
        private readonly object _lock = new();

        public void Add(TDelegate? handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                PruneDeadSubscriptions();
                _subscriptions.Add(new WeakSubscription(handler));
            }
        }

        public void Remove(TDelegate? handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                _subscriptions.RemoveAll(s => s.Matches(handler));
            }
        }

        public void Invoke(params object?[] args)
        {
            List<WeakSubscription> active;
            lock (_lock)
            {
                PruneDeadSubscriptions();
                active = [.. _subscriptions];
            }

            foreach (var sub in active)
            {
                try
                {
                    sub.Invoke(args);
                }
                catch
                {
                    // Fault isolation: one faulty subscriber shouldn't break other subscribers
                }
            }
        }

        private void PruneDeadSubscriptions()
        {
            _subscriptions.RemoveAll(s => !s.IsAlive);
        }

        private sealed class WeakSubscription
        {
            private readonly WeakReference? _targetRef;
            private readonly MethodInfo _method;
            private readonly bool _isStatic;

            public WeakSubscription(TDelegate handler)
            {
                _method = handler.Method;
                if (handler.Target != null)
                {
                    _targetRef = new WeakReference(handler.Target);
                    _isStatic = false;
                }
                else
                {
                    _isStatic = true;
                }
            }

            public bool IsAlive => _isStatic || (_targetRef?.IsAlive ?? false);

            public bool Matches(TDelegate handler)
            {
                if (_method != handler.Method) return false;
                if (_isStatic) return handler.Target == null;
                return _targetRef != null && ReferenceEquals(_targetRef.Target, handler.Target);
            }

            public void Invoke(object?[] args)
            {
                if (_isStatic)
                {
                    _method.Invoke(null, args.Length == 0 ? null : args);
                }
                else if (_targetRef?.Target is object target)
                {
                    _method.Invoke(target, args.Length == 0 ? null : args);
                }
            }
        }
    }
}
