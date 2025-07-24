using System.Runtime.CompilerServices;

namespace Authentication.Logic.Validation;

public static class Validate
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression("value")] string? name = null)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(name);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression("value")] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", name);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotDefault<T>(
        T value,
        [CallerArgumentExpression("value")] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(value, default!))
            throw new ArgumentException("Value cannot be the default value.", name);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<T> NotEmpty<T>(
        IEnumerable<T>? value,
        [CallerArgumentExpression("value")] string? name = null)
    {
        if (value is null || !value.Any())
            throw new ArgumentException("Collection cannot be null or empty.", name);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Valid<T>(
        T value,
        Func<T, bool> predicate,
        string errorMessage,
        [CallerArgumentExpression("value")] string? name = null)
    {
        if (!predicate(value))
            throw new ArgumentException(errorMessage, name);
        return value;
    }
}