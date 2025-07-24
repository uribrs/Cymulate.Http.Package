namespace DefensiveToolkit.Contracts.Enums;

public enum RetryBackoff 
{ 
    Fixed, 
    Exponential,
    ExponentialWithJitter
}