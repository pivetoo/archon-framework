using Archon.Api.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Archon.Testing.Unit.Api.Attributes
{
    public sealed class RequireAccessAttributeTests
    {
        private static AuthorizationFilterContext CreateContext(string? claimType = null, string? claimValue = null, string controllerName = "Test", string actionName = "Action", bool isAuthenticated = true)
        {
            List<Claim> claims = [];
            if (claimType is not null && claimValue is not null)
            {
                claims.Add(new Claim(claimType, claimValue));
            }

            string? authType = isAuthenticated ? "TestAuth" : null;
            ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(claims, authType));
            DefaultHttpContext httpContext = new DefaultHttpContext { User = user };

            ControllerActionDescriptor actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerName,
                ActionName = actionName,
                MethodInfo = typeof(TestController).GetMethod(actionName)!
            };

            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            return new AuthorizationFilterContext(actionContext, []);
        }

        [Test]
        public void OnAuthorization_ShouldAllow_AuthenticatedWithPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "test.action");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public void OnAuthorization_ShouldAllow_RootUser()
        {
            AuthorizationFilterContext context = CreateContext("root", "true");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public void OnAuthorization_ShouldDeny_UnauthenticatedUser()
        {
            AuthorizationFilterContext context = CreateContext(isAuthenticated: false);
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
        }

        [Test]
        public void OnAuthorization_ShouldDeny_MissingPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "other.action");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<ForbidResult>());
        }

        [Test]
        public void OnAuthorization_ShouldDeny_WrongPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "test.delete");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<ForbidResult>());
        }

        [Test]
        public void OnAuthorization_ShouldUseCamelCase_ControllerAndAction()
        {
            AuthorizationFilterContext context = CreateContext("permission", "testController.createUser", controllerName: "TestController", actionName: "CreateUser");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public void OnAuthorization_ShouldDeny_NonControllerActionDescriptor()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "test.action")], "TestAuth")) };
            ActionDescriptor actionDescriptor = new ActionDescriptor();
            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            AuthorizationFilterContext context = new AuthorizationFilterContext(actionContext, []);
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<ForbidResult>());
        }

        private class TestController
        {
            public void Action() { }
            public void CreateUser() { }
        }
    }
}
