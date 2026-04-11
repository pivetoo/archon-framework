using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GetEndpointAttribute : HttpGetAttribute
    {
        public GetEndpointAttribute() : base()
        {
        }

        public GetEndpointAttribute(string template) : base(template)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PostEndpointAttribute : HttpPostAttribute
    {
        public PostEndpointAttribute() : base()
        {
        }

        public PostEndpointAttribute(string template) : base(template)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PutEndpointAttribute : HttpPutAttribute
    {
        public PutEndpointAttribute() : base()
        {
        }

        public PutEndpointAttribute(string template) : base(template)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DeleteEndpointAttribute : HttpDeleteAttribute
    {
        public DeleteEndpointAttribute() : base()
        {
        }

        public DeleteEndpointAttribute(string template) : base(template)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PatchEndpointAttribute : HttpPatchAttribute
    {
        public PatchEndpointAttribute() : base()
        {
        }

        public PatchEndpointAttribute(string template) : base(template)
        {
        }
    }
}
