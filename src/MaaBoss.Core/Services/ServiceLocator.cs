using System;
using System.Collections.Generic;

namespace MaaBoss.Core.Services;

/// <summary>
/// 服务定位器，用于在 ViewModel 之间共享服务实例。
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
    }

    public static T Get<T>() where T : notnull
    {
        if (_services.TryGetValue(typeof(T), out var service))
            return (T)service;

        throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册");
    }

    public static T GetRequiredService<T>() where T : notnull
    {
        return Get<T>();
    }

    public static T GetOrCreate<T>() where T : notnull, new()
    {
        if (_services.TryGetValue(typeof(T), out var service))
            return (T)service;

        var instance = new T();
        _services[typeof(T)] = instance;
        return instance;
    }

    public static void Reset()
    {
        _services.Clear();
    }
}
