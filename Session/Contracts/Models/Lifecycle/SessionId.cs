namespace Session.Contracts.Models.Lifecycle;

public readonly struct SessionId : IEquatable<SessionId>
{
    public Guid Value { get; }

    public SessionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty", nameof(value));

        Value = value;
    }

    public static SessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();

    public bool Equals(SessionId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is SessionId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator Guid(SessionId id) => id.Value;
    public static explicit operator SessionId(Guid value) => new(value);
}
