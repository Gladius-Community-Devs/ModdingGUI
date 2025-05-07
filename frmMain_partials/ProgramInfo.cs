// Models/ProgramInfo.cs
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

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
            Version = "2.4.1";
            Author = "BigBuda";
            GitHubUrl = "https://github.com/Gladius-Community-Devs/ModdingGUI/releases";
        }

        public async Task<string> GetLatestVersionAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string apiUrl = "https://api.github.com/repos/Gladius-Community-Devs/ModdingGUI/releases/latest";
                    client.DefaultRequestHeaders.Add("User-Agent", "ModdingGUI");
                    string response = await client.GetStringAsync(apiUrl);

                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        return doc.RootElement.GetProperty("tag_name").GetString();
                    }
                }
                catch
                {
                    return null; // Handle errors gracefully
                }
            }
        }
    }
}
