namespace ModdingGUI.Core.Skills;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModdingGUI.Core.Api;

/// <summary>
/// Resolves the animation names a skill may use from its <c>SKILLUSECLASS</c> values.
/// </summary>
/// <remarks>
/// <para>
/// The chain is: skill → <c>SKILLUSECLASS: "Amazon"</c> → the class definition whose
/// <c>SKILLUSENAME</c> matches → its <c>MESH</c> → <c>data/characters/&lt;mesh&gt;/*.pan</c>
/// → the animation name is the file stem after the first underscore
/// (<c>amazon_actkick.pan</c> ⇒ <c>actkick</c>).
/// </para>
/// <para>
/// Note the indirection that trips people up: <c>SKILLUSECLASS</c> is compared against
/// <c>SKILLUSENAME</c>, not against the <c>CREATECLASS:</c> name. When a skill names several
/// classes the result is the intersection, so only animations every mesh can play are offered.
/// </para>
/// </remarks>
public static class SkillAnimationResolver
{
    /// <summary>Animations found for the given classes, plus why none were found.</summary>
    /// <param name="AnimationNames">Sorted animation names; empty when nothing resolved.</param>
    /// <param name="Error">Message explaining an empty result, otherwise null.</param>
    public sealed record Result(IReadOnlyList<string> AnimationNames, string? Error);

    /// <summary>
    /// Resolve animation names for one or more <c>SKILLUSECLASS</c> values.
    /// </summary>
    /// <param name="projectPath">The project's BEC folder (the root the Apis take).</param>
    /// <param name="skillUseClasses">Values of the skill's <c>SKILLUSECLASS</c> lines.</param>
    public static Result Resolve(string projectPath, IEnumerable<string> skillUseClasses)
    {
        ArgumentNullException.ThrowIfNull(skillUseClasses);

        var classNames = skillUseClasses
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().Trim('"'))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (classNames.Count == 0)
            return new Result(Array.Empty<string>(), null);   // caller decides the wording

        List<string> meshes;
        try
        {
            meshes = ResolveMeshes(projectPath, classNames);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return new Result(Array.Empty<string>(), $"could not read class definitions: {ex.Message}");
        }

        if (meshes.Count == 0)
            return new Result(Array.Empty<string>(),
                $"no MESH found for class(es): {string.Join(", ", classNames)}");

        string charactersRoot = Path.Combine(projectPath, "data", "characters");
        var perMesh = new List<HashSet<string>>();

        foreach (string mesh in meshes)
        {
            string meshDir = Path.Combine(charactersRoot, mesh);
            if (!Directory.Exists(meshDir)) continue;

            var animations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(meshDir, "*.pan");
            }
            catch (IOException)
            {
                continue;   // unreadable folder: leave it out of the intersection
            }

            foreach (string file in files)
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                int underscore = stem.IndexOf('_');
                if (underscore < 0) continue;   // base mesh animation, not an action
                animations.Add(stem.Substring(underscore + 1));
            }

            if (animations.Count > 0) perMesh.Add(animations);
        }

        if (perMesh.Count == 0)
            return new Result(Array.Empty<string>(),
                $"no character folders found for mesh(es): {string.Join(", ", meshes)}");

        IEnumerable<string> intersection = perMesh[0];
        for (int i = 1; i < perMesh.Count; i++)
        {
            var next = perMesh[i];
            intersection = intersection.Where(a => next.Contains(a));
        }

        var names = intersection.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        return new Result(names, names.Count == 0
            ? $"meshes {string.Join(", ", meshes)} share no animations"
            : null);
    }

    /// <summary>Meshes for the given <c>SKILLUSECLASS</c> values, via the class catalog API.</summary>
    private static List<string> ResolveMeshes(string projectPath, IReadOnlyList<string> classNames)
    {
        var api = new ClassDefsApi();
        var wanted = new HashSet<string>(classNames, StringComparer.OrdinalIgnoreCase);
        var meshes = new List<string>();

        foreach (var classDef in api.GetAll(projectPath))
        {
            string? skillUseName = classDef.Value("SKILLUSENAME")?.Trim().Trim('"');
            if (skillUseName is null || !wanted.Contains(skillUseName)) continue;

            string? mesh = classDef.Value("MESH")?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(mesh)) continue;
            if (meshes.Contains(mesh, StringComparer.OrdinalIgnoreCase)) continue;

            meshes.Add(mesh);
        }

        return meshes;
    }
}
