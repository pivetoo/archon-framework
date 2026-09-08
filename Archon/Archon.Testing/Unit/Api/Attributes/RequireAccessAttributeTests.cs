using Archon.Api.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
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
        public async Task OnAuthorization_ShouldAllow_AuthenticatedWithPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "test.action");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public async Task OnAuthorization_ShouldAllow_UnauthenticatedWithValidApiKeyAndResolveTenant()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Api-Key"] = "tenant1-secret";
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

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
            Assert.That(httpContext.Items["TenantId"], Is.EqualTo("tenant1"));
        }

        [Test]
        public async Task OnAuthorization_ShouldReturnUnauthorized_UnauthenticatedWithInvalidApiKey()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Api-Key"] = "invalid-secret";
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

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldReturnUnauthorized_UnauthenticatedWithMissingApiKey()
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

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldAllow_RootUser()
        {
            AuthorizationFilterContext context = CreateContext("root", "true");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public async Task OnAuthorization_ShouldDeny_UnauthenticatedUser()
        {
            AuthorizationFilterContext context = CreateContext(isAuthenticated: false);
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldDeny_MissingPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "other.action");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.InstanceOf<ForbidResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldDeny_WrongPermission()
        {
            AuthorizationFilterContext context = CreateContext("permission", "test.delete");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.InstanceOf<ForbidResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldUseCamelCase_ControllerAndAction()
        {
            AuthorizationFilterContext context = CreateContext("permission", "testController.createUser", controllerName: "TestController", actionName: "CreateUser");
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public async Task OnAuthorization_ShouldDeny_NonControllerActionDescriptor()
        {
            DefaultHttpContext httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "test.action")], "TestAuth")) };
            ActionDescriptor actionDescriptor = new ActionDescriptor();
            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            AuthorizationFilterContext context = new AuthorizationFilterContext(actionContext, []);
            RequireAccessAttribute attribute = new RequireAccessAttribute();

            await attribute.OnAuthorizationAsync(context);

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
                    { "TenantDatabases:tenant1:ApiKey", "tenant1-secret" },
                    { "TenantDatabases:tenant2:ConnectionString", "Host=localhost;Database=db2;" },
                    { "TenantDatabases:tenant2:ApiKey", "tenant2-secret" }
                })
                .Build();

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddMemoryCache();
            services.AddSingleton<ITenantResolver, ConfigurationTenantResolver>();
            services.AddSingleton<ITenantContext, MultiTenantContext>();
            return services.BuildServiceProvider();
        }

        private class TestController
        {
            public void Action() { }
            public void CreateUser() { }
        }

        [AccessModule("financeiro")]
        private class FinanceiroController
        {
            public void Get() { }

            [AccessCapability("financeiro.aprovar")]
            public void Approve() { }
        }

        private static AuthorizationFilterContext CreateCapabilityContext(Type controllerType, string actionName, string httpMethod, params string[] capabilityClaims)
        {
            List<Claim> claims = capabilityClaims.Select(capability => new Claim("capability", capability)).ToList();
            ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            DefaultHttpContext httpContext = new DefaultHttpContext { User = user };
            httpContext.Request.Method = httpMethod;

            ControllerActionDescriptor actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerType.Name.Replace("Controller", string.Empty),
                ActionName = actionName,
                MethodInfo = controllerType.GetMethod(actionName)!,
                ControllerTypeInfo = controllerType.GetTypeInfo()
            };

            ActionContext actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            return new AuthorizationFilterContext(actionContext, []);
        }

        [Test]
        public async Task OnAuthorization_ShouldAllow_WhenCapabilityOfTheEndpointIsGranted()
        {
            AuthorizationFilterContext context = CreateCapabilityContext(typeof(FinanceiroController), "Get", "GET", "financeiro.ver");

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public async Task OnAuthorization_ShouldForbid_WhenCapabilityDoesNotCoverTheVerb()
        {
            // financeiro.ver nao libera POST: escrever exige financeiro.editar.
            AuthorizationFilterContext context = CreateCapabilityContext(typeof(FinanceiroController), "Get", "POST", "financeiro.ver");

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.TypeOf<ForbidResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldUseTheExplicitCapabilityOfTheAction()
        {
            AuthorizationFilterContext granted = CreateCapabilityContext(typeof(FinanceiroController), "Approve", "POST", "financeiro.aprovar");
            AuthorizationFilterContext denied = CreateCapabilityContext(typeof(FinanceiroController), "Approve", "POST", "financeiro.editar");

            await new RequireAccessAttribute().OnAuthorizationAsync(granted);
            await new RequireAccessAttribute().OnAuthorizationAsync(denied);

            Assert.That(granted.Result, Is.Null);
            Assert.That(denied.Result, Is.TypeOf<ForbidResult>());
        }

        [Test]
        public async Task OnAuthorization_ShouldForbid_WhenUserHasNeitherPermissionNorCapability()
        {
            AuthorizationFilterContext context = CreateCapabilityContext(typeof(FinanceiroController), "Get", "GET", "comercial.ver");

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.TypeOf<ForbidResult>());
        }
    }
}
