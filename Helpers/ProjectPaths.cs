using System.IO;

namespace ModdingGUI
{
    internal static class ProjectPaths
    {
        public static string BecFolder(string projectFolder)
        {
            projectFolder = projectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(projectFolder, $"{Path.GetFileName(projectFolder)}_BEC");
        }

        public static string DataFolder(string projectFolder) =>
            Path.Combine(BecFolder(projectFolder), "data");

        public static string UnitsFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "units");

        public static string ConfigFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "config");

        public static string SchoolFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "school");

        public static string TownsFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "towns");

        public static string ScriptFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "script");

        public static string EncountersFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "encounters");

        public static string CharactersFolder(string projectFolder) =>
            Path.Combine(DataFolder(projectFolder), "characters");

        public static string GladiatorsFile(string projectFolder) =>
            Path.Combine(UnitsFolder(projectFolder), "gladiators.txt");

        public static string StatSetsFile(string projectFolder) =>
            Path.Combine(UnitsFolder(projectFolder), "statsets.txt");

        public static string ItemSetsFile(string projectFolder) =>
            Path.Combine(UnitsFolder(projectFolder), "itemsets.txt");

        public static string SkillSetsFile(string projectFolder) =>
            Path.Combine(UnitsFolder(projectFolder), "skillsets.txt");

        public static string ClassDefsFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "classdefs.tok");

        public static string ItemsTokFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "items.tok");

        public static string LookupTextFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "lookuptext_eng.txt");

        public static string SkillsTokFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "skills.tok");

        public static string SkillMeterTokFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "skillmeter.tok");

        public static string WorldmapFile(string projectFolder) =>
            Path.Combine(ConfigFolder(projectFolder), "worldmap.tok");

        public static string SchoolFile(string projectFolder, string fileName) =>
            Path.Combine(SchoolFolder(projectFolder), fileName);
    }
}
