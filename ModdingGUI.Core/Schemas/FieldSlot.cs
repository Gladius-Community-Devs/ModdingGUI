namespace ModdingGUI.Core.Schemas;

/// <summary>
/// Typed storage slot for a schema field value that distinguishes "unset" from
/// "set to default". Used by <see cref="FieldDescriptor{TRecord,TValue}"/> so
/// that absent body keys round-trip correctly: an unset slot emits no line on
/// write, while a slot set to default(<typeparamref name="T"/>) emits a line
/// with the default value.
/// </summary>
/// <remarks>
/// A dedicated slot type (instead of <see cref="System.Nullable{T}"/>) is used
/// so that <typeparamref name="T"/> may itself be a reference type (e.g.
/// <see cref="string"/>) or a tuple value type, without conflating "no value"
/// with "null is the value".
/// </remarks>
public sealed class FieldSlot<T>
{
    private T? _value;

    /// <summary>True when <see cref="Set"/> has been called at least once.</summary>
    public bool HasValue { get; private set; }

    /// <summary>The current value, or default when <see cref="HasValue"/> is false.</summary>
    public T? Value => _value;

    /// <summary>Assign a value and mark the slot as set.</summary>
    public void Set(T value)
    {
        _value = value;
        HasValue = true;
    }

    /// <summary>Clear the slot so the field will not be emitted on write.</summary>
    public void Clear()
    {
        _value = default;
        HasValue = false;
    }
}
