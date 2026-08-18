using System;

namespace CursorDesk.Api
{
    public sealed class CursorApiException : Exception
    {
        public CursorApiException(string message)
            : this(message, null, null, null)
        {
        }

        public CursorApiException(string message, int? statusCode)
            : this(message, statusCode, null, null)
        {
        }

        public CursorApiException(string message, Exception inner)
            : this(message, null, null, inner)
        {
        }

        public CursorApiException(string message, int? statusCode, int? retryAfterSeconds, Exception inner)
            : base(message, inner)
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
        }

        public int? StatusCode { get; }

        public int? RetryAfterSeconds { get; }

        public static CursorApiException InvalidKey()
        {
            return new CursorApiException("API key invalid", 401);
        }

        public static CursorApiException RateLimited(int waitSeconds)
        {
            if (waitSeconds < 1)
            {
                waitSeconds = 1;
            }

            return new CursorApiException(
                "Rate limited, wait " + waitSeconds + "s",
                429,
                waitSeconds,
                null);
        }
    }
}
