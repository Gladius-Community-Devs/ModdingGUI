using System.Text.RegularExpressions;

namespace ModdingGUI
{
    public static class PathHelper
    {
        // Enclose a file path in double quotes and escape batch-special characters
        public static string QuotePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "\"\"";
            }

            string escapedPath = path
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("<", "^<")
                .Replace(">", "^>")
                .Replace("|", "^|")
                .Replace("(", "^(")
                .Replace(")", "^)")
                .Replace("%", "%%")
                .Replace("\"", "\"\"");

            return $"\"{escapedPath}\"";
        }

        // Ensure a path ends with a single directory separator character
        public static string EnsureTrailingSeparator(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path += Path.DirectorySeparatorChar;
            return path;
        }

        // Normalize a path by replacing backslashes with forward slashes
        // and collapsing consecutive slashes
        public static string NormalizePath(string path)
        {
            path = path.Replace("\\", "/");
            path = Regex.Replace(path, "/{2,}", "/");
            return path;
        }
    }
}
