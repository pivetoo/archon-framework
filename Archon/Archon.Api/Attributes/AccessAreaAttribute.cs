namespace Archon.Api.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AccessAreaAttribute : Attribute
    {
        public string Description { get; }

        public AccessAreaAttribute(string description)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            Description = description;
        }
    }
}
