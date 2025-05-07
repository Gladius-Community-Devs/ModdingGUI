// Models/ProgramInfo.cs
namespace ModdingGUI.Models
{
    public class ProgramInfo
    {
        public string Version { get; set; }
        public string Author { get; set; }
        public string GitHubUrl { get; set; }

        public ProgramInfo()
        {
            // Default values
            Version = "2.3.2";
            Author = "BigBuda";
            GitHubUrl = "https://github.com/Gladius-Community-Devs/ModdingGUI/releases";
        }
    }
}
