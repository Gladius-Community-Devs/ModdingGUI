namespace ModdingGUI.Tests.Core.Grids;

using ModdingGUI.Core.Grids;
using Xunit;

/// <summary>
/// Tests for <see cref="MapPlacementStore"/>: the per-arena map alignment that a user can correct and have remembered.
/// </summary>
/// <remarks>
/// The alignment is not derivable from the .grd data - the original app hand-tuned ten arenas and left the rest, and two
/// attempts to infer it from the wall cells did not reproduce those values - so being able to save what somebody tuned by
/// eye, and keep it across restarts, is the feature.
/// </remarks>
public class MapPlacementStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"placements-{Guid.NewGuid():N}");

    public MapPlacementStoreTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void LoadFrom_MissingFile_IsAnEmptyStoreRatherThanAnError()
    {
        var store = MapPlacementStore.LoadFrom(_folder);

        Assert.Empty(store.All);
        Assert.Null(store.Problem);
    }

    [Fact]
    public void LoadFrom_NullDirectory_DoesNotThrow()
    {
        Assert.Empty(MapPlacementStore.LoadFrom(null).All);
        Assert.Empty(MapPlacementStore.Empty.All);
    }

    [Fact]
    public void SetThenSaveThenLoad_RoundTripsEveryField()
    {
        var store = MapPlacementStore.LoadFrom(_folder);
        store.Set("arenasuren", new MapPlacement(RotationIndex: 2, OffsetX: 50, OffsetY: -75, Scale: 2.5m));

        string path = store.Save(_folder);
        var reloaded = MapPlacementStore.LoadFrom(_folder);

        Assert.True(File.Exists(path));
        Assert.True(reloaded.TryGet("arenasuren", out MapPlacement placement));
        Assert.Equal(2, placement.RotationIndex);
        Assert.Equal(50, placement.OffsetX);
        Assert.Equal(-75, placement.OffsetY);
        Assert.Equal(2.5m, placement.Scale);
    }

    [Fact]
    public void ArenaKeysAreCaseInsensitive()
    {
        var store = MapPlacementStore.LoadFrom(_folder);
        store.Set("ArenaSuren", new MapPlacement(4, 10, 20, 2.5m));

        Assert.True(store.TryGet("arenasuren", out _));
        Assert.True(store.TryGet("ARENASUREN", out _));
    }

    [Fact]
    public void Remove_ForgetsOnlyThatArena()
    {
        var store = MapPlacementStore.LoadFrom(_folder);
        store.Set("arenasuren", new MapPlacement(0, 0, 0, 2.5m));
        store.Set("pirgosarena", new MapPlacement(2, 50, -75, 2.5m));

        Assert.True(store.Remove("arenasuren"));
        Assert.False(store.Remove("arenasuren"));

        Assert.False(store.TryGet("arenasuren", out _));
        Assert.True(store.TryGet("pirgosarena", out _));
    }

    [Fact]
    public void CorruptFile_IsReportedButDoesNotThrow()
    {
        File.WriteAllText(Path.Combine(_folder, MapPlacementStore.FileName), "{ this is not json");

        var store = MapPlacementStore.LoadFrom(_folder);

        Assert.Empty(store.All);
        Assert.NotNull(store.Problem);
    }

    [Fact]
    public void SavedFileIsReadableByHand()
    {
        // The file is meant to be editable and shippable, so the shape matters: camelCase, indented, keyed by arena.
        var store = MapPlacementStore.LoadFrom(_folder);
        store.Set("arenasuren", new MapPlacement(5, -12, 34, 2.25m));
        string path = store.Save(_folder);
        string json = File.ReadAllText(path);

        Assert.Contains("\"placements\"", json, StringComparison.Ordinal);
        Assert.Contains("\"arenasuren\"", json, StringComparison.Ordinal);
        Assert.Contains("\"rotationIndex\": 5", json, StringComparison.Ordinal);
        Assert.Contains("\"offsetX\": -12", json, StringComparison.Ordinal);
        Assert.Contains('\n', json);   // indented, not one long line
    }

    [Fact]
    public void RotationIndex_KeepsTheFlipValues()
    {
        // Indices 0-3 are the original rotations; 4 and 5 are Flip H / Flip V, which rotation alone cannot express.
        var store = MapPlacementStore.LoadFrom(_folder);
        store.Set("mirrored", new MapPlacement(RotationIndex: 4, OffsetX: 0, OffsetY: 0, Scale: 2.5m));

        store.Save(_folder);
        var reloaded = MapPlacementStore.LoadFrom(_folder);

        Assert.True(reloaded.TryGet("mirrored", out MapPlacement placement));
        Assert.Equal(4, placement.RotationIndex);
    }
}
