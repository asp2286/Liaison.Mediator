namespace Liaison.Mediator;

/// <summary>
/// Represents a request that yields a response from a handler.
/// </summary>
/// <typeparam name="TResponse">Type of response expected from the handler.</typeparam>
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker interface for requests that return <see cref="Unit"/>.
/// </summary>
public interface IRequest : IRequest<Unit>
{
}
