namespace ModdingGUI.Core.Grids;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Where an arena's map page sits over the grid.</summary>
/// <param name="RotationIndex">Index into the view's rotation list: 0 None, 1 90, 2 180, 3 270, 4 Flip H, 5 Flip V.</param>
/// <param name="OffsetX">Board pixels to the right.</param>
/// <param name="OffsetY">Board pixels down.</param>
/// <param name="Scale">Page pixels per board pixel multiple.</param>
public sealed record MapPlacement(int RotationIndex, int OffsetX, int OffsetY, decimal Scale);

/// <summary>
/// Per-arena map placements, saved beside the app.
/// </summary>
/// <remarks>
/// <para>
/// The original app hand-tuned ten arenas in code and left the rest at scale 1, no rotation. The alignment is a property of
/// the art rather than something derivable from the <c>.grd</c> data - attempts to infer it from the wall cells did not
/// reproduce the hand-tuned values - so the only reliable source is somebody looking at the overlay and saying "that is
/// where it goes". This store is that memory: tuned once in the UI, applied forever afterwards, per arena.
/// </para>
/// <para>
/// Precedence when an arena loads: saved placement, then the built-in table, then fit-to-board. Kept as JSON so it can be
/// hand-edited or shipped with a project.
/// </para>
/// </remarks>
public sealed class MapPlacementStore
{
    /// <summary>File name inside the app directory.</summary>
    public const string FileName = "map_placements.json";

    private const int CurrentVersion = 1;

    private readonly Dictionary<string, MapPlacement> _placements;

    private MapPlacementStore(Dictionary<string, MapPlacement> placements, string? path, string? problem)
    {
        _placements = placements;
        Path = path;
        Problem = problem;
    }

    /// <summary>Where the store was loaded from, when it came from a file.</summary>
    public string? Path { get; }

    /// <summary>Why the file could not be used, when it could not.</summary>
    public string? Problem { get; }

    /// <summary>Every saved placement, keyed by arena (grd parent folder name).</summary>
    public IReadOnlyDictionary<string, MapPlacement> All => _placements;

    /// <summary>An empty store that saves nothing anywhere; used when there is no app directory.</summary>
    public static MapPlacementStore Empty { get; } = new(new Dictionary<string, MapPlacement>(StringComparer.OrdinalIgnoreCase), null, null);

    /// <summary>Load the store beside <paramref name="appDirectory"/>. A missing or broken file yields an empty store.</summary>
    public static MapPlacementStore LoadFrom(string? appDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDirectory)) return Empty;

        string path = System.IO.Path.Combine(appDirectory, FileName);
        if (!File.Exists(path)) return new MapPlacementStore(new(StringComparer.OrdinalIgnoreCase), path, null);

        try
        {
            string json = File.ReadAllText(path);
            MapPlacementFile? file = JsonSerializer.Deserialize<MapPlacementFile>(json, JsonOptions);

            var placements = new Dictionary<string, MapPlacement>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, MapPlacementDto dto) in file?.Placements ?? new Dictionary<string, MapPlacementDto>())
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                placements[key] = new MapPlacement(dto.RotationIndex, dto.OffsetX, dto.OffsetY, dto.Scale);
            }

            return new MapPlacementStore(placements, path, null);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt file must not stop the editor opening; it is reported instead and overwritten on the next save.
            return new MapPlacementStore(new(StringComparer.OrdinalIgnoreCase), path, ex.Message);
        }
    }

    /// <summary>The saved placement for an arena, when there is one.</summary>
    public bool TryGet(string arenaKey, out MapPlacement placement) => _placements.TryGetValue(arenaKey, out placement!);

    /// <summary>Remember (or replace) the placement for an arena.</summary>
    public void Set(string arenaKey, MapPlacement placement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arenaKey);
        _placements[arenaKey] = placement;
    }

    /// <summary>Forget an arena's placement. Returns true when there was one.</summary>
    public bool Remove(string arenaKey) => !string.IsNullOrWhiteSpace(arenaKey) && _placements.Remove(arenaKey);

    /// <summary>Write the store to <paramref name="appDirectory"/>. Returns the path written.</summary>
    public string Save(string appDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);

        string path = System.IO.Path.Combine(appDirectory, FileName);
        var file = new MapPlacementFile
        {
            Version = CurrentVersion,
            Placements = _placements.ToDictionary(
                pair => pair.Key,
                pair => new MapPlacementDto
                {
                    RotationIndex = pair.Value.RotationIndex,
                    OffsetX = pair.Value.OffsetX,
                    OffsetY = pair.Value.OffsetY,
                    Scale = pair.Value.Scale,
                },
                StringComparer.OrdinalIgnoreCase),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        return path;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private sealed class MapPlacementFile
    {
        [JsonPropertyName("version")] public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("placements")] public Dictionary<string, MapPlacementDto> Placements { get; set; } = new();
    }

    private sealed class MapPlacementDto
    {
        [JsonPropertyName("rotationIndex")] public int RotationIndex { get; set; }

        [JsonPropertyName("offsetX")] public int OffsetX { get; set; }

        [JsonPropertyName("offsetY")] public int OffsetY { get; set; }

        [JsonPropertyName("scale")] public decimal Scale { get; set; } = 2.5m;
    }
}
