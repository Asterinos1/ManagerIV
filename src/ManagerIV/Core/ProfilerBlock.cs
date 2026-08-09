using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ManagerIV.Core;

public readonly struct ProfilerBlock : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _blockName;
    private readonly Stopwatch _stopwatch;

    public ProfilerBlock(ILogger logger, string blockName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _blockName = blockName;
        _stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("START: {BlockName}", _blockName);
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _logger.LogInformation("END: {BlockName} completed in {ElapsedMilliseconds}ms", _blockName, _stopwatch.ElapsedMilliseconds);
    }
}
