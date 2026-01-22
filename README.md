# Liaison.Mediator

Liaison.Mediator is a lightweight mediator library for .NET that keeps the familiar request/response and notification patterns of [MediatR](https://github.com/jbogard/MediatR) while removing assembly scanning. Handlers are registered explicitly via a fluent builder so the runtime footprint stays small and configuration is fully deterministic.

## Features

- **Explicit registration** – Use `MediatorBuilder` to register request and notification handlers without reflection or assembly scanning.
- **Request/response messaging** – Define requests by implementing `IRequest<TResult>` and handle them through `IRequestHandler<TRequest, TResult>` implementations.
- **Notifications** – Broadcast one-way messages by implementing `INotification` and attaching one or more `INotificationHandler<TNotification>` instances.
- **Unit result helper** – Return `Unit.Value` from commands that do not need to return data.
- **Microsoft.Extensions.DependencyInjection integration** – Wire `IMediator` into `IServiceCollection` and let DI construct handlers (optional assembly-scanning overload available).

## Getting started

1. Install the `Liaison.Mediator` package from NuGet.
2. Choose the registration style that fits your app:
   - Build an `IMediator` manually with `MediatorBuilder` for full control over handler wiring.
   - Register handlers with Microsoft.Extensions.DependencyInjection, then call `AddMediator` to let the container construct them.
3. Define your request/notification types and handlers, then start sending messages.

### Manual builder usage

```csharp
var builder = new MediatorBuilder();

builder.RegisterRequestHandler<AddTodo, Todo>(new AddTodoHandler())
       .RegisterNotificationHandler<TodoAdded>(new TodoAddedHandler());

IMediator mediator = builder.Build();

var result = await mediator.Send(new AddTodo("Write docs"));
await mediator.Publish(new TodoAdded(result.Id));
```

### Dependency injection usage

```csharp
var services = new ServiceCollection();

services.AddScoped<IRequestHandler<AddTodo, Todo>, AddTodoHandler>();
services.AddScoped<INotificationHandler<TodoAdded>, TodoAddedHandler>();

services.AddMediator();

using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();

var result = await mediator.Send(new AddTodo("Write docs"));
await mediator.Publish(new TodoAdded(result.Id));
```

## Samples

Clone the repository and run the sample projects to see the mediator in action with real request and notification types.

| Sample | Demonstrates | Run it |
| --- | --- | --- |
| **ManualBuilderSample** | Building an `IMediator` instance manually and registering handlers without DI. | `dotnet run --project samples/ManualBuilderSample` |
| **DependencyInjectionSample** | Wiring handlers with `IServiceCollection` and requesting `IMediator` from the container. | `dotnet run --project samples/DependencyInjectionSample` |

Each sample writes its output to the console so you can verify handler execution.

## Release flow

- **Stable** – Tag the desired commit with the semantic version (for example `1.2.3`) and push the tag to publish the exact build.
- **Release candidates** – Every push to `master` emits `-rc.*` packages. Include `[minor]` or `[major]` in the commit message to bump the respective version component before the prerelease is generated.

## Project layout

- `src/Liaison.Mediator` – Library source, including interfaces like [`IMediator`](src/Liaison.Mediator/IMediator.cs) and the [`MediatorBuilder`](src/Liaison.Mediator/MediatorBuilder.cs).
- `tests/Liaison.Mediator.Tests` – xUnit test project with coverage for core scenarios.

## Benchmarks (Win11 Pro / Ryzen 9 7940HS as the primary baseline)

BenchmarkDotNet runs are summarized into committed files under `benchmarks/results/**` so results are reproducible and easy to review.

Baseline summary files (commit these after running locally):

- `benchmarks/results/ryzen-win11/latest.summary.json`
- `benchmarks/results/ryzen-win11/latest.summary.md`

Definitions: `Speedup = MediatRMean / LiaisonMean`, `Alloc reduction = 100 * (1 - LiaisonAlloc / MediatRAlloc)`.

If the baseline summary is not present yet, run benchmarks locally and commit `benchmarks/results/ryzen-win11/latest.summary.json`.

| Scenario | Liaison (ns) | MediatR (ns) | Speedup | Liaison (B/op) | MediatR (B/op) | Alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Send_SingleHandler/Send | N/A | N/A | N/A | N/A | N/A | N/A |
| Send_DI/Send | N/A | N/A | N/A | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=2) | N/A | N/A | N/A | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=5) | N/A | N/A | N/A | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=10) | N/A | N/A | N/A | N/A | N/A | N/A |

### How to run locally

```bash
dotnet run -c Release --project benchmarks/Liaison.Mediator.Benchmarks --framework net8.0
dotnet run --project benchmarks/tools/Benchmarks.SummaryGen -- \
  --resultsDir "BenchmarkDotNet.Artifacts/results" \
  --outDir "benchmarks/results/ryzen-win11" \
  --machine "Ryzen 9 7940HS" \
  --os "Windows 11 Pro" \
  --arch "x64" \
  --runtime "net8.0"
```

## Cross-platform sanity check (Ryzen 9 7940HS, Apple M3, Raspberry Pi 5)

Relative metrics are taken from the committed summaries:

- `benchmarks/results/ryzen-win11/latest.summary.json`
- `benchmarks/results/apple-m3-macos/latest.summary.json`
- `benchmarks/results/rpi5-linux/latest.summary.json`

If a machine summary is missing, the values are reported as `N/A`.

| Scenario | Ryzen (speedup / alloc) | Apple M3 (speedup / alloc) | RPi 5 (speedup / alloc) |
| --- | --- | --- | --- |
| Send_SingleHandler/Send | N/A | N/A | N/A |
| Send_DI/Send | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=2) | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=5) | N/A | N/A | N/A |
| Publish_MultiHandler/Publish (HandlerCount=10) | N/A | N/A | N/A |

## License and ownership

This project is released under the MIT License and is developed and maintained by **INNOVATIVE INFLUENCE TECHNOLOGY, LLC**. See [LICENSE](LICENSE) for full terms.
