using BenchmarkDotNet.Attributes;
using Liaison.Mediator.Benchmarks.Infrastructure;
using Liaison.Mediator.Benchmarks.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Liaison.Mediator.Benchmarks.Benchmarks;

public class Publish_DI : BenchmarkBase
{
    private Liaison.Mediator.IMediator? _liaisonMediator;
    private MediatR.IMediator? _mediatRMediator;
    private ServiceProvider? _liaisonProvider;
    private ServiceProvider? _mediatRProvider;
    private IServiceScope? _liaisonScope;
    private IServiceScope? _mediatRScope;

    private readonly PingNotification _notification = new(7);

    [Params(2, 5, 10)]
    public int HandlerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var liaisonServices = new ServiceCollection();
        for (var i = 0; i < HandlerCount; i++)
        {
            liaisonServices.AddScoped<Liaison.Mediator.INotificationHandler<PingNotification>, PingNotificationHandler>();
        }

        liaisonServices.AddMediator();

        _liaisonProvider = liaisonServices.BuildServiceProvider();
        _liaisonScope = _liaisonProvider.CreateScope();
        _liaisonMediator = _liaisonScope.ServiceProvider.GetRequiredService<Liaison.Mediator.IMediator>();

        _ = _liaisonScope.ServiceProvider.GetServices<Liaison.Mediator.INotificationHandler<PingNotification>>();

        var mediatRServices = new ServiceCollection();
        for (var i = 0; i < HandlerCount; i++)
        {
            mediatRServices.AddScoped<MediatR.INotificationHandler<PingNotification>, PingNotificationHandler>();
        }

        mediatRServices.AddScoped<MediatR.INotificationPublisher, MediatR.NotificationPublishers.ForeachAwaitPublisher>();
        mediatRServices.AddScoped<MediatR.IMediator>(static provider =>
            new MediatR.Mediator(provider, provider.GetRequiredService<MediatR.INotificationPublisher>()));

        _mediatRProvider = mediatRServices.BuildServiceProvider();
        _mediatRScope = _mediatRProvider.CreateScope();
        _mediatRMediator = _mediatRScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();

        _ = _mediatRScope.ServiceProvider.GetServices<MediatR.INotificationHandler<PingNotification>>();
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
    public Task Liaison_Publish()
    {
        return _liaisonMediator!.Publish(_notification, CancellationToken);
    }

    [Benchmark]
    public Task MediatR_Publish()
    {
        return _mediatRMediator!.Publish(_notification, CancellationToken);
    }
}

