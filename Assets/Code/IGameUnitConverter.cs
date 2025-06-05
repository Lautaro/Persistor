using System;

/// <summary>
/// Generic interface for converting between a runtime type (T) and a serializable data type (TData).
/// Inherits from the non-generic IGameUnitConverter to allow type-agnostic management and discovery.
/// </summary>
/// <typeparam name="T">The runtime type (e.g., Vector3).</typeparam>
/// <typeparam name="TData">The serializable data type (e.g., Vector3Data).</typeparam>
public interface IGameUnitConverter<T, TData> : IGameUnitConverter
{
    /// <summary>
    /// Converts a runtime value to its serializable data representation.
    /// </summary>
    /// <param name="value">The runtime value.</param>
    /// <returns>The serializable data representation.</returns>
    TData ToData(T value);

    /// <summary>
    /// Converts a serializable data value back to its runtime representation.
    /// </summary>
    /// <param name="data">The serializable data value.</param>
    /// <returns>The runtime value.</returns>
    T FromData(TData data);
}

/// <summary>
/// Non-generic base interface for all game unit converters.
/// This allows all converters to be managed, discovered, and registered in a type-agnostic way,
/// which is essential for reflection-based discovery and for storing them in a single registry.
/// </summary>
public interface IGameUnitConverter
{
    /// <summary>
    /// The source (runtime) type this converter handles (e.g., typeof(Vector3)).
    /// </summary>
    Type SourceType { get; }

    /// <summary>
    /// The data (serializable) type this converter handles (e.g., typeof(Vector3Data)).
    /// </summary>
    Type DataType { get; }
}

/*
    Why this inheritance pattern?

    - The generic interface (IGameUnitConverter<T, TData>) provides type-safe conversion logic for each specific type pair.
    - The non-generic base (IGameUnitConverter) allows all converters to be managed in a type-agnostic way.
    - This enables:
        * Reflection-based discovery of all converters (e.g., for auto-registration).
        * Storing all converters in a single registry (e.g., Dictionary<Type, IGameUnitConverter>).
        * Querying the source/data types at runtime, even if you don't know the generic parameters.
    - This pattern is common in plugin/adapter/strategy systems in C# and ensures both extensibility and maintainability.
*/