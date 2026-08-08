namespace ModdingGUI.Core;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ModdingGUI.Core.Formats.Items;
using ModdingGUI.Core.Schemas;

/// <summary>
/// Process-wide registry that maps a <see cref="RecordSchema"/> format id (e.g.
/// <c>"items"</c>) to the singleton schema instance that handles it.
/// </summary>
/// <remarks>
/// <para>
/// The registry is the indirection layer between the API / MCP tool surface and
/// the concrete schema implementations. It is populated once by
/// <see cref="Core.Initialize"/> and is idempotent: calling
/// <see cref="Core.Initialize"/> repeatedly has no additional effect.
/// </para>
/// <para>
/// The registry does not (yet) know about project directories, file paths, or
/// locking — it is a pure "format id → schema" catalogue. Higher-level APIs
/// (e.g. <c>IItemsApi</c>) compose the registry with <see cref="ProjectLock"/>
/// and <see cref="ChangeHistoryService"/>.
/// </para>
/// </remarks>
public static class FileFormatRegistry
{
    private static readonly ConcurrentDictionary<string, object> s_schemas =
        new(StringComparer.Ordinal);

    /// <summary>True once the built-in formats have been registered.</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Register a concrete <see cref="RecordSchema{TRecord}"/> under its
    /// <see cref="RecordSchema.FormatId"/>. Subsequent registrations for the
    /// same id are ignored (idempotent).
    /// </summary>
    public static void Register<TRecord>(RecordSchema<TRecord> schema) where TRecord : class, new()
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        s_schemas.TryAdd(schema.FormatId, schema);
    }

    /// <summary>Returns the registered schema for <paramref name="formatId"/>, or null.</summary>
    public static RecordSchema<TRecord>? TryGet<TRecord>(string formatId) where TRecord : class, new()
    {
        if (s_schemas.TryGetValue(formatId, out var schema))
            return schema as RecordSchema<TRecord>;
        return null;
    }

    /// <summary>Enumerate all registered format ids (sorted).</summary>
    public static IReadOnlyList<string> FormatIds
    {
        get
        {
            var keys = new List<string>(s_schemas.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }

    internal static void RegisterBuiltins()
    {
        if (IsInitialized) return;
        Register(new ItemsSchema());
        IsInitialized = true;
    }

    /// <summary>
    /// Test-only hook to reset the registry; not for production use. The
    /// <see cref="ItemsApi"/> surface must never call this.
    /// </summary>
    internal static void ResetForTests()
    {
        s_schemas.Clear();
        IsInitialized = false;
    }
}
