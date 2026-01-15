using Liaison.Mediator;

var builder = new MediatorBuilder();

builder.RegisterRequestHandler<AddTodo, Todo>(new AddTodoHandler())
       .RegisterNotificationHandler<TodoAdded>(new TodoAddedHandler());

IMediator mediator = builder.Build();

Todo todo = await mediator.Send(new AddTodo("Write more docs"));
await mediator.Publish(new TodoAdded(todo.Id, todo.Title));

Console.WriteLine($"Created todo #{todo.Id}: {todo.Title}");

public sealed record AddTodo(string Title) : IRequest<Todo>;

public sealed record Todo(int Id, string Title);

public sealed class AddTodoHandler : IRequestHandler<AddTodo, Todo>
{
    private int _nextId = 1;

    public Task<Todo> Handle(AddTodo request, CancellationToken cancellationToken)
    {
        var todo = new Todo(_nextId++, request.Title);

        return Task.FromResult(todo);
    }
}

public sealed record TodoAdded(int Id, string Title) : INotification;

public sealed class TodoAddedHandler : INotificationHandler<TodoAdded>
{
    public Task Handle(TodoAdded notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Observed todo #{notification.Id}: {notification.Title}");

        return Task.CompletedTask;
    }
}
