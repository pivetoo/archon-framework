namespace Archon.Core.Exceptions
{
    public sealed class IntegrityException : Exception
    {
        public IntegrityException() : base("error.integrity.violation")
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
