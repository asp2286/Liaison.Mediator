# Liaison.Mediator

[![NuGet](https://img.shields.io/nuget/v/Liaison.Mediator.svg)](https://www.nuget.org/packages/Liaison.Mediator/)
[![NuGet (prerelease)](https://img.shields.io/nuget/vpre/Liaison.Mediator.svg)](https://www.nuget.org/packages/Liaison.Mediator/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Liaison.Mediator.svg)](https://www.nuget.org/packages/Liaison.Mediator/)

**NuGet package:** https://www.nuget.org/packages/Liaison.Mediator

Liaison.Mediator is a lightweight mediator library for .NET that keeps the familiar request/response and notification patterns of [MediatR](https://github.com/jbogard/MediatR) without requiring assembly scanning. Handlers are registered explicitly via a fluent builder so the runtime footprint stays small and configuration is fully deterministic.

## Features

- **Explicit registration (default)** – Use `MediatorBuilder` to register request and notification handlers without reflection or assembly scanning.
- **Request/response messaging** – Define requests by implementing `IRequest<TResult>` and handle them through `IRequestHandler<TRequest, TResult>` implementations.
- **Notifications** – Broadcast one-way messages by implementing `INotification` and attaching one or more `INotificationHandler<TNotification>` instances.
- **Unit result helper** – Return `Unit.Value` from commands that do not need to return data.
- **Microsoft.Extensions.DependencyInjection integration** – Wire `IMediator` into `IServiceCollection` and let DI construct handlers. An optional convenience overload can scan assemblies, but only as an opt-in helper for rapid prototyping or migration.

## Why Liaison.Mediator

- Explicit registration remains the default and recommended approach (`MediatorBuilder` or registering handlers in DI).
- No required assembly scanning; scanning is provided only as an opt-in convenience overload.
- Deterministic configuration (no hidden handler discovery).
- Design intent: keep startup predictable and be more trimming/AOT-friendly by avoiding reflection-heavy discovery in the default path.

## When to prefer MediatR

- You want the larger ecosystem (integrations, extensions, examples, community support).
- You rely on MediatR-specific conventions or extension patterns beyond a minimal core API.
- You prefer feature completeness and established defaults over explicit wiring.

## Trade-offs / Non-goals

- `Send` requires exactly one handler per concrete request type (multiple handlers are not supported and result in an error).
- Dispatch is by exact runtime type (no polymorphic/base-type fallback for requests or notifications).
- No built-in complex ordering/priority policies for notification handlers (order is registration/container order).

## Publish semantics

- Current behavior (verified): `MediatorBuilder` publishes handlers sequentially, in the order they were registered; exceptions are fail-fast.
- Current behavior (verified): DI publish enumerates handlers from the container; `INotificationPublisher` controls sequencing (default `ForeachAwaitNotificationPublisher` is sequential/fail-fast; `TaskWhenAllNotificationPublisher` is concurrent and follows `Task.WhenAll` exception aggregation).
- Current behavior (verified): the `CancellationToken` is passed to each handler and publisher; cancellation behavior depends on the handlers/publisher.
- Current behavior (verified): 0 B/op allocations are possible for Publish when handlers are allocation-free and Liaison’s DI publish path can invoke them directly without building per-call execution wrappers.

## Compatibility

- Target frameworks: `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`.
- Nullable reference types: enabled.
- Trimming/AOT: explicit registration avoids required scanning; the scanning overload uses reflection over assembly types (design intent, not a hard guarantee).
- Versioning: stable releases use semantic versions; breaking changes require a major version bump (see Release flow).

### Future: Abstractions-only package (idea)

Optional idea: an `Abstractions` package containing the core interfaces (`IMediator`, `IRequest<>`, `INotification`, etc.) for large solutions that want to share contracts without referencing the full implementation. No commitment or timeline.

## Getting started

1. Install the `Liaison.Mediator` package from NuGet.
   > Note: The GitHub “Packages” section refers to GitHub Packages.
   > Liaison.Mediator is published on NuGet.org (see the link and badges above).

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
- **Release candidates** – Every push to `main` emits `-rc.*` packages. Include `[minor]` or `[major]` in the commit message to bump the respective version component before the prerelease is generated.

## Project layout

- `src/Liaison.Mediator` – Library source, including interfaces like [`IMediator`](src/Liaison.Mediator/IMediator.cs) and the [`MediatorBuilder`](src/Liaison.Mediator/MediatorBuilder.cs).
- `tests/Liaison.Mediator.Tests` – xUnit test project with coverage for core scenarios.

<!-- BENCHMARKS:BEGIN -->
## Benchmarks

Primary baseline: Windows 11 Pro / Ryzen 9 7940HS.

| Scenario | Runtime | MediatR Mean | Liaison Mean | Speedup | MediatR Alloc (B/op) | Liaison Alloc (B/op) | Alloc Reduction |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Send_DI/Send | net8.0 | 97.91 ns | 160.02 ns | x0.61 | 312 | 440 | -41% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | net8.0 | 131.2 ns | 189.7 ns | x0.69 | 448 | 576 | -29% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | net8.0 | 163.7 ns | 195.6 ns | x0.84 | 560 | 688 | -23% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | net8.0 | 200.4 ns | 226.4 ns | x0.89 | 896 | 1024 | -14% |
| Send_SingleHandler/Send | net8.0 | 99.92 ns | 71.71 ns | x1.39 | 312 | 272 | 13% |
| Publish_MultiHandler/Publish (HandlerCount=2) | net8.0 | 92.03 ns | 33.56 ns | x2.74 | 392 | 32 | 92% |
| Publish_MultiHandler/Publish (HandlerCount=5) | net8.0 | 145.5 ns | 44.36 ns | x3.28 | 776 | 32 | 96% |
| Publish_MultiHandler/Publish (HandlerCount=10) | net8.0 | 236.41 ns | 59.87 ns | x3.95 | 1416 | 32 | 98% |

## Cross-platform sanity check

| Scenario | Ryzen speedup | Ryzen alloc reduction | Apple M3 speedup | Apple M3 alloc reduction | RPi5 speedup | RPi5 alloc reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Send_DI/Send | x0.61 | -41% | x0.60 | -41% | x0.67 | -41% |
| Send_DI_Pipeline/Send (BehaviorCount=1) | x0.69 | -29% | x0.74 | -29% | x0.76 | -29% |
| Send_DI_Pipeline/Send (BehaviorCount=2) | x0.84 | -23% | x0.78 | -23% | x0.84 | -23% |
| Send_DI_Pipeline/Send (BehaviorCount=5) | x0.89 | -14% | x0.92 | -14% | x0.86 | -14% |
| Send_SingleHandler/Send | x1.39 | 13% | N/A | N/A | x1.29 | 13% |
| Publish_MultiHandler/Publish (HandlerCount=2) | x2.74 | 92% | N/A | N/A | x3.71 | 92% |
| Publish_MultiHandler/Publish (HandlerCount=5) | x3.28 | 96% | N/A | N/A | x5.22 | 96% |
| Publish_MultiHandler/Publish (HandlerCount=10) | x3.95 | 98% | N/A | N/A | x5.32 | 98% |
<!-- BENCHMARKS:END -->

## License and ownership

This project is released under the MIT License and is developed and maintained by **INNOVATIVE INFLUENCE TECHNOLOGY, LLC**. See [LICENSE](LICENSE) for full terms.
