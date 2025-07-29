# DefensiveToolkit Telemetry

The DefensiveToolkit provides comprehensive telemetry and observability through OpenTelemetry integration. This system enables detailed monitoring, debugging, and performance analysis of all defensive policies.

## Overview

The telemetry system consists of two main components:

- **DefensiveTelemetryConstants**: Defines all telemetry tags, activity names, and outcome values
- **DefensiveDiagnostics**: Provides utility methods for creating activities, setting tags, and recording outcomes

## Activity Source

All telemetry is emitted through a single activity source:

```csharp
ActivitySourceName = "PowerHttp.DefensiveToolkit"
```

This ensures all defensive policy activities are correlated and can be filtered in monitoring systems.

## Telemetry Tags

### Core Tags

| Tag | Description | Example Values |
|-----|-------------|----------------|
| `policy.name` | Name of the policy being executed | `RetryPolicy`, `CircuitBreakerPolicy`, etc. |
| `policy.outcome` | Result of policy execution | `Success`, `Failure`, `Cancelled`, `Rejected` |
| `exception.type` | Type of exception that occurred | `HttpRequestException`, `TimeoutException` |
| `exception.message` | Exception message | `Connection timeout`, `Operation cancelled` |
| `exception.stacktrace` | Full exception stack trace | Full stack trace string |

### Policy-Specific Tags

#### Retry Policy
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `retry.attempts` | Number of retry attempts made | `0`, `1`, `2`, `3` |

#### Circuit Breaker Policy
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `circuit.state` | Current circuit breaker state | `Closed`, `Open`, `HalfOpen` |

#### Timeout Policy
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `timeout.threshold_ms` | Timeout threshold in milliseconds | `30000`, `5000`, `1000` |

#### Rate Limiter Policy
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.wait_duration_ms` | Time spent waiting for permit | `0`, `150`, `500`, `1000` |
| `rate_limiter.kind` | Type of rate limiter used | `FixedWindow`, `SlidingWindow`, `TokenBucket` |

### Rate Limiter Configuration Tags

#### Source and Context
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.source` | Source of rate limiter policy | `global`, `override` |
| `rate_limiter.policy_type` | Type of rate limiter policy | `RateLimiterPolicy`, `RateLimiterRouterPolicy` |
| `rate_limiter.has_request_context` | Whether request context is available | `true`, `false` |
| `rate_limiter.request_uri` | Request URI when context is available | `https://api.example.com/data` |
| `rate_limiter.request_method` | HTTP method when context is available | `GET`, `POST`, `PUT` |

#### Machine Profile Tags
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `machine.cpu_cores` | Number of CPU cores detected | `2`, `4`, `8`, `16` |
| `machine.ram_gb` | Available RAM in GB | `4.0`, `8.0`, `16.0`, `32.0` |
| `machine.category` | Machine category based on resources | `Low`, `Medium`, `High`, `Default` |

#### Rate Limiter Profile Tags
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.profile.category` | Selected profile category | `Low`, `Medium`, `High`, `Default` |
| `rate_limiter.profile.fixed_window_configured` | Whether fixed window is configured | `true`, `false` |
| `rate_limiter.profile.sliding_window_configured` | Whether sliding window is configured | `true`, `false` |
| `rate_limiter.profile.token_bucket_configured` | Whether token bucket is configured | `true`, `false` |

#### Fixed Window Configuration
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.fixed_window.permit_limit` | Number of permits per window | `10`, `20`, `30` |
| `rate_limiter.fixed_window.window_seconds` | Window duration in seconds | `60`, `30`, `15` |
| `rate_limiter.fixed_window.queue_limit` | Queue limit for waiting requests | `0`, `2`, `5`, `10` |

#### Sliding Window Configuration
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.sliding_window.permit_limit` | Number of permits per window | `8`, `18`, `25` |
| `rate_limiter.sliding_window.window_seconds` | Window duration in seconds | `30`, `60`, `120` |
| `rate_limiter.sliding_window.segments` | Number of segments per window | `3`, `5`, `6` |
| `rate_limiter.sliding_window.queue_limit` | Queue limit for waiting requests | `2`, `4`, `8` |

#### Token Bucket Configuration
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.token_bucket.capacity` | Token bucket capacity | `10`, `18`, `25` |
| `rate_limiter.token_bucket.refill_rate` | Token refill rate per second | `5.0`, `10.0`, `15.0` |
| `rate_limiter.token_bucket.queue_limit` | Queue limit for waiting requests | `2`, `4`, `6` |

#### Adaptive Rate Limiter Tags
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `rate_limiter.adaptive.category` | Adaptive category applied | `Low`, `Medium`, `High`, `Default` |
| `rate_limiter.adaptive.applied` | Whether adaptive profile was applied | `true`, `false` |
| `rate_limiter.adaptive.applied_count` | Number of times adaptive profile applied | `1`, `5`, `10` |

## Outcomes

The telemetry system tracks four main outcomes:

- **Success**: Operation completed successfully
- **Failure**: Operation failed with an exception
- **Cancelled**: Operation was cancelled
- **Rejected**: Operation was rejected (e.g., by rate limiter)

## Usage Examples

### Basic Activity Creation

```csharp
// Start a policy activity
var activity = DefensiveDiagnostics.StartPolicyActivity("RetryPolicy", "ExecuteAsync");

try
{
    // Execute your operation
    var result = await operation(cancellationToken);
    
    // Record success
    DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
    return result;
}
catch (Exception ex)
{
    // Record failure with exception details
    DefensiveDiagnostics.RecordFailure(activity, ex);
    DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);
    throw;
}
finally
{
    // Always dispose the activity
    activity?.Dispose();
}
```

### Setting Custom Tags

```csharp
var activity = DefensiveDiagnostics.StartPolicyActivity("CustomPolicy", "ExecuteAsync");

// Set custom tags
DefensiveDiagnostics.SetTag(activity, "custom.operation_type", "api_call");
DefensiveDiagnostics.SetTag(activity, "custom.endpoint", "/api/data");
DefensiveDiagnostics.SetTag(activity, "custom.user_id", userId);

// Set policy-specific tags
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RetryCount, retryCount);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.TimeoutThreshold, timeoutMs);
```

### Rate Limiter Telemetry

```csharp
var activity = DefensiveDiagnostics.StartPolicyActivity("RateLimiterPolicy", "ExecuteAsync");

// Track rate limiter configuration
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterKind, "FixedWindow");
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowPermitLimit, 100);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowWindowSeconds, 60);

// Track wait time
var stopwatch = Stopwatch.StartNew();
var lease = await _limiter.AcquireAsync(1, cancellationToken);
stopwatch.Stop();

DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterWait, stopwatch.ElapsedMilliseconds);
```

## Monitoring and Alerting

### Key Metrics to Monitor

1. **Policy Success Rates**
   - Track `policy.outcome` tag values
   - Alert on high failure rates

2. **Retry Attempts**
   - Monitor `retry.attempts` distribution
   - Alert on excessive retries

3. **Circuit Breaker State Changes**
   - Track `circuit.state` transitions
   - Alert when circuit opens frequently

4. **Rate Limiter Performance**
   - Monitor `rate_limiter.wait_duration_ms`
   - Alert on high wait times or rejections

5. **Timeout Occurrences**
   - Track timeout failures
   - Alert on frequent timeouts

### Example Queries

#### Prometheus/Grafana
```promql
# Policy success rate
rate(defensive_policy_outcomes_total{outcome="Success"}[5m]) / 
rate(defensive_policy_outcomes_total[5m])

# Average retry attempts
histogram_quantile(0.95, rate(retry_attempts_bucket[5m]))

# Circuit breaker state changes
rate(circuit_breaker_state_changes_total[5m])

# Rate limiter wait time
histogram_quantile(0.95, rate(rate_limiter_wait_duration_bucket[5m]))
```

#### Jaeger/Zipkin
```sql
-- Find slow operations
SELECT * FROM traces 
WHERE service_name = 'PowerHttp.DefensiveToolkit' 
AND duration > 5000000  -- 5 seconds

-- Find failed operations
SELECT * FROM traces 
WHERE service_name = 'PowerHttp.DefensiveToolkit' 
AND tags['policy.outcome'] = 'Failure'

-- Find retry patterns
SELECT * FROM traces 
WHERE service_name = 'PowerHttp.DefensiveToolkit' 
AND tags['retry.attempts'] > 0
```

## Integration with Application Insights

### Custom Dimensions

```csharp
// Add custom dimensions for Application Insights
var telemetryClient = new TelemetryClient();
var telemetry = new DependencyTelemetry
{
    Type = "Http",
    Target = "api.example.com",
    Data = "GET /api/data"
};

// Add defensive policy context
telemetry.Properties["policy.name"] = "RetryPolicy";
telemetry.Properties["retry.attempts"] = retryCount.ToString();
telemetry.Properties["policy.outcome"] = outcome;

telemetryClient.TrackDependency(telemetry);
```

### Custom Metrics

```csharp
// Track custom metrics
var metrics = new MetricTelemetry
{
    Name = "DefensivePolicy.RetryAttempts",
    Sum = retryCount
};

telemetryClient.TrackMetric(metrics);
```

## Performance Considerations

### Activity Sampling

The telemetry system respects OpenTelemetry sampling decisions:

```csharp
// Only set tags if activity data is requested
if (activity?.IsAllDataRequested == true)
{
    activity.SetTag("custom.tag", value);
}
```

### Memory Management

- Activities are automatically disposed when using `using` statements
- Tags are only set when sampling is enabled
- Exception details are captured efficiently

### Overhead Minimization

- Tag setting is conditional on sampling
- Exception stack traces are captured only when needed
- Activity creation is lightweight

## Best Practices

### Tag Naming

1. **Use Consistent Naming**: Follow the established tag naming conventions
2. **Avoid Sensitive Data**: Never include PII or secrets in tags
3. **Keep Tags Small**: Limit tag values to reasonable sizes
4. **Use Enums**: Use predefined outcome values for consistency

### Activity Management

1. **Always Dispose**: Use `using` statements or manually dispose activities
2. **Set Tags Early**: Set tags as soon as they're available
3. **Handle Exceptions**: Always record outcomes in try-catch blocks
4. **Use Descriptive Names**: Use meaningful activity and operation names

### Monitoring Setup

1. **Start Simple**: Begin with basic success/failure tracking
2. **Add Context**: Gradually add more tags for better debugging
3. **Set Alerts**: Configure alerts for critical failure patterns
4. **Review Regularly**: Periodically review telemetry data for insights

## Troubleshooting

### Common Issues

1. **Missing Activities**: Ensure activities are created for all policy executions
2. **Missing Tags**: Check that tags are set when sampling is enabled
3. **Memory Leaks**: Verify activities are properly disposed
4. **Performance Impact**: Monitor telemetry overhead in high-throughput scenarios

### Debugging Tips

1. **Enable Sampling**: Set sampling rate to 100% for debugging
2. **Check Tags**: Verify all expected tags are present
3. **Review Logs**: Check structured logs for additional context
4. **Test Isolated**: Test telemetry in isolation before integration 