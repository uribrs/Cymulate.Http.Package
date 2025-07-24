namespace DefensiveToolkit.Contracts.Options;

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional filter to determine whether a failure should trip the circuit.
    /// </summary>
    public Func<Exception, bool>? ShouldHandleException { get; set; }
}