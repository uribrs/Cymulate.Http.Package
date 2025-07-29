# DefensiveToolkit

A modular, observable defensive policies library for robust HTTP workflows and beyond. Built on top of Polly, this toolkit provides comprehensive resilience patterns with built-in telemetry and observability.

## Overview

The DefensiveToolkit provides four core resilience policies that can be chained together to create robust, observable operations:

- **Retry Policy**: Automatically retry failed operations with configurable backoff strategies
- **Circuit Breaker**: Prevent cascading failures by temporarily stopping operations when a threshold is exceeded
- **Timeout Policy**: Ensure operations don't hang indefinitely
- **Rate Limiter**: Control the rate of operations with multiple algorithms (Fixed Window, Sliding Window, Token Bucket)

## Architecture

### Core Components

- **DefensivePolicyRunner**: Orchestrates policy execution in a configurable chain
- **IResiliencePolicy**: Common interface for all policies
- **Telemetry Integration**: Built-in OpenTelemetry support with comprehensive metrics and traces
- **Machine Profile Adaptation**: Automatic rate limiter configuration based on system resources

### Policy Execution Order

The policy chain executes in the following order (outermost to innermost):
1. Rate Limiter
2. Timeout
3. Retry
4. Circuit Breaker

This order ensures that rate limiting happens first, timeouts are enforced, retries can handle transient failures, and circuit breakers protect against cascading failures.

## Policies

### 1. Retry Policy

Automatically retries failed operations with configurable backoff strategies.

#### Use Cases
- Handling transient network failures
- Retrying API calls that occasionally fail
- Dealing with temporary service unavailability
- Managing flaky external dependencies

#### Configuration

```csharp
var retryOptions = new RetryOptions
{
    MaxAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(250),
    BackoffStrategy = RetryBackoff.ExponentialWithJitter,
    ShouldRetry = (exception, result) =>
    {
        // Custom retry logic
        return exception is HttpRequestException || 
               exception is TaskCanceledException;
    }
};

var retryPolicy = new RetryPolicy(retryOptions, logger);
```

#### Backoff Strategies

- **Fixed**: Constant delay between retries
- **Exponential**: Delay doubles with each retry (2^attempt)
- **ExponentialWithJitter**: Exponential delay with random jitter to prevent thundering herd

#### Telemetry Triggers

- **Activity**: `RetryPolicy.ExecuteAsync`
- **Tags**:
  - `retry.attempts`: Number of retry attempts made
  - `policy.outcome`: Success/Failure
- **Events**:
  - Retry attempt logged with warning level
  - Success after retries logged with info level
  - Final failure logged with error level

#### Example Usage

```csharp
var result = await retryPolicy.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.GetAsync("https://api.example.com/data", ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

### 2. Circuit Breaker Policy

Prevents cascading failures by temporarily stopping operations when a failure threshold is exceeded.

#### Use Cases
- Protecting downstream services from overload
- Preventing resource exhaustion
- Managing external API dependencies
- Handling service degradation gracefully

#### States

- **Closed**: Normal operation, requests pass through
- **Open**: Circuit is open, requests are rejected immediately
- **Half-Open**: Testing if the service has recovered

#### Configuration

```csharp
var circuitBreakerOptions = new CircuitBreakerOptions
{
    FailureThreshold = 5,
    BreakDuration = TimeSpan.FromSeconds(30),
    ShouldHandleException = (exception) =>
    {
        // Only trip circuit for specific exceptions
        return exception is HttpRequestException ||
               exception is TimeoutException;
    }
};

var circuitBreakerPolicy = new CircuitBreakerPolicy(circuitBreakerOptions, logger);
```

#### Telemetry Triggers

- **Activity**: `CircuitBreakerPolicy.ExecuteAsync`
- **Tags**:
  - `circuit.state`: Current circuit state (Closed/Open/HalfOpen)
  - `policy.outcome`: Success/Failure
- **Events**:
  - Circuit opening logged with warning level
  - Circuit reset logged with info level
  - Half-open state logged with info level

#### Example Usage

```csharp
var result = await circuitBreakerPolicy.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.PostAsync("https://api.example.com/process", content, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

### 3. Timeout Policy

Ensures operations don't hang indefinitely by enforcing time limits.

#### Use Cases
- Preventing resource leaks from hanging operations
- Ensuring responsive user experiences
- Managing external API timeouts
- Protecting against slow external dependencies

#### Configuration

```csharp
var timeoutOptions = new TimeoutOptions
{
    Timeout = TimeSpan.FromSeconds(30)
};

var timeoutPolicy = new TimeoutPolicy(timeoutOptions, logger);
```

#### Telemetry Triggers

- **Activity**: `TimeoutPolicy.ExecuteAsync`
- **Tags**:
  - `timeout.threshold_ms`: Timeout threshold in milliseconds
  - `policy.outcome`: Success/Failure
- **Events**:
  - Timeout exceeded logged with warning level
  - Unhandled exceptions logged with error level

#### Example Usage

```csharp
var result = await timeoutPolicy.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.GetAsync("https://slow-api.example.com/data", ct);
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

### 4. Rate Limiter Policy

Controls the rate of operations using various algorithms to prevent overwhelming systems.

#### Use Cases
- Protecting APIs from abuse
- Managing resource consumption
- Implementing fair usage policies
- Controlling load on downstream services

#### Rate Limiter Types

##### Fixed Window
- Simple time-based window (e.g., 100 requests per minute)
- Resets completely at window boundaries
- May allow bursts at window edges

```csharp
var fixedWindowOptions = new FixedWindowOptions
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 10
};

var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.FixedWindow,
    FixedWindow = fixedWindowOptions
};
```

##### Sliding Window
- Smooths out traffic over time
- Uses segments to provide more granular control
- Better for preventing bursts

```csharp
var slidingWindowOptions = new SlidingWindowOptions
{
    PermitLimit = 100,
    Window = TimeSpan.FromMinutes(1),
    SegmentsPerWindow = 6, // 10-second segments
    QueueLimit = 10
};

var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.SlidingWindow,
    SlidingWindow = slidingWindowOptions
};
```

##### Token Bucket
- Allows bursts up to bucket capacity
- Refills at a constant rate
- Good for handling traffic spikes

```csharp
var tokenBucketOptions = new TokenBucketOptions
{
    TokenBucketCapacity = 100,
    TokenBucketRefillRate = 10, // tokens per second
    QueueLimit = 10
};

var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.TokenBucket,
    TokenBucket = tokenBucketOptions
};
```

#### Telemetry Triggers

- **Activity**: `RateLimiterPolicy.ExecuteAsync`
- **Tags**:
  - `rate_limiter.wait_duration_ms`: Time spent waiting for permit
  - `rate_limiter.kind`: Type of rate limiter used
  - `policy.outcome`: Success/Rejected/Failure
- **Events**:
  - Request rejection logged with warning level
  - Long wait times (>500ms) logged with warning level
  - Operation failures logged with error level

#### Example Usage

```csharp
var rateLimiterPolicy = new RateLimiterPolicy(rateLimiterOptions, logger);

var result = await rateLimiterPolicy.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.GetAsync("https://api.example.com/data", ct);
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

## Advanced Features

### Machine Profile Adaptation

The toolkit automatically adapts rate limiter configurations based on system resources:

```csharp
// Automatically detect machine capabilities and select appropriate profile
var profile = RateLimiterAdaptor.GetProfile(logger);

var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.FixedWindow,
    FixedWindow = profile.FixedWindow
};
```

#### Machine Categories

- **Low**: ≤2 CPU cores or ≤4GB RAM
- **Medium**: ≤4 CPU cores or ≤8GB RAM  
- **High**: >4 CPU cores and >8GB RAM
- **Default**: Fallback configuration

### Request-Specific Rate Limiting

Use the `RateLimiterRouterPolicy` to apply different rate limits based on request context:

```csharp
var routerPolicy = new RateLimiterRouterPolicy(
    globalPolicy: defaultRateLimiter,
    requestOverrideResolver: (request) =>
    {
        if (request?.RequestUri?.Host == "api.example.com")
            return strictRateLimiter;
        
        if (request?.Method == HttpMethod.Post)
            return conservativeRateLimiter;
            
        return null; // Use global policy
    }
);
```

### Policy Chaining

Combine multiple policies for comprehensive protection:

```csharp
var runner = new DefensivePolicyRunner(
    rateLimiter: rateLimiterPolicy,
    timeout: timeoutPolicy,
    retry: retryPolicy,
    circuitBreaker: circuitBreakerPolicy,
    logger: logger
);

var result = await runner.ExecuteAsync(async (ct) =>
{
    var response = await httpClient.GetAsync("https://api.example.com/data", ct);
    return await response.Content.ReadAsStringAsync();
}, cancellationToken);
```

## Telemetry and Observability

### OpenTelemetry Integration

The toolkit provides comprehensive telemetry through OpenTelemetry:

- **Activity Source**: `PowerHttp.DefensiveToolkit`
- **Activities**: Each policy execution creates a span
- **Tags**: Rich metadata about policy configuration and execution
- **Events**: Structured logging with correlation IDs

### Key Metrics

- Policy execution success/failure rates
- Retry attempt counts
- Circuit breaker state transitions
- Rate limiter wait times and rejections
- Timeout occurrences

### Example Telemetry Output

```
Activity: RetryPolicy.ExecuteAsync
Tags:
  - policy.name: RetryPolicy
  - retry.attempts: 2
  - policy.outcome: Success
  - exception.type: HttpRequestException
  - exception.message: Connection timeout

Activity: CircuitBreakerPolicy.ExecuteAsync  
Tags:
  - policy.name: CircuitBreakerPolicy
  - circuit.state: Open
  - policy.outcome: Failure

Activity: RateLimiterPolicy.ExecuteAsync
Tags:
  - policy.name: RateLimiterPolicy
  - rate_limiter.wait_duration_ms: 150
  - rate_limiter.kind: FixedWindow
  - policy.outcome: Success
```

## Best Practices

### Policy Configuration

1. **Start Conservative**: Begin with conservative settings and adjust based on monitoring
2. **Monitor Telemetry**: Use the built-in telemetry to understand system behavior
3. **Test Failure Scenarios**: Verify policies work correctly under failure conditions
4. **Consider Dependencies**: Understand how policies interact with each other

### Rate Limiting Guidelines

1. **Fixed Window**: Use for simple rate limiting with predictable traffic patterns
2. **Sliding Window**: Use when you need to smooth out traffic and prevent bursts
3. **Token Bucket**: Use when you need to handle traffic spikes gracefully
4. **Adaptive Profiles**: Leverage machine profile adaptation for automatic configuration

### Circuit Breaker Tuning

1. **Failure Threshold**: Start with 5-10 failures before opening
2. **Break Duration**: Use 30-60 seconds for most scenarios
3. **Exception Filtering**: Only trip on exceptions that indicate service issues
4. **Monitor State Changes**: Track circuit state transitions in telemetry

### Retry Strategy Selection

1. **Fixed Backoff**: Use for simple retry scenarios
2. **Exponential Backoff**: Use for transient failures that may take time to resolve
3. **Exponential with Jitter**: Use in distributed systems to prevent thundering herd
4. **Custom Retry Logic**: Implement specific retry conditions for your use case

## Dependencies

- **Polly**: Core resilience library
- **OpenTelemetry**: Telemetry and observability
- **Microsoft.Extensions.Logging**: Structured logging
- **System.Threading.RateLimiting**: Rate limiting algorithms

## Getting Started

1. Install the package:
```bash
dotnet add package Cymulate.Http.Package.DefensiveToolkit
```

2. Configure policies:
```csharp
var retryPolicy = new RetryPolicy(new RetryOptions { MaxAttempts = 3 });
var timeoutPolicy = new TimeoutPolicy(new TimeoutOptions { Timeout = TimeSpan.FromSeconds(30) });
```

3. Create a policy runner:
```csharp
var runner = new DefensivePolicyRunner(retry: retryPolicy, timeout: timeoutPolicy);
```

4. Execute operations:
```csharp
var result = await runner.ExecuteAsync(async (ct) => await YourOperation(ct));
```

The DefensiveToolkit provides a robust foundation for building resilient, observable applications with minimal configuration and maximum flexibility. 