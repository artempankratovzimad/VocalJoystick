using System;
using System.Collections.Generic;
using System.Linq;

namespace VocalJoystick.App.DependencyInjection;

internal sealed class SimpleServiceProvider : IDisposable
{
    private readonly Dictionary<Type, Func<SimpleServiceProvider, object>> _factories = new();
    private readonly Dictionary<Type, object> _instances = new();

    public void RegisterSingleton<TService>(Func<SimpleServiceProvider, TService> factory)
        where TService : class
    {
        _factories[typeof(TService)] = provider => factory(provider)!;
    }

    public TService GetRequiredService<TService>() where TService : class
    {
        if (_instances.TryGetValue(typeof(TService), out var instance))
        {
            return (TService)instance;
        }

        if (!_factories.TryGetValue(typeof(TService), out var factory))
        {
            throw new InvalidOperationException($"Service {typeof(TService)} has not been registered.");
        }

        var created = (TService)factory(this);
        _instances[typeof(TService)] = created!;
        return created;
    }

    public void Dispose()
    {
        foreach (var disposable in _instances.Values.OfType<IDisposable>())
        {
            disposable.Dispose();
        }

        _instances.Clear();
    }
}
