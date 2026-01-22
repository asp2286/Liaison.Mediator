using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Liaison.Mediator.Benchmarks.Infrastructure;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
public abstract class BenchmarkBase
{
    protected readonly Consumer Consumer = new();
    protected static readonly CancellationToken CancellationToken = CancellationToken.None;
}
