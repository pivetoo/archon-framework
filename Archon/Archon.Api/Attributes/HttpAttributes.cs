using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GetEndpointAttribute : HttpGetAttribute
    {
        public GetEndpointAttribute() : base("[action]")
        {
        }

        public GetEndpointAttribute(string template) : base(EndpointRouteTemplate.Normalize(template))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PostEndpointAttribute : HttpPostAttribute
    {
        public PostEndpointAttribute() : base("[action]")
        {
        }

        public PostEndpointAttribute(string template) : base(EndpointRouteTemplate.Normalize(template))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PutEndpointAttribute : HttpPutAttribute
    {
        public PutEndpointAttribute() : base("[action]")
        {
        }

        public PutEndpointAttribute(string template) : base(EndpointRouteTemplate.Normalize(template))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DeleteEndpointAttribute : HttpDeleteAttribute
    {
        public DeleteEndpointAttribute() : base("[action]")
        {
        }

        public DeleteEndpointAttribute(string template) : base(EndpointRouteTemplate.Normalize(template))
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PatchEndpointAttribute : HttpPatchAttribute
    {
        public PatchEndpointAttribute() : base("[action]")
        {
        }

        public PatchEndpointAttribute(string template) : base(EndpointRouteTemplate.Normalize(template))
        {
        }
    }

}
