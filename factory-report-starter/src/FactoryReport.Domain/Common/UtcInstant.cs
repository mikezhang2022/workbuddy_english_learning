namespace FactoryReport.Domain.Common;

/// <summary>
/// UTC 时间戳值对象。领域层一律使用 UTC 存储语义，不依赖时区换算。
/// </summary>
public readonly struct UtcInstant : IEquatable<UtcInstant>, IComparable<UtcInstant>
{
    public DateTimeOffset Value { get; }

    public UtcInstant(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("UtcInstant requires Offset == 0 (UTC).", nameof(value));
        }

        Value = value;
    }

    public static UtcInstant FromUtc(DateTimeOffset utc) => new(utc.ToUniversalTime());

    public static UtcInstant FromUtcDateTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Local)
        {
            throw new ArgumentException("Local DateTime is not allowed; pass UTC Kind or Unspecified-as-UTC.", nameof(utcDateTime));
        }

        var normalized = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return new UtcInstant(new DateTimeOffset(normalized, TimeSpan.Zero));
    }

    public static UtcInstant Now() => new(DateTimeOffset.UtcNow);

    public bool Equals(UtcInstant other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is UtcInstant other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(UtcInstant other) => Value.CompareTo(other.Value);

    public static bool operator ==(UtcInstant left, UtcInstant right) => left.Equals(right);

    public static bool operator !=(UtcInstant left, UtcInstant right) => !left.Equals(right);

    public override string ToString() => Value.ToString("O");
}
