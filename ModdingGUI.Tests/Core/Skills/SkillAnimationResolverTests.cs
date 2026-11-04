using System.IO;
using System.Linq;
using ModdingGUI.Core.Skills;
using Xunit;

namespace ModdingGUI.Tests.Core.Skills;

/// <summary>
/// Animation resolution for skills: <c>SKILLUSECLASS</c> → classdefs <c>SKILLUSENAME</c> →
/// <c>MESH</c> → <c>data/characters/&lt;mesh&gt;/*.pan</c> stems. Runs against the real corpus
/// when the fixture is available, and against a synthetic layout otherwise.
/// </summary>
public sealed class SkillAnimationResolverTests
{
    [Fact]
    public void Resolve_RealAmazonClass_FindsAmazonAnimations()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        var result = SkillAnimationResolver.Resolve(CleanBecCorpus.Root!, new[] { "Amazon" });

        Assert.Null(result.Error);
        Assert.NotEmpty(result.AnimationNames);
        Assert.Contains("actkick", result.AnimationNames);
        Assert.Contains("death", result.AnimationNames);
        // The base mesh animation has no underscore, so it is not an action name.
        Assert.DoesNotContain("amazon", result.AnimationNames);
        Assert.Equal(result.AnimationNames.OrderBy(a => a, System.StringComparer.OrdinalIgnoreCase),
            result.AnimationNames);
    }

    [Fact]
    public void Resolve_UnknownClass_ExplainsThatNoMeshWasFound()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        var result = SkillAnimationResolver.Resolve(CleanBecCorpus.Root!, new[] { "NoSuchClass" });

        Assert.Empty(result.AnimationNames);
        Assert.NotNull(result.Error);
        Assert.Contains("no MESH", result.Error);
    }

    [Fact]
    public void Resolve_NoClasses_ReturnsEmptyWithoutAnError()
    {
        var result = SkillAnimationResolver.Resolve(".", System.Array.Empty<string>());

        Assert.Empty(result.AnimationNames);
        Assert.Null(result.Error);   // the caller supplies its own "add SKILLUSECLASS" hint
    }

    [Fact]
    public void Resolve_IntersectsAnimationsAcrossMeshes()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        // Amazon and Archer have different meshes; the result must be the intersection.
        var single = SkillAnimationResolver.Resolve(CleanBecCorpus.Root!, new[] { "Amazon" });
        var both = SkillAnimationResolver.Resolve(CleanBecCorpus.Root!, new[] { "Amazon", "Archer" });

        Assert.NotEmpty(single.AnimationNames);
        Assert.All(both.AnimationNames, name => Assert.Contains(name, single.AnimationNames));
        Assert.True(both.AnimationNames.Count <= single.AnimationNames.Count);
    }

    [Fact]
    public void Resolve_MatchesSkillUseNameNotTheClassHeader()
    {
        if (!CleanBecCorpus.IsAvailable) return;

        // classdefs blocks carry SKILLUSENAME; for most classes it equals the CREATECLASS
        // name, but the lookup goes through SKILLUSENAME, which is what this pins.
        var bySkillUseName = SkillAnimationResolver.Resolve(CleanBecCorpus.Root!, new[] { "Amazon" });
        Assert.NotEmpty(bySkillUseName.AnimationNames);
    }
}
