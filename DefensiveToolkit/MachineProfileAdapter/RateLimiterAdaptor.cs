using DefensiveToolkit.Contracts;
using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Models;
using DefensiveToolkit.Telemetry;
using Microsoft.Extensions.Logging;

namespace DefensiveToolkit.MachineProfileAdapter;

public static class RateLimiterAdaptor
{
    public static AdaptiveRateLimiterProfile GetProfile(ILogger? logger = null)
    {
        var activity = DefensiveDiagnostics.StartPolicyActivity("RateLimiterAdaptor", "GetProfile");
        
        try
        {
            var machine = MachineProfileScanner.Scan(logger);
            var category = Categorize(machine);

            // Track machine profile information
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineCpuCores, machine.CpuCores);
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineRamGb, machine.RamGb);
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.MachineCategory, category.ToString());

            logger?.LogInformation("RateLimiterAdaptor selected {Category} profile for {Cores} cores, {Ram:F1} GB RAM", category, machine.CpuCores, machine.RamGb);

            var profile = AdaptiveRateLimiterProfileSet.Profiles.TryGetValue(category, out var selectedProfile)
                ? selectedProfile
                : AdaptiveRateLimiterProfileSet.Profiles[MachineCategory.Default];

            // Track the selected profile details
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileCategory, profile.Category.ToString());
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileFixedWindowConfigured, profile.FixedWindow is not null);
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileSlidingWindowConfigured, profile.SlidingWindow is not null);
            DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterProfileTokenBucketConfigured, profile.TokenBucket is not null);

            if (profile.FixedWindow is not null)
            {
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowPermitLimit, profile.FixedWindow.PermitLimit);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowWindowSeconds, profile.FixedWindow.Window.TotalSeconds);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterFixedWindowQueueLimit, profile.FixedWindow.QueueLimit);
            }

            if (profile.SlidingWindow is not null)
            {
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowPermitLimit, profile.SlidingWindow.PermitLimit);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowWindowSeconds, profile.SlidingWindow.Window.TotalSeconds);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowSegments, profile.SlidingWindow.SegmentsPerWindow);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterSlidingWindowQueueLimit, profile.SlidingWindow.QueueLimit);
            }

            if (profile.TokenBucket is not null)
            {
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketCapacity, profile.TokenBucket.TokenBucketCapacity);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketRefillRate, profile.TokenBucket.TokenBucketRefillRate);
                DefensiveDiagnostics.SetTag(activity, DefensiveTelemetryConstants.Tags.RateLimiterTokenBucketQueueLimit, profile.TokenBucket.QueueLimit);
            }

            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Success);
            return profile;
        }
        catch (Exception ex)
        {
            DefensiveDiagnostics.RecordFailure(activity, ex);
            DefensiveDiagnostics.RecordOutcome(activity, DefensiveTelemetryConstants.Outcomes.Failure);
            logger?.LogError(ex, "Failed to get adaptive rate limiter profile");
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static MachineCategory Categorize(MachineProfile machine)
    {
        if (machine.CpuCores <= 2 || machine.RamGb <= 4)
            return MachineCategory.Low;

        if (machine.CpuCores <= 4 || machine.RamGb <= 8)
            return MachineCategory.Medium;

        return MachineCategory.High;
    }
}
