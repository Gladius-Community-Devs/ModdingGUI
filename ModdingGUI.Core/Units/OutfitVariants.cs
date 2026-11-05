using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModdingGUI.Core.Api;

namespace ModdingGUI.Core.Units;

/// <summary>
/// How many outfit variants a class has, read from <c>data/characters/&lt;mesh&gt;/*_varNN.ptx</c>.
/// </summary>
/// <remarks>
/// <para>
/// A gladiator's <c>Outfit</c> is the outfit variant to wear: <c>0</c> is the plain model and
/// <c>1..N</c> select <c>var01..varNN</c> in the class's mesh folder (vanilla classes have 4, 5 or 6
/// variants; 68 classes have 4). Nothing in the game files lists the valid set — the folder is the list —
/// which is why an invalid outfit is a crash risk rather than a validator's guess: the engine asks for a
/// variant that does not exist.
/// </para>
/// <para>
/// Every one of the 2,170 vanilla gladiators stays within its class's variant count, which is what makes
/// this rule safe to enforce.
/// </para>
/// </remarks>
public sealed class OutfitVariants
{
    private static readonly Regex VariantFile = new(@"_var(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _bec;
    private readonly IClassesApi _classes;
    private readonly Dictionary<string, int?> _byClass = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int>? _byMesh;

    /// <summary>Read variants for one project.</summary>
    public OutfitVariants(string projectRootOrBec, IClassesApi classes)
    {
        _bec = ProjectRoots.ResolveBecFolder(projectRootOrBec);
        _classes = classes;
    }

    /// <summary>
    /// Highest variant number for a class, or null when it cannot be determined (unknown class, no mesh
    /// folder, or the class file lists no mesh).
    /// </summary>
    public int? TryGetVariantCount(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;
        if (_byClass.TryGetValue(className, out int? cached)) return cached;

        int? count = null;
        try
        {
            var def = _classes.Get(_bec, className);
            string? mesh = def?.Values("MESH").FirstOrDefault()?.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(mesh))
            {
                _byMesh ??= IndexMeshes();
                if (_byMesh.TryGetValue(mesh!, out int variants)) count = variants;
            }
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException)
        {
            count = null;
        }

        _byClass[className] = count;
        return count;
    }

    /// <summary>True when <paramref name="outfit"/> exists for the class; null when it cannot be checked.</summary>
    public bool? IsValid(string className, int outfit)
    {
        int? variants = TryGetVariantCount(className);
        if (variants is null) return null;
        return outfit >= 0 && outfit <= variants.Value;
    }

    /// <summary>Mesh folder name (lower-case) to its highest variant number.</summary>
    private Dictionary<string, int> IndexMeshes()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string root = Path.Combine(_bec, "data", "characters");
        if (!Directory.Exists(root)) return result;

        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            int highest = 0;
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                Match match = VariantFile.Match(Path.GetFileName(file));
                if (!match.Success) continue;

                if (int.TryParse(match.Groups[1].Value, out int number) && number > highest)
                    highest = number;
            }

            if (highest > 0) result[Path.GetFileName(directory)] = highest;
        }

        return result;
    }
}
