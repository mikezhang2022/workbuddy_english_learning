using System.Text;
using System.Text.RegularExpressions;

namespace CursorDesk.Core
{
    public static class TextHelper
    {
        // Matches ANSI/VT100 escape sequences like "\x1b[31m".
        private static readonly Regex AnsiRegex =
            new Regex(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

        /// <summary>
        /// Removes ANSI escape sequences and other control characters, keeping
        /// only printable text (plus \r, \n and \t).
        /// </summary>
        public static string StripAnsi(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var cleaned = AnsiRegex.Replace(text, string.Empty);
            var sb = new StringBuilder(cleaned.Length);
            foreach (var c in cleaned)
            {
                if (c == '\r' || c == '\n' || c == '\t' || c >= 0x20)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Quotes a single command-line argument using CommandLineToArgvW rules
        /// so the child process receives it exactly.
        /// </summary>
        public static string EncodeArgument(string arg)
        {
            if (arg == null)
            {
                arg = string.Empty;
            }

            if (arg.Length == 0)
            {
                return "\"\"";
            }

            var sb = new StringBuilder();
            sb.Append('"');
            for (var i = 0; i < arg.Length; i++)
            {
                var backslashes = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i == arg.Length)
                {
                    sb.Append('\\', backslashes * 2);
                }
                else if (arg[i] == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(arg[i]);
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        public static string TruncateOneLine(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var flat = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
            if (maxChars < 1 || flat.Length <= maxChars)
            {
                return flat;
            }

            return flat.Substring(0, maxChars) + "...";
        }

        public static string FormatAccount(string email, string name)
        {
            var e = (email ?? string.Empty).Trim();
            var n = (name ?? string.Empty).Trim();
            if (e.Length > 0 && n.Length > 0)
            {
                return n + " <" + e + ">";
            }

            if (e.Length > 0)
            {
                return e;
            }

            return n;
        }
    }
}
