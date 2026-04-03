namespace Archon.Core.Exceptions
{
    public sealed class IntegrityException : Exception
    {
        public IntegrityException() : base("An integrity constraint violation occurred.")
        {
        }

        public IntegrityException(string message) : base(message)
        {
        }

        public IntegrityException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
