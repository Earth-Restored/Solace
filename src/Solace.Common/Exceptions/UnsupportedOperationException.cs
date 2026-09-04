namespace Solace.Common.Exceptions;

public sealed class UnsupportedOperationException : Exception
{
    public UnsupportedOperationException()
        : base()
    {
    }

    public UnsupportedOperationException(string? message)
        : base(message)
    {
    }

    public UnsupportedOperationException(string? message, Exception? innerException)
        : base(message, innerException)
    {

    }
}
