using System;
using System.Collections.Generic;

namespace ModdingGUI
{
    /// <summary>
    /// Lightweight runtime representation of a parsed skill entry. Used by
    /// <see cref="SkillParser"/> and consumed by the Item Editor's skill dropdowns.
    /// </summary>
    public class SkillInfo
    {
        public string InternalName { get; init; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsItemSkill => InternalName.StartsWith("Item ", System.StringComparison.Ordinal);
        public override string ToString() => DisplayName;
    }
}
