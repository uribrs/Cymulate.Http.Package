namespace DefensiveToolkit.Contracts.Models;

public sealed record MachineProfile
{
    public int CpuCores { get; init; }
    public double RamGb { get; init; }
}