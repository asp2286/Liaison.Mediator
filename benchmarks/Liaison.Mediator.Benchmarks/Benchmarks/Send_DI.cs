using BenchmarkDotNet.Attributes;
using Liaison.Mediator.Benchmarks.Infrastructure;
using Liaison.Mediator.Benchmarks.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Liaison.Mediator.Benchmarks.Benchmarks;

public class Send_DI : BenchmarkBase
{
    private Liaison.Mediator.IMediator? _liaisonMediator;
    private MediatR.IMediator? _mediatRMediator;
    private ServiceProvider? _liaisonProvider;
    private ServiceProvider? _mediatRProvider;
    private IServiceScope? _liaisonScope;
    private IServiceScope? _mediatRScope;
    private readonly Ping _request = new(42);

    [GlobalSetup]
    public void Setup()
    {
        var liaisonServices = new ServiceCollection();
        liaisonServices.AddScoped<Liaison.Mediator.IRequestHandler<Ping, Pong>, PingHandler>();
        liaisonServices.AddMediator();

        _liaisonProvider = liaisonServices.BuildServiceProvider();
        _liaisonScope = _liaisonProvider.CreateScope();
        _liaisonMediator = _liaisonScope.ServiceProvider.GetRequiredService<Liaison.Mediator.IMediator>();

        var mediatRServices = new ServiceCollection();
        mediatRServices.AddScoped<MediatR.IRequestHandler<Ping, Pong>, PingHandler>();
        mediatRServices.AddScoped<MediatR.INotificationPublisher, MediatR.NotificationPublishers.ForeachAwaitPublisher>();
        mediatRServices.AddScoped<MediatR.IMediator>(static provider =>
            new MediatR.Mediator(provider, provider.GetRequiredService<MediatR.INotificationPublisher>()));

        _mediatRProvider = mediatRServices.BuildServiceProvider();
        _mediatRScope = _mediatRProvider.CreateScope();
        _mediatRMediator = _mediatRScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _liaisonScope?.Dispose();
        _liaisonProvider?.Dispose();
        _mediatRScope?.Dispose();
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
