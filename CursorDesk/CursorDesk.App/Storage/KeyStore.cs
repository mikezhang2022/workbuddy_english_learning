using System;
using System.IO;

namespace CursorDesk.Storage
{
    public static class KeyStore
    {
        public const string FileName = "key.txt";

        public static string KeyFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        public static bool TryRead(out string apiKey, out string error)
        {
            apiKey = null;
            error = null;

            var path = KeyFilePath;
            if (!File.Exists(path))
            {
                error = "Missing " + FileName + ". Place your Cursor API key (one line) next to the executable:\n" + path;
                return false;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                error = "Could not read " + FileName + ": " + ex.Message;
                return false;
            }

            apiKey = (text ?? string.Empty).Trim();
            if (apiKey.Length == 0)
            {
                error = FileName + " is empty. Put your Cursor API key on the first line.";
                return false;
            }

            return true;
        }
    }
}
