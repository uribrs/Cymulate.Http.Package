using System.Diagnostics;
using DefensiveToolkit.Contracts.Enums;

namespace DefensiveToolkit.Telemetry;

public static class DefensiveDiagnostics
{
    private static readonly ActivitySource ActivitySource =
        new(DefensiveTelemetryConstants.ActivitySourceName);

    public static Activity? StartPolicyActivity(string policyName, string operationName)
    {
        var activity = ActivitySource.StartActivity($"{policyName}.{operationName}", ActivityKind.Internal);

        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(DefensiveTelemetryConstants.Tags.Policy, policyName);
        }

        return activity;
    }

    public static void SetTag(Activity? activity, string key, object? value)
    {
        if (activity?.IsAllDataRequested == true)
            activity.SetTag(key, value);
    }

    public static void RecordFailure(Activity? activity, Exception ex)
    {
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag(DefensiveTelemetryConstants.Tags.ExceptionType, ex.GetType().FullName);
        activity.SetTag(DefensiveTelemetryConstants.Tags.ExceptionMessage, ex.Message);
        activity.SetTag(DefensiveTelemetryConstants.Tags.ExceptionStackTrace, ex.StackTrace);
    }

    public static void RecordOutcome(Activity? activity, string outcome)
    {
        if (activity?.IsAllDataRequested == true)
            activity.SetTag(DefensiveTelemetryConstants.Tags.Outcome, outcome);
    }
}