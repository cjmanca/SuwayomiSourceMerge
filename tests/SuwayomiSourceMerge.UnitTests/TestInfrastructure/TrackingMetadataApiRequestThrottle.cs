using System;
using System.Threading;
using System.Threading.Tasks;

using SuwayomiSourceMerge.Infrastructure.Metadata;

namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// A test double for <see cref="IMetadataApiRequestThrottle"/> that tracks execution counts.
/// </summary>
internal sealed class TrackingMetadataApiRequestThrottle : IMetadataApiRequestThrottle
{
    private int _callCount;

    /// <summary>
    /// Gets the number of times the throttle was executed.
    /// </summary>
    public int CallCount => _callCount;

    /// <inheritdoc />
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return operation(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return operation(cancellationToken);
    }
}
