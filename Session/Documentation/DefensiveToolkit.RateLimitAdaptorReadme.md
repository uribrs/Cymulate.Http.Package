# Machine Profile Adapter

The Machine Profile Adapter provides intelligent, adaptive rate limiting based on system resources. It automatically detects machine capabilities and applies appropriate rate limiter configurations to optimize performance across different deployment environments.

## Overview

The Machine Profile Adapter consists of two main components:

- **MachineProfileScanner**: Detects system resources (CPU cores, RAM)
- **RateLimiterAdaptor**: Selects appropriate rate limiter profiles based on machine capabilities

## Machine Categorization

The adapter categorizes machines into four categories based on their resources:

### Low Performance
- **CPU**: ≤2 cores
- **RAM**: ≤4GB
- **Use Case**: Development machines, small VMs, containers with limited resources

### Medium Performance
- **CPU**: ≤4 cores OR ≤8GB RAM
- **Use Case**: Standard application servers, medium-sized VMs

### High Performance
- **CPU**: >4 cores AND >8GB RAM
- **Use Case**: Production servers, large VMs, dedicated hardware

### Default
- **Fallback**: Used when categorization fails or for unknown configurations
- **Use Case**: Safe default for any environment

## Machine Profile Detection

### CPU Detection

```csharp
// Uses Environment.ProcessorCount for CPU core detection
int cores = Environment.ProcessorCount;
```

### Memory Detection

The system uses different approaches based on the operating system:

#### Linux
```csharp
// Reads /proc/meminfo for available memory
var lines = File.ReadAllLines("/proc/meminfo");
var memAvailableLine = lines.FirstOrDefault(x => x.StartsWith("MemAvailable:"));
if (memAvailableLine != null)
{
    var parts = memAvailableLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (double.TryParse(parts[1], out var kb))
        result = kb / 1024 / 1024; // Convert kB to GB
}
```

#### Windows/Other
```csharp
// Uses GC.GetGCMemoryInfo for available memory
var memoryInfo = GC.GetGCMemoryInfo();
var availableBytes = memoryInfo.TotalAvailableMemoryBytes;
result = availableBytes / 1024d / 1024d / 1024d;
```

### Fallback Behavior

If memory detection fails or returns suspicious values:
- Logs a warning with the detected value
- Falls back to 8GB as a safe default
- Continues operation with the default profile

## Rate Limiter Profiles

### Low Performance Profile

Optimized for resource-constrained environments:

```csharp
[MachineCategory.Low] = new AdaptiveRateLimiterProfile
{
    Category = MachineCategory.Low,
    
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 10,        // Conservative limit
        Window = TimeSpan.FromSeconds(60),
        QueueLimit = 2           // Small queue
    },
    
    SlidingWindow = new SlidingWindowOptions
    {
        PermitLimit = 8,         // Even more conservative
        Window = TimeSpan.FromSeconds(30),
        SegmentsPerWindow = 3,   // Fewer segments for efficiency
        QueueLimit = 2
    },
    
    TokenBucket = new TokenBucketOptions
    {
        TokenBucketCapacity = 10,
        TokenBucketRefillRate = 5,  // 5 tokens per second
        QueueLimit = 2
    }
}
```

### Medium Performance Profile

Balanced configuration for standard environments:

```csharp
[MachineCategory.Medium] = new AdaptiveRateLimiterProfile
{
    Category = MachineCategory.Medium,
    
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 20,        // Moderate limit
        Window = TimeSpan.FromSeconds(60),
        QueueLimit = 5           // Medium queue
    },
    
    SlidingWindow = new SlidingWindowOptions
    {
        PermitLimit = 18,        // Slightly lower than fixed window
        Window = TimeSpan.FromSeconds(30),
        SegmentsPerWindow = 5,   // More segments for better smoothing
        QueueLimit = 4
    },
    
    TokenBucket = new TokenBucketOptions
    {
        TokenBucketCapacity = 18,
        TokenBucketRefillRate = 10,  // 10 tokens per second
        QueueLimit = 4
    }
}
```

### High Performance Profile

Optimized for high-capacity environments:

```csharp
[MachineCategory.High] = new AdaptiveRateLimiterProfile
{
    Category = MachineCategory.High,
    
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 30,        // Higher limit
        Window = TimeSpan.FromSeconds(60),
        QueueLimit = 10          // Larger queue
    },
    
    SlidingWindow = new SlidingWindowOptions
    {
        PermitLimit = 25,        // Balanced with fixed window
        Window = TimeSpan.FromSeconds(30),
        SegmentsPerWindow = 6,   // More segments for precision
        QueueLimit = 8
    },
    
    TokenBucket = new TokenBucketOptions
    {
        TokenBucketCapacity = 25,
        TokenBucketRefillRate = 15,  // 15 tokens per second
        QueueLimit = 6
    }
}
```

### Default Profile

Safe fallback configuration:

```csharp
[MachineCategory.Default] = new AdaptiveRateLimiterProfile
{
    Category = MachineCategory.Default,
    
    FixedWindow = new FixedWindowOptions
    {
        PermitLimit = 15,        // Conservative default
        Window = TimeSpan.FromSeconds(60),
        QueueLimit = 4
    },
    
    SlidingWindow = new SlidingWindowOptions
    {
        PermitLimit = 12,        // Conservative default
        Window = TimeSpan.FromSeconds(30),
        SegmentsPerWindow = 4,   // Balanced segments
        QueueLimit = 3
    },
    
    TokenBucket = new TokenBucketOptions
    {
        TokenBucketCapacity = 12,
        TokenBucketRefillRate = 6,   // 6 tokens per second
        QueueLimit = 3
    }
}
```

## Usage Examples

### Basic Usage

```csharp
// Automatically detect machine capabilities and get appropriate profile
var profile = RateLimiterAdaptor.GetProfile(logger);

// Use the profile to configure rate limiter
var rateLimiterOptions = new RateLimiterOptions
{
    Kind = RateLimiterKind.FixedWindow,
    FixedWindow = profile.FixedWindow
};

var rateLimiterPolicy = new RateLimiterPolicy(rateLimiterOptions, logger);
```

### With Custom Logger

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

var profile = RateLimiterAdaptor.GetProfile(logger);

logger.LogInformation("Selected {Category} profile for rate limiting", profile.Category);
```

### Manual Machine Profile Creation

```csharp
// Create a custom machine profile
var machineProfile = new MachineProfile
{
    CpuCores = 8,
    RamGb = 16.0
};

// Use the profile directly
var profile = AdaptiveRateLimiterProfileSet.Profiles[MachineCategory.High];
```

## Telemetry Integration

The Machine Profile Adapter provides comprehensive telemetry for monitoring and debugging:

### Machine Detection Telemetry

```csharp
// Machine capabilities are tracked
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineCpuCores, machine.CpuCores);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineRamGb, machine.RamGb);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineCategory, category.ToString());
```

### Profile Selection Telemetry

```csharp
// Profile details are tracked
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileCategory, profile.Category.ToString());
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileFixedWindowConfigured, profile.FixedWindow is not null);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileSlidingWindowConfigured, profile.SlidingWindow is not null);
DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileTokenBucketConfigured, profile.TokenBucket is not null);
```

### Configuration Telemetry

For each configured rate limiter type, detailed configuration is tracked:

#### Fixed Window Configuration
```csharp
if (profile.FixedWindow is not null)
{
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowPermitLimit, profile.FixedWindow.PermitLimit);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowWindowSeconds, profile.FixedWindow.Window.TotalSeconds);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowQueueLimit, profile.FixedWindow.QueueLimit);
}
```

#### Sliding Window Configuration
```csharp
if (profile.SlidingWindow is not null)
{
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowPermitLimit, profile.SlidingWindow.PermitLimit);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowWindowSeconds, profile.SlidingWindow.Window.TotalSeconds);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowSegments, profile.SlidingWindow.SegmentsPerWindow);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowQueueLimit, profile.SlidingWindow.QueueLimit);
}
```

#### Token Bucket Configuration
```csharp
if (profile.TokenBucket is not null)
{
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketCapacity, profile.TokenBucket.TokenBucketCapacity);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketRefillRate, profile.TokenBucket.TokenBucketRefillRate);
    DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketQueueLimit, profile.TokenBucket.QueueLimit);
}
```

## Best Practices

### Profile Selection

1. **Start with Auto-Detection**: Use `RateLimiterAdaptor.GetProfile()` for automatic configuration
2. **Monitor Performance**: Use telemetry to understand how profiles perform in your environment
3. **Customize When Needed**: Override profiles only when auto-detection doesn't meet your needs
4. **Test Across Environments**: Verify profiles work correctly in all deployment environments

### Memory Detection

1. **Handle Failures Gracefully**: The system falls back to safe defaults when detection fails
2. **Monitor Warnings**: Pay attention to memory detection warnings in logs
3. **Verify in Containers**: Test memory detection in containerized environments
4. **Consider Resource Limits**: Be aware of container resource limits that may affect detection

### Configuration Tuning

1. **Conservative Defaults**: Start with conservative settings and adjust based on monitoring
2. **Monitor Queue Usage**: Track queue utilization to optimize queue limits
3. **Balance Permit Limits**: Ensure permit limits are appropriate for your traffic patterns
4. **Consider Burst Handling**: Use token bucket for environments with traffic spikes

## Troubleshooting

### Common Issues

1. **Incorrect Categorization**: Check CPU and RAM detection in logs
2. **Memory Detection Failures**: Verify system access to memory information
3. **Profile Not Applied**: Ensure profile is properly used in rate limiter configuration
4. **Performance Issues**: Monitor telemetry for rate limiter performance

### Debugging Tips

1. **Enable Logging**: Use structured logging to understand profile selection
2. **Check Telemetry**: Review telemetry data for machine capabilities and profile details
3. **Test Manually**: Create manual profiles to test specific configurations
4. **Monitor Metrics**: Track rate limiter performance metrics in your monitoring system

### Environment-Specific Considerations

#### Docker Containers
- Memory detection may be limited by container resource limits
- CPU detection works correctly with container CPU limits
- Consider using custom profiles for containerized environments

#### Virtual Machines
- Memory detection works well in most VM environments
- CPU detection reflects VM CPU allocation
- Profiles are generally appropriate for VM deployments

#### Kubernetes
- Resource detection respects pod resource limits
- Consider using custom profiles for different pod sizes
- Monitor resource usage to optimize profiles

## Performance Considerations

### Detection Overhead
- CPU detection is instantaneous (uses `Environment.ProcessorCount`)
- Memory detection involves file I/O on Linux or GC calls on Windows
- Detection is cached per application instance

### Profile Selection
- Profile selection is O(1) dictionary lookup
- No performance impact during rate limiter operation
- Telemetry overhead is minimal and conditional

### Memory Usage
- Profiles are static and shared across all instances
- No additional memory overhead during operation
- Configuration objects are lightweight 