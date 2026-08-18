using System;
using System.Text.RegularExpressions;

namespace CursorDesk.Api
{
    /// <summary>
    /// Helpers for resolving the GitHub URL where Cursor landed generated files.
    /// Cursor commits to a branch named "cursor/{suffix}" and reports the branch name
    /// inside the run result text (e.g. "...提交到分支 cursor/source-hello-file-f6ea").
    /// We parse that branch name and build the tree URL for its source/ folder.
    /// </summary>
    public static class GitHubHelper
    {
        // Matches "cursor/xxx" branch tokens that Cursor creates.
        private static readonly Regex BranchRegex =
            new Regex(@"cursor/[A-Za-z0-9_\-]+", RegexOptions.Compiled);

        public static bool TryParseRepoUrl(string repoUrl, out string owner, out string repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                return false;
            }

            // Accept forms:
            //   https://github.com/owner/repo
            //   https://github.com/owner/repo.git
            //   owner/repo
            var s = repoUrl.Trim();
            if (s.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring("https://github.com/".Length);
            }
            else if (s.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring("github.com/".Length);
            }

            s = s.TrimEnd('/');
            if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 4);
            }

            var parts = s.Split('/');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            owner = parts[0];
            repo = parts[1];
            return true;
        }

        /// <summary>
        /// Extracts the first "cursor/..." branch token from a Cursor run result string.
        /// Returns null if none found.
        /// </summary>
        public static string ParseBranchName(string resultText)
        {
            if (string.IsNullOrWhiteSpace(resultText))
            {
                return null;
            }

            var m = BranchRegex.Match(resultText);
            return m.Success ? m.Value : null;
        }

        /// <summary>
        /// Builds the GitHub web URL of the source/ folder on the given branch.
        /// branch is expected as "cursor/xxx" (with prefix) or "xxx" (without).
        /// </summary>
        public static string BuildSourceUrl(string repoUrl, string branch)
        {
            if (!TryParseRepoUrl(repoUrl, out var owner, out var repo) || string.IsNullOrWhiteSpace(branch))
            {
                return null;
            }

            var branchName = branch.StartsWith("cursor/", StringComparison.OrdinalIgnoreCase)
                ? branch
                : "cursor/" + branch;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "https://github.com/{0}/{1}/tree/{2}/source",
                owner,
                repo,
                Uri.EscapeDataString(branchName));
        }
    }
}
