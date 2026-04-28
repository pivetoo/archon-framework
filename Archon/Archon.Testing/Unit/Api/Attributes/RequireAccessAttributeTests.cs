using Archon.Api.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;

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
        public void OnAuthorization_ShouldAllow_UnauthenticatedWithValidIntegrationSecretAndResolveTenant()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Integration-Secret"] = "tenant1-secret";
            httpContext.RequestServices = CreateServiceProviderWithTenantResolver();

            ControllerActionDescriptor actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "Test",
                ActionName = "Action",
                MethodInfo = typeof(TestController).GetMethod("Action")!
            };

            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            AuthorizationFilterContext context = new AuthorizationFilterContext(actionContext, []);

            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.Null);
            Assert.That(httpContext.Items["TenantId"], Is.EqualTo("tenant1"));
        }

        [Test]
        public void OnAuthorization_ShouldReturnUnauthorized_UnauthenticatedWithInvalidIntegrationSecret()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Integration-Secret"] = "invalid-secret";
            httpContext.RequestServices = CreateServiceProviderWithTenantResolver();

            ControllerActionDescriptor actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "Test",
                ActionName = "Action",
                MethodInfo = typeof(TestController).GetMethod("Action")!
            };

            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            AuthorizationFilterContext context = new AuthorizationFilterContext(actionContext, []);

            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
        }

        [Test]
        public void OnAuthorization_ShouldReturnUnauthorized_UnauthenticatedWithMissingIntegrationSecret()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.RequestServices = CreateServiceProviderWithTenantResolver();

            ControllerActionDescriptor actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "Test",
                ActionName = "Action",
                MethodInfo = typeof(TestController).GetMethod("Action")!
            };

            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            AuthorizationFilterContext context = new AuthorizationFilterContext(actionContext, []);

            RequireAccessAttribute attribute = new RequireAccessAttribute();

            attribute.OnAuthorization(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
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

        private static IServiceProvider CreateServiceProvider(IConfiguration configuration)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton(configuration);
            return services.BuildServiceProvider();
        }

        private static IServiceProvider CreateServiceProviderWithTenantResolver()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "TenantDatabases:tenant1:ConnectionString", "Host=localhost;Database=db1;" },
                    { "TenantDatabases:tenant1:IntegrationSecret", "tenant1-secret" },
                    { "TenantDatabases:tenant2:ConnectionString", "Host=localhost;Database=db2;" },
                    { "TenantDatabases:tenant2:IntegrationSecret", "tenant2-secret" }
                })
                .Build();

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddSingleton<ITenantResolver, ConfigurationTenantResolver>();
            services.AddSingleton<ITenantContext, MultiTenantContext>();
            return services.BuildServiceProvider();
        }

        private class TestController
        {
            public void Action() { }
            public void CreateUser() { }
        }
    }
}
