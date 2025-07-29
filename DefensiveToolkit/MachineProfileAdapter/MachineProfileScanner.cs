using DefensiveToolkit.Contracts.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DefensiveToolkit.MachineProfileAdapter;

public static class MachineProfileScanner
{
    public static MachineProfile Scan(ILogger? logger = null)
    {
        int cores = Environment.ProcessorCount;
        double ramGb = GetAvailableMemoryInGB(logger);
        return new MachineProfile { CpuCores = cores, RamGb = ramGb };
    }

    private static double GetAvailableMemoryInGB(ILogger? logger = null)
    {
        try
        {
            double? result = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                var memAvailableLine = lines.FirstOrDefault(x => x.StartsWith("MemAvailable:"));
                if (memAvailableLine != null)
                {
                    var parts = memAvailableLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (double.TryParse(parts[1], out var kb))
                        result = kb / 1024 / 1024; // Convert kB to GB
                }
            }

            if (result == null)
            {
                var memoryInfo = GC.GetGCMemoryInfo();
                var availableBytes = memoryInfo.TotalAvailableMemoryBytes;
                result = availableBytes / 1024d / 1024d / 1024d;
            }

            if (result is null or <= 0 or > 1024)
            {
                logger?.LogWarning("Memory detection returned suspicious value {Value} GB. Falling back to default 8 GB.", result);
                return 8;
            }

            return result.Value;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to detect available system memory. Falling back to default 8 GB.");
            return 8;
        }
    }
}