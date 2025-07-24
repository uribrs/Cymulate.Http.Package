namespace DefensiveToolkit.Contracts.Options;

public class TimeoutOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}