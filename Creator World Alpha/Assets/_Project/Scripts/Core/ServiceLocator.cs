using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreatorWorld.Core
{
    /// <summary>
    /// Simple service locator for dependency injection without heavy frameworks.
    /// Register services at startup, resolve them anywhere.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new();
        private static readonly Dictionary<Type, Func<object>> factories = new();

        /// <summary>
        /// Register a service instance.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service: {type.Name}");
            }
            services[type] = service;
        }

        /// <summary>
        /// Register a factory function for lazy instantiation.
        /// </summary>
        public static void RegisterFactory<T>(Func<T> factory) where T : class
        {
            factories[typeof(T)] = () => factory();
        }

        /// <summary>
        /// Get a registered service. Returns null if not found.
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);

            // Check direct registration first
            if (services.TryGetValue(type, out var service))
            {
                return service as T;
            }

            // Check factory
            if (factories.TryGetValue(type, out var factory))
            {
                var instance = factory() as T;
                services[type] = instance; // Cache for future calls
                return instance;
            }

            return null;
        }

        /// <summary>
        /// Get a service, throwing if not found.
        /// </summary>
        public static T GetRequired<T>() where T : class
        {
            var service = Get<T>();
            if (service == null)
            {
                throw new InvalidOperationException($"Required service not registered: {typeof(T).Name}");
            }
            return service;
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        public static bool Has<T>() where T : class
        {
            var type = typeof(T);
            return services.ContainsKey(type) || factories.ContainsKey(type);
        }

        /// <summary>
        /// Unregister a service.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            services.Remove(type);
            factories.Remove(type);
        }

        /// <summary>
        /// Clear all registered services. Call on scene unload or game quit.
        /// </summary>
        public static void Clear()
        {
            services.Clear();
            factories.Clear();
        }

        /// <summary>
        /// Initialize core services. Call from a bootstrap script.
        /// </summary>
        public static void Initialize()
        {
            // Services will be registered by their respective MonoBehaviours
            // This method exists for explicit initialization if needed
            Debug.Log("[ServiceLocator] Initialized");
        }
    }

    /// <summary>
    /// MonoBehaviour helper to auto-register as a service.
    /// Inherit from this instead of MonoBehaviour for service components.
    /// </summary>
    public abstract class ServiceBehaviour<T> : MonoBehaviour where T : class
    {
        protected virtual void Awake()
        {
            ServiceLocator.Register(this as T);
        }

        protected virtual void OnDestroy()
        {
            ServiceLocator.Unregister<T>();
        }
    }
}
