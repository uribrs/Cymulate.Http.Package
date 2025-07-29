using DefensiveToolkit.Contracts.Enums;
using DefensiveToolkit.Contracts.Models;
using DefensiveToolkit.Contracts.Options.RateLimiterKindOptions;

namespace DefensiveToolkit.Contracts
{
    public static class AdaptiveRateLimiterProfileSet
    {
        public static readonly IReadOnlyDictionary<MachineCategory, AdaptiveRateLimiterProfile> Profiles =
            new Dictionary<MachineCategory, AdaptiveRateLimiterProfile>
            {
                [MachineCategory.Low] = new AdaptiveRateLimiterProfile
                {
                    Category = MachineCategory.Low,

                    FixedWindow = new FixedWindowOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 2
                    },
                    SlidingWindow = new SlidingWindowOptions
                    {
                        PermitLimit = 8,
                        Window = TimeSpan.FromSeconds(30),
                        SegmentsPerWindow = 3,
                        QueueLimit = 2
                    },
                    TokenBucket = new TokenBucketOptions
                    {
                        TokenBucketCapacity = 10,
                        TokenBucketRefillRate = 5,
                        QueueLimit = 2
                    }
                },

                [MachineCategory.Medium] = new AdaptiveRateLimiterProfile
                {
                    Category = MachineCategory.Medium,

                    FixedWindow = new FixedWindowOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 5
                    },
                    SlidingWindow = new SlidingWindowOptions
                    {
                        PermitLimit = 18,
                        Window = TimeSpan.FromSeconds(30),
                        SegmentsPerWindow = 5,
                        QueueLimit = 4
                    },
                    TokenBucket = new TokenBucketOptions
                    {
                        TokenBucketCapacity = 18,
                        TokenBucketRefillRate = 10,
                        QueueLimit = 4
                    }
                },

                [MachineCategory.High] = new AdaptiveRateLimiterProfile
                {
                    Category = MachineCategory.High,

                    FixedWindow = new FixedWindowOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 10
                    },
                    SlidingWindow = new SlidingWindowOptions
                    {
                        PermitLimit = 25,
                        Window = TimeSpan.FromSeconds(30),
                        SegmentsPerWindow = 6,
                        QueueLimit = 8
                    },
                    TokenBucket = new TokenBucketOptions
                    {
                        TokenBucketCapacity = 25,
                        TokenBucketRefillRate = 15,
                        QueueLimit = 6
                    }
                },

                [MachineCategory.Default] = new AdaptiveRateLimiterProfile
                {
                    Category = MachineCategory.Default,

                    FixedWindow = new FixedWindowOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 4
                    },
                    SlidingWindow = new SlidingWindowOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromSeconds(30),
                        SegmentsPerWindow = 4,
                        QueueLimit = 3
                    },
                    TokenBucket = new TokenBucketOptions
                    {
                        TokenBucketCapacity = 12,
                        TokenBucketRefillRate = 6,
                        QueueLimit = 3
                    }
                }
            };
    }
}