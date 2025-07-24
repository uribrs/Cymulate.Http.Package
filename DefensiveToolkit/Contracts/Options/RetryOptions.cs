using DefensiveToolkit.Contracts.Enums;

namespace DefensiveToolkit.Contracts.Options;

public class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// A predicate to determine whether to retry based on the exception or result.
    /// </summary>
    public Func<Exception?, object?, bool>? ShouldRetry { get; set; }
    public RetryBackoff BackoffStrategy { get; set; } = RetryBackoff.Fixed;
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(250);
}