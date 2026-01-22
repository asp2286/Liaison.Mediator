using BenchmarkDotNet.Attributes;
using Liaison.Mediator.Benchmarks.Infrastructure;
using Liaison.Mediator.Benchmarks.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace Liaison.Mediator.Benchmarks.Benchmarks;

public class Publish_MultiHandler : BenchmarkBase
{
    private Liaison.Mediator.IMediator? _liaisonMediator;
    private MediatR.IMediator? _mediatRMediator;
    private ServiceProvider? _mediatRProvider;
    private readonly PingNotification _notification = new(7);

    [Params(2, 5, 10)]
    public int HandlerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var liaisonBuilder = new Liaison.Mediator.MediatorBuilder();
        for (var i = 0; i < HandlerCount; i++)
        {
            liaisonBuilder.RegisterNotificationHandler<PingNotification>(new PingNotificationHandler());
        }

        _liaisonMediator = liaisonBuilder.Build();

        var services = new ServiceCollection();
        for (var i = 0; i < HandlerCount; i++)
        {
            services.AddSingleton<MediatR.INotificationHandler<PingNotification>>(new PingNotificationHandler());
        }

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
