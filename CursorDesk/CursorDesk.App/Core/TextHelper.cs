namespace CursorDesk.Core
{
    public static class TextHelper
    {
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
