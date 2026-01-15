# Liaison.Mediator

Liaison.Mediator is a lightweight mediator library that keeps the familiar request/response and notification patterns of MediatR while making handler registration explicit by default. You can also wire it into Microsoft.Extensions.DependencyInjection via `AddMediator`.

## Installation

```powershell
Install-Package Liaison.Mediator
```

## Usage

### Manual builder

```csharp
var builder = new MediatorBuilder();

builder.RegisterRequestHandler<CreateOrder, Order>(new CreateOrderHandler())
       .RegisterNotificationHandler<OrderCreated>(new OrderCreatedHandler());

IMediator mediator = builder.Build();
```

### Microsoft.Extensions.DependencyInjection

```csharp
var services = new ServiceCollection();
services.AddScoped<IRequestHandler<CreateOrder, Order>, CreateOrderHandler>();
services.AddScoped<INotificationHandler<OrderCreated>, OrderCreatedHandler>();
services.AddMediator();

await using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
```

## Documentation

Find samples and additional guidance on GitHub: https://github.com/asp2286/Liaison.Mediator
