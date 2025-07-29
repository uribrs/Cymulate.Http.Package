# DefensiveToolkit Core

The Core directory contains the fundamental components that orchestrate and coordinate the defensive policies. These components provide the infrastructure for policy chaining, request context management, and resource disposal.

## Overview

The Core components consist of:

- **DefensivePolicyRunner**: Main orchestrator for policy execution
- **RequestContextScope**: Manages HTTP request context for policy decisions
- **DisposableAction**: Utility for creating disposable actions

## DefensivePolicyRunner

**File**: `DefensivePolicyRunner.cs`

The DefensivePolicyRunner is the central orchestrator that chains multiple defensive policies together in a specific order to provide comprehensive protection for operations.

### Architecture

The runner implements the `IDefensivePolicyRunner` interface and provides a fluent, configurable way to execute operations through multiple resilience policies.

### Policy Execution Order

Policies are executed in the following order (outermost to innermost):

1. **Rate Limiter** - Controls the rate of operations
2. **Timeout** - Ensures operations don't hang indefinitely
3. **Retry** - Handles transient failures through retries
4. **Circuit Breaker** - Prevents cascading failures

This order is carefully designed to ensure:
- Rate limiting happens first to prevent system overload
- Timeouts are enforced to prevent hanging operations
- Retries can handle transient failures
- Circuit breakers protect against cascading failures

### Implementation Details

```csharp
public class DefensivePolicyRunner : IDefensivePolicyRunner
{
    private readonly IResiliencePolicy? _rateLimiter;
    private readonly IResiliencePolicy? _timeout;
    private readonly IResiliencePolicy? _retry;
    private readonly IResiliencePolicy? _circuitBreaker;
    private readonly ILogger<DefensivePolicyRunner>? _logger;
}
```

### Policy Chaining Logic

The runner uses functional composition to chain policies:

```csharp
private async Task<T> ExecuteChainedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
{
    var current = operation;

    // Chain policies from innermost to outermost
    if (_circuitBreaker is not null)
    {
        var inner = current;
        current = ct => _circuitBreaker.ExecuteAsync(inner, ct);
    }

    if (_retry is not null)
    {
        var inner = current;
        current = ct => _retry.ExecuteAsync(inner, ct);
    }

    if (_timeout is not null)
    {
        var inner = current;
        current = ct => _timeout.ExecuteAsync(inner, ct);
    }

    if (_rateLimiter is not null)
    {
        var inner = current;
        current = ct => _rateLimiter.ExecuteAsync(inner, ct);
    }

    return await current(cancellationToken);
}
```

### Usage Examples

#### Basic Usage

```csharp
// Create a simple runner with just retry and timeout
var runner = new DefensivePolicyRunner(
    timeout: new TimeoutPolicy(new TimeoutOptions { Timeout = TimeSpan.FromSeconds(30) }, logger),
    retry: new RetryPolicy(new RetryOptions { MaxAttempts = 3 }, logger),
    logger: logger
);

var result = await runner.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.GetAsync("https://api.example.com/data", ct);
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

#### Full Policy Chain

```csharp
// Create a comprehensive runner with all policies
var runner = new DefensivePolicyRunner(
    rateLimiter: new RateLimiterPolicy(rateLimiterOptions, logger),
    timeout: new TimeoutPolicy(new TimeoutOptions { Timeout = TimeSpan.FromSeconds(30) }, logger),
    retry: new RetryPolicy(new RetryOptions { MaxAttempts = 3 }, logger),
    circuitBreaker: new CircuitBreakerPolicy(new CircuitBreakerOptions { FailureThreshold = 5 }, logger),
    logger: logger
);

var result = await runner.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.PostAsync("https://api.example.com/process", content, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

#### Partial Policy Configuration

```csharp
// Only use rate limiting and circuit breaker
var runner = new DefensivePolicyRunner(
    rateLimiter: new RateLimiterPolicy(rateLimiterOptions, logger),
    circuitBreaker: new CircuitBreakerPolicy(circuitBreakerOptions, logger),
    logger: logger
);

var result = await runner.ExecuteAsync(async (ct) =>
{
    // Your operation here
    return await ProcessDataAsync(data, ct);
}, cancellationToken);
```

### Telemetry Integration

The runner provides comprehensive telemetry for monitoring policy execution:

```csharp
public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
{
    var activity = DefensiveDiagnostics.StartPolicyActivity("DefensivePolicyRunner", "ExecuteAsync");

    try
    {
        _logger?.LogDebug("[Runner] Executing policy chain: {Chain}",
            GetPolicyChainSummary());

        var result = await ExecuteChainedAsync(operation, cancellationToken);

        DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
        return result;
    }
    catch (Exception ex)
    {
        DefensiveDiagnostics.RecordFailure(activity, ex);
        DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);

        _logger?.LogError(ex, "[Runner] Operation failed through defensive policy chain.");
        throw;
    }
    finally
    {
        activity?.Dispose();
    }
}
```

### Policy Chain Summary

The runner provides a human-readable summary of the configured policy chain:

```csharp
private string GetPolicyChainSummary()
{
    var chain = new List<string>();

    if (_rateLimiter != null) chain.Add("RateLimiter");
    if (_timeout != null) chain.Add("Timeout");
    if (_retry != null) chain.Add("Retry");
    if (_circuitBreaker != null) chain.Add("CircuitBreaker");

    return chain.Count > 0 ? string.Join(" → ", chain) : "None";
}
```

Example outputs:
- `"RateLimiter → Timeout → Retry → CircuitBreaker"`
- `"Timeout → Retry"`
- `"None"`

## RequestContextScope

**File**: `RequestContextScope.cs`

The RequestContextScope provides a way to pass HTTP request context through the policy execution chain, enabling request-specific policy decisions.

### Implementation

```csharp
public static class RequestContextScope
{
    private static readonly AsyncLocal<HttpRequestMessage?> _current = new();
    public static HttpRequestMessage? Current => _current.Value;
    
    public static IDisposable With(HttpRequestMessage request)
    {
        var original = _current.Value;
        _current.Value = request;
        return new DisposableAction(() => _current.Value = original);
    }
}
```

### Usage Examples

#### Setting Request Context

```csharp
// Set request context for policy execution
using (RequestContextScope.With(httpRequestMessage))
{
    var result = await runner.ExecuteAsync(async (ct) =>
    {
        var response = await httpClient.SendAsync(httpRequestMessage, ct);
        return await response.Content.ReadAsStringAsync();
    }, cancellationToken);
}
```

#### Accessing Request Context in Policies

```csharp
// In a custom policy or rate limiter router
public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
{
    var request = RequestContextScope.Current;
    
    if (request?.RequestUri?.Host == "api.example.com")
    {
        // Apply stricter rate limiting for specific hosts
        return await strictRateLimiter.ExecuteAsync(operation, cancellationToken);
    }
    
    return await defaultRateLimiter.ExecuteAsync(operation, cancellationToken);
}
```

#### Rate Limiter Router Integration

```csharp
var routerPolicy = new RateLimiterRouterPolicy(
    globalPolicy: defaultRateLimiter,
    requestOverrideResolver: (request) =>
    {
        // Access request context to make policy decisions
        if (request?.RequestUri?.Host == "api.example.com")
            return strictRateLimiter;
        
        if (request?.Method == HttpMethod.Post)
            return conservativeRateLimiter;
            
        return null; // Use global policy
    }
);
```

### Thread Safety

The RequestContextScope uses `AsyncLocal<T>` to ensure thread safety in asynchronous operations:

- Each async execution context has its own request context
- Context is automatically scoped to the async operation
- No cross-contamination between concurrent operations

## DisposableAction

**File**: `DisposableAction.cs`

The DisposableAction is a utility class that wraps an action in an IDisposable interface, enabling the use of actions in using statements.

### Implementation

```csharp
public sealed class DisposableAction : IDisposable
{
    private readonly Action _onDispose;

    public DisposableAction(Action onDispose) => _onDispose = onDispose;

    public void Dispose() => _onDispose();
}
```

### Usage Examples

#### Basic Usage

```csharp
// Create a disposable action
using (new DisposableAction(() => Console.WriteLine("Cleanup completed")))
{
    // Perform some work
    Console.WriteLine("Performing work...");
}
// Cleanup completed
```

#### Request Context Management

```csharp
// Used internally by RequestContextScope
public static IDisposable With(HttpRequestMessage request)
{
    var original = _current.Value;
    _current.Value = request;
    return new DisposableAction(() => _current.Value = original);
}
```

#### Custom Resource Management

```csharp
// Custom resource cleanup
using (new DisposableAction(() => 
{
    // Cleanup code here
    _connection?.Close();
    _fileStream?.Dispose();
}))
{
    // Use resources
    await ProcessDataAsync();
}
```

## Best Practices

### Policy Runner Configuration

1. **Start Simple**: Begin with basic policies and add complexity as needed
2. **Consider Order**: Understand the policy execution order and its implications
3. **Monitor Performance**: Use telemetry to understand policy impact
4. **Test Combinations**: Verify policy combinations work correctly

### Request Context Usage

1. **Scope Appropriately**: Set request context only when needed
2. **Handle Null Values**: Always check for null when accessing request context
3. **Use Using Statements**: Ensure proper cleanup with using statements
4. **Thread Safety**: Be aware of async context boundaries

### Resource Management

1. **Always Dispose**: Use using statements for disposable resources
2. **Handle Exceptions**: Ensure cleanup happens even when exceptions occur
3. **Minimize Overhead**: Keep cleanup actions lightweight
4. **Test Cleanup**: Verify cleanup logic works correctly

## Performance Considerations

### Policy Chaining Overhead

- Each policy in the chain adds a small overhead
- Functional composition is efficient
- Consider removing unused policies to minimize overhead

### Request Context Overhead

- AsyncLocal access is very fast
- Context setting/clearing is minimal overhead
- No impact when context is not used

### Memory Usage

- Policy runner instances are lightweight
- Request context uses minimal memory
- DisposableAction has no memory overhead

## Troubleshooting

### Common Issues

1. **Policy Not Executing**: Check if policy is properly configured in runner
2. **Context Not Available**: Verify request context is set before policy execution
3. **Resource Leaks**: Ensure all disposable resources are properly disposed
4. **Performance Issues**: Monitor telemetry for policy overhead

### Debugging Tips

1. **Enable Logging**: Use structured logging to understand policy execution
2. **Check Chain Summary**: Review the policy chain summary in logs
3. **Monitor Telemetry**: Use OpenTelemetry to track policy execution
4. **Test Isolated**: Test policies individually before combining

### Performance Optimization

1. **Remove Unused Policies**: Don't configure policies you don't need
2. **Optimize Context Usage**: Only set request context when necessary
3. **Monitor Overhead**: Track policy execution times in telemetry
4. **Profile Under Load**: Test performance under realistic load conditions 