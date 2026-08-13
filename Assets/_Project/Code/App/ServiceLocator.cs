using System;
using System.Collections.Generic;

namespace Game.App
{
    /// <summary>
    /// Minimal service registry used as the app's composition root. Bootstrap registers the
    /// long-lived services here; everything else resolves interfaces from it. Deliberately tiny —
    /// swap for a full DI container (VContainer/Zenject) later without touching call sites much.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj)) { service = (T)obj; return true; }
            service = null;
            return false;
        }

        public void Clear() => _services.Clear();
    }
}
