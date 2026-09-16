using System;

namespace CursorDesk.Core
{
    public sealed class Session
    {
        public long Id { get; set; }

        public string Model { get; set; }

        public string Prompt { get; set; }

        public string Result { get; set; }

        /// <summary>
        /// Execution mode: "cloud" (Cursor Agent API) or "local" (local CLI agent).
        /// </summary>
        public string Mode { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
