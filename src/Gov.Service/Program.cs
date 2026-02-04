using Gov.Common;
using Gov.Service;

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              Build Reliability Governor v0.1.0                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Show initial system status
var memStatus = WindowsMemoryMetrics.GetMemoryStatus();
Console.WriteLine($"System Memory:");
Console.WriteLine($"  Physical: {memStatus.AvailablePhysicalGb:F1} GB available / {memStatus.TotalPhysicalGb:F1} GB total");
Console.WriteLine($"  Commit:   {memStatus.CommitChargeGb:F1} GB used / {memStatus.CommitLimitGb:F1} GB limit ({memStatus.CommitRatio:P0})");
Console.WriteLine();

// Show GPU status
var gpuStatus = GpuMetrics.GetAggregateStatus();
if (gpuStatus.Available)
{
    Console.WriteLine($"GPU Memory:");
    foreach (var gpu in gpuStatus.Gpus)
    {
        Console.WriteLine($"  [{gpu.Index}] {gpu.Name}");
        Console.WriteLine($"      VRAM: {gpu.FreeMemoryGb:F1} GB free / {gpu.TotalMemoryGb:F1} GB total ({gpu.MemoryUsageRatio:P0} used)");
        Console.WriteLine($"      Util: {gpu.UtilizationPercent}%  Temp: {gpu.TemperatureCelsius}°C");
    }
    Console.WriteLine();
}
else
{
    Console.WriteLine("GPU: Not detected (nvidia-smi not available)");
    Console.WriteLine();
}

// Create token pool with default config
var config = new TokenBudgetConfig
{
    GbPerToken = 2.0,
    SafetyReserveGb = 8.0,
    MinTokens = 1,
    MaxTokens = 32,
    CautionRatio = 0.80,
    SoftStopRatio = 0.88,
    HardStopRatio = 0.92
};

using var tokenPool = new TokenPool(config);
var status = tokenPool.GetStatus();

Console.WriteLine($"Token Pool:");
Console.WriteLine($"  Total tokens:    {status.TotalTokens}");
Console.WriteLine($"  Throttle level:  {status.ThrottleLevel}");
Console.WriteLine($"  Recommended -j:  {status.RecommendedParallelism}");
Console.WriteLine();

// Handle Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nShutting down...");
    cts.Cancel();
};

// Run pipe server
await using var server = new PipeServer(tokenPool);
await server.RunAsync(cts.Token);

Console.WriteLine("Governor stopped.");
