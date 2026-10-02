namespace Company.Product.Worker.Managers.Exceptions;

/// <summary>Something the function needs does not exist. Retrying won't help, so the message is dropped (logged, not retried).</summary>
/// <param name="message">Description of what was not found.</param>
public class NotFoundException(string message) : Exception(message);
