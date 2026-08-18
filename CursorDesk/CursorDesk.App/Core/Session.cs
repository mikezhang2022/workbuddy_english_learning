using System;

namespace CursorDesk.Core
{
    public sealed class Session
    {
        public long Id { get; set; }

        public string Model { get; set; }

        public string Prompt { get; set; }

        public string Result { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
