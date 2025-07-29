# DefensiveToolkit Policies

This directory contains the core resilience policy implementations for the DefensiveToolkit. Each policy implements the `IResiliencePolicy` interface and provides specific defensive capabilities.

## Policy Overview

The DefensiveToolkit provides four main policy types, each designed to handle specific failure scenarios:

1. **RetryPolicy** - Handles transient failures through automatic retries
2. **CircuitBreakerPolicy** - Prevents cascading failures by temporarily stopping operations
3. **TimeoutPolicy** - Ensures operations don't hang indefinitely
4. **RateLimiterPolicy** - Controls the rate of operations to prevent system overload

## Policy Implementation Details

### RetryPolicy

**File**: `RetryPolicy.cs`

The RetryPolicy automatically retries failed operations with configurable backoff strategies. It's built on top of Polly's retry capabilities with enhanced telemetry.

#### Key Features

- **Configurable Backoff Strategies**: Fixed, Exponential, and Exponential with Jitter
- **Custom Retry Logic**: Define when to retry based on exceptions or results
- **Comprehensive Telemetry**: Track retry attempts and outcomes
- **Cancellation Support**: Respects cancellation tokens throughout retry attempts

#### Implementation Highlights

```csharp
// Custom retry logic example
var retryOptions = new RetryOptions
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(250),
    BackoffStrategy = RetryBackoff.ExponentialWithJitter,
    ShouldRetry = (exception, result) =>
    {
        // Retry on network exceptions or specific HTTP status codes
        if (exception is HttpRequestException) return true;
        if (result is HttpResponseMessage response)
            return response.StatusCode == HttpStatusCode.TooManyRequests;
        return false;
    }
};
```

#### Telemetry Events

- **Retry Attempt**: Logged when a retry occurs (Warning level)
- **Success After Retries**: Logged when operation succeeds after retries (Info level)
- **Final Failure**: Logged when all retries are exhausted (Error level)

### CircuitBreakerPolicy

**File**: `CircuitBreakerPolicy.cs`

The CircuitBreakerPolicy implements the Circuit Breaker pattern to prevent cascading failures. It temporarily stops operations when a failure threshold is exceeded.

#### Key Features

- **Three States**: Closed (normal), Open (blocking), Half-Open (testing)
- **Configurable Thresholds**: Set failure count and break duration
- **Exception Filtering**: Define which exceptions should trip the circuit
- **State Change Events**: Comprehensive logging of state transitions

#### Implementation Highlights

```csharp
// Circuit breaker with custom exception filtering
var circuitBreakerOptions = new CircuitBreakerOptions
{
    FailureThreshold = 5,
    BreakDuration = TimeSpan.FromSeconds(30),
    ShouldHandleException = (exception) =>
    {
        // Only trip circuit for network or timeout exceptions
        return exception is HttpRequestException ||
               exception is TimeoutException ||
               exception is TaskCanceledException;
    }
};
```

#### State Management

- **Closed → Open**: When failure threshold is reached
- **Open → Half-Open**: After break duration expires
- **Half-Open → Closed**: When a successful operation occurs
- **Half-Open → Open**: When another failure occurs

#### Telemetry Events

- **Circuit Opening**: Logged when circuit transitions to Open state (Warning level)
- **Circuit Reset**: Logged when circuit transitions to Closed state (Info level)
- **Half-Open Testing**: Logged when circuit enters Half-Open state (Info level)

### TimeoutPolicy

**File**: `TimeoutPolicy.cs`

The TimeoutPolicy ensures operations don't hang indefinitely by enforcing time limits. It uses cancellation tokens to implement timeouts.

#### Key Features

- **Configurable Timeout**: Set timeout duration per operation
- **Cancellation Token Integration**: Properly handles cancellation
- **Exception Wrapping**: Wraps timeout exceptions for clarity
- **Resource Cleanup**: Ensures proper disposal of resources

#### Implementation Highlights

```csharp
// Timeout with custom duration
var timeoutOptions = new TimeoutOptions
{
    Timeout = TimeSpan.FromSeconds(30)
};

// The policy creates a linked cancellation token source
// that combines the provided cancellation token with the timeout
```

#### Exception Handling

- **TimeoutException**: Thrown when operation exceeds timeout
- **OperationCanceledException**: Handled when timeout occurs
- **Other Exceptions**: Passed through unchanged

#### Telemetry Events

- **Timeout Exceeded**: Logged when operation times out (Warning level)
- **Unhandled Exception**: Logged for other exceptions (Error level)

### RateLimiterPolicy

**File**: `RateLimiterPolicy.cs`

The RateLimiterPolicy controls the rate of operations using various algorithms to prevent system overload. It supports multiple rate limiting strategies.

#### Key Features

- **Multiple Algorithms**: Fixed Window, Sliding Window, Token Bucket
- **Queue Management**: Configurable queue limits and processing order
- **Wait Time Tracking**: Measures time spent waiting for permits
- **Resource Disposal**: Implements IAsyncDisposable for cleanup

#### Rate Limiter Types

##### Fixed Window
```csharp
var fixedWindowOptions = new FixedWindowOptions
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 10,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    AutoReplenishment = true
};
```

##### Sliding Window
```csharp
var slidingWindowOptions = new SlidingWindowOptions
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    SegmentsPerWindow = 6, // 10-second segments
    QueueLimit = 10,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    AutoReplenishment = true
};
```

##### Token Bucket
```csharp
var tokenBucketOptions = new TokenBucketOptions
{
    TokenBucketCapacity = 100,
    TokenBucketRefillRate = 10, // tokens per second
    QueueLimit = 10,
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    AutoReplenishment = true
};
```

#### Performance Considerations

- **Wait Time Monitoring**: Logs warnings for wait times > 500ms
- **Queue Management**: Configurable queue limits prevent memory issues
- **Resource Cleanup**: Proper disposal of rate limiter resources

#### Telemetry Events

- **Request Rejection**: Logged when queue limit is exceeded (Warning level)
- **Long Wait Time**: Logged when wait time > 500ms (Warning level)
- **Operation Failure**: Logged when operation fails after acquiring permit (Error level)

### NoOpPolicy

**File**: `NoOpPolicy.cs`

The NoOpPolicy is a placeholder implementation that does nothing. It's used when a policy is not needed but the interface requires an implementation.

#### Use Cases

- **Optional Policies**: When a policy is conditionally applied
- **Testing**: When you need to disable a policy for testing
- **Default Values**: As a safe default when no policy is specified

## Policy Configuration

### Common Configuration Patterns

#### Basic Configuration
```csharp
// Simple retry with default settings
var retryPolicy = new RetryPolicy(new RetryOptions(), logger);

// Simple timeout
var timeoutPolicy = new TimeoutPolicy(new TimeoutOptions(), logger);

// Simple circuit breaker
var circuitBreakerPolicy = new CircuitBreakerPolicy(new CircuitBreakerOptions(), logger);
```

#### Advanced Configuration
```csharp
// Retry with exponential backoff and custom logic
var retryOptions = new RetryOptions
{
    MaxAttempts = 5,
    Delay = TimeSpan.FromMilliseconds(100),
    BackoffStrategy = RetryBackoff.ExponentialWithJitter,
    ShouldRetry = (ex, result) => ex is HttpRequestException
};

// Circuit breaker with specific exception handling
var circuitBreakerOptions = new CircuitBreakerOptions
{
    FailureThreshold = 3,
    BreakDuration = TimeSpan.FromSeconds(60),
    ShouldHandleException = (ex) => ex is HttpRequestException
};

// Rate limiter with sliding window
var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.SlidingWindow,
    SlidingWindow = new SlidingWindowOptions
    {
        PermitLimit = 50,
        Window = TimeSpan.FromSeconds(30),
        SegmentsPerWindow = 3,
        QueueLimit = 5
    }
};
```

## Policy Chaining

Policies can be combined using the `DefensivePolicyRunner`:

```csharp
var runner = new DefensivePolicyRunner(
    rateLimiter: new RateLimiterPolicy(rateLimiterOptions, logger),
    timeout: new TimeoutPolicy(timeoutOptions, logger),
    retry: new RetryPolicy(retryOptions, logger),
    circuitBreaker: new CircuitBreakerPolicy(circuitBreakerOptions, logger),
    logger: logger
);

var result = await runner.ExecuteAsync(async (ct) =>
{
    // Your operation here
    return await httpClient.GetAsync("https://api.example.com/data", ct);
}, cancellationToken);
```

## Testing Policies

### Unit Testing
```csharp
[Test]
public async Task RetryPolicy_ShouldRetryOnException()
{
    var retryOptions = new RetryOptions { MaxAttempts = 3 };
    var policy = new RetryPolicy(retryOptions, logger);
    
    var attemptCount = 0;
    var operation = new Func<CancellationToken, Task<string>>(async (ct) =>
    {
        attemptCount++;
        if (attemptCount < 3)
            throw new HttpRequestException("Transient failure");
        return "success";
    });
    
    var result = await policy.ExecuteAsync(operation, CancellationToken.None);
    
    Assert.That(result, Is.EqualTo("success"));
    Assert.That(attemptCount, Is.EqualTo(3));
}
```

### Integration Testing
```csharp
[Test]
public async Task PolicyChain_ShouldHandleComplexScenario()
{
    var runner = new DefensivePolicyRunner(
        retry: new RetryPolicy(new RetryOptions { MaxAttempts = 2 }, logger),
        timeout: new TimeoutPolicy(new TimeoutOptions { Timeout = TimeSpan.FromSeconds(5) }, logger)
    );
    
    var result = await runner.ExecuteAsync(async (ct) =>
    {
        await Task.Delay(100, ct); // Simulate work
        return "success";
    }, CancellationToken.None);
    
    Assert.That(result, Is.EqualTo("success"));
}
```

## Best Practices

### Policy Selection

1. **Start Simple**: Begin with basic policies and add complexity as needed
2. **Monitor Performance**: Use telemetry to understand policy impact
3. **Test Failure Scenarios**: Verify policies work under stress
4. **Consider Interactions**: Understand how policies affect each other

### Configuration Guidelines

1. **Retry Policy**: Use exponential backoff for network operations
2. **Circuit Breaker**: Set conservative thresholds and monitor state changes
3. **Timeout Policy**: Set timeouts based on SLA requirements
4. **Rate Limiter**: Choose algorithm based on traffic patterns

### Performance Considerations

1. **Resource Management**: Ensure proper disposal of policies
2. **Memory Usage**: Monitor queue sizes in rate limiters
3. **CPU Overhead**: Consider policy complexity in high-throughput scenarios
4. **Telemetry Impact**: Balance observability with performance

## Troubleshooting

### Common Issues

1. **Policy Not Working**: Check if policy is properly configured and chained
2. **Performance Degradation**: Monitor telemetry for policy overhead
3. **Resource Leaks**: Ensure proper disposal of rate limiter policies
4. **Unexpected Behavior**: Verify policy configuration and order

### Debugging Tips

1. **Enable Logging**: Use structured logging to understand policy behavior
2. **Monitor Telemetry**: Use OpenTelemetry to track policy execution
3. **Test Isolated**: Test policies individually before combining
4. **Review Configuration**: Double-check policy settings and thresholds 