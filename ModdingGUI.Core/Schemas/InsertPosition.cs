namespace ModdingGUI.Core.Schemas;

/// <summary>
/// Placement hint for <see cref="RecordSet{TRecord}.AddRecord(TRecord, InsertPosition, string?)"/>.
/// </summary>
public enum InsertPositionKind
{
    /// <summary>Append after the last existing record (default).</summary>
    AtEnd,
    /// <summary>Insert immediately after a named sibling record.</summary>
    After,
    /// <summary>Insert immediately before a named sibling record.</summary>
    Before,
}

/// <summary>
/// Where a newly added record should be placed inside its owning <see cref="RecordSet{TRecord}"/>.
/// </summary>
/// <param name="Kind">Placement mode.</param>
/// <param name="AnchorStableId">
/// Required when <see cref="Kind"/> is <see cref="InsertPositionKind.After"/> or
/// <see cref="InsertPositionKind.Before"/>; ignored for <see cref="InsertPositionKind.AtEnd"/>.
/// </param>
public readonly record struct InsertPosition(InsertPositionKind Kind, string? AnchorStableId = null)
{
    /// <summary>Append at end of the record list.</summary>
    public static InsertPosition AtEnd => new(InsertPositionKind.AtEnd);
    /// <summary>Insert immediately after the record whose stable id is <paramref name="stableId"/>.</summary>
    public static InsertPosition After(string stableId) => new(InsertPositionKind.After, stableId);
    /// <summary>Insert immediately before the record whose stable id is <paramref name="stableId"/>.</summary>
    public static InsertPosition Before(string stableId) => new(InsertPositionKind.Before, stableId);
}
