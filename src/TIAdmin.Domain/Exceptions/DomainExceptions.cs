namespace TIAdmin.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entity, int id)
        : base($"{entity.ToUpperInvariant()}_NOT_FOUND", $"No fue posible encontrar {entity} con ID {id}.")
    {
    }

    public EntityNotFoundException(string entity)
        : base($"{entity.ToUpperInvariant()}_NOT_FOUND", $"No fue posible encontrar {entity}.")
    {
    }
}

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string code, string message) : base(code, message)
    {
    }
}

public sealed class InvalidOperationException : DomainException
{
    public InvalidOperationException(string code, string message) : base(code, message)
    {
    }
}
