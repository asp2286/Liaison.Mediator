using BenchmarkDotNet.Attributes;
using Liaison.Mediator.Benchmarks.Infrastructure;
using Liaison.Mediator.Benchmarks.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Liaison.Mediator.Benchmarks.Benchmarks;

public class Send_SingleHandler : BenchmarkBase
{
    private Liaison.Mediator.IMediator? _liaisonMediator;
    private MediatR.IMediator? _mediatRMediator;
    private ServiceProvider? _mediatRProvider;
    private readonly Ping _request = new(42);

    [GlobalSetup]
    public void Setup()
    {
        var liaisonBuilder = new Liaison.Mediator.MediatorBuilder()
            .RegisterRequestHandler<Ping, Pong>(new PingHandler());
        _liaisonMediator = liaisonBuilder.Build();

        var services = new ServiceCollection();
        services.AddSingleton<MediatR.IRequestHandler<Ping, Pong>, PingHandler>();
        services.AddSingleton<MediatR.INotificationPublisher, MediatR.NotificationPublishers.ForeachAwaitPublisher>();
        services.AddSingleton<MediatR.IMediator>(static provider =>
            new MediatR.Mediator(provider, provider.GetRequiredService<MediatR.INotificationPublisher>()));

        _mediatRProvider = services.BuildServiceProvider();
        _mediatRMediator = _mediatRProvider.GetRequiredService<MediatR.IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _mediatRProvider?.Dispose();
    }

    [Benchmark]
    public async Task Liaison_Send()
    {
        var response = await _liaisonMediator!.Send(_request, CancellationToken).ConfigureAwait(false);
        Consumer.Consume(response);
    }

    [Benchmark]
    public async Task MediatR_Send()
    {
        var response = await _mediatRMediator!.Send(_request, CancellationToken).ConfigureAwait(false);
        Consumer.Consume(response);
    }
}
