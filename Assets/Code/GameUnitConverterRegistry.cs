using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameUnitConverterRegistry
{
    private static readonly Dictionary<Type, IGameUnitConverter> converters = new();

    static GameUnitConverterRegistry()
    {
        AutoRegisterAllConverters();
    }

    public static void Register<T, TData>(IGameUnitConverter<T, TData> converter)
    {
        converters[typeof(T)] = converter;
    }

    public static IGameUnitConverter Get(Type type)
    {
        converters.TryGetValue(type, out var conv);
        return conv;
    }

    public static IEnumerable<IGameUnitConverter> GetAll() => converters.Values;

    /// <summary>
    /// Automatically discovers and registers all IGameUnitConverter implementations in all loaded assemblies.
    /// </summary>
    public static void AutoRegisterAllConverters()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsAbstract && typeof(IGameUnitConverter).IsAssignableFrom(type))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type) as IGameUnitConverter;
                        if (instance != null)
                        {
                            // Use reflection to call the generic Register<T, TData>
                            var method = typeof(GameUnitConverterRegistry).GetMethod("Register")
                                .MakeGenericMethod(instance.SourceType, instance.DataType);
                            method.Invoke(null, new object[] { instance });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to auto-register converter {type}: {ex}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Call this if you need to re-register converters after domain reloads (optional).
    /// </summary>
    public static void Init()
    {
        converters.Clear();
        AutoRegisterAllConverters();
    }
    public static TData ToData<T, TData>(T value)
    {
        var converter = Get(typeof(T)) as IGameUnitConverter<T, TData>;
        if (converter == null)
            throw new InvalidOperationException($"No converter registered for {typeof(T)} → {typeof(TData)}");
        return converter.ToData(value);
    }

    public static T FromData<T, TData>(TData data)
    {
        var converter = Get(typeof(T)) as IGameUnitConverter<T, TData>;
        if (converter == null)
            throw new InvalidOperationException($"No converter registered for {typeof(T)} ← {typeof(TData)}");
        return converter.FromData(data);
    }
}
