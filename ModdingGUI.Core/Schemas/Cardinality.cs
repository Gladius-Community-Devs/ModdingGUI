namespace ModdingGUI.Core.Schemas;

/// <summary>
/// How many times a given keyed body field may appear within a single record.
/// </summary>
public enum Cardinality
{
    /// <summary>At most one occurrence per record (typical scalar field).</summary>
    Single,

    /// <summary>
    /// Zero or more occurrences per record (repeated list fields such as
    /// <c>ITEMSTATMOD</c> or <c>ITEMREGION</c>). Patch DTOs for such fields use
    /// the Add/Remove verb pair rather than a scalar value.
    /// </summary>
    Multiple,
}
