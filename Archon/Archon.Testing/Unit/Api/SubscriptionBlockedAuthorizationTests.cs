using System.Reflection;
using System.Security.Claims;
using Archon.Api.Attributes;
using Archon.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Archon.Testing.Unit.Api
{
    /// <summary>
    /// Modo restrito por assinatura: com a claim <c>subscription_blocked</c> o usuario entra, mas
    /// so alcanca o que estiver marcado com <see cref="AllowWhenSubscriptionBlockedAttribute"/>.
    /// O padrao e negar — se um endpoint novo esquecer a marcacao, ele fica fora, nao dentro.
    /// </summary>
    [TestFixture]
    public sealed class SubscriptionBlockedAuthorizationTests
    {
        private sealed class SampleController : ApiControllerBase
        {
            public IActionResult Protected() => Ok();

            [AllowWhenSubscriptionBlocked]
            public IActionResult Billing() => Ok();
        }

        private static AuthorizationFilterContext BuildContext(string actionName, bool blocked, bool root)
        {
            List<Claim> claims = [new Claim("permission", "sample.protected"), new Claim("permission", "sample.billing")];
            if (blocked)
            {
                claims.Add(new Claim("subscription_blocked", "true"));
            }

            if (root)
            {
                claims.Add(new Claim("root", "true"));
            }

            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            MethodInfo method = typeof(SampleController).GetMethod(actionName)!;
            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = "Sample",
                ActionName = actionName,
                MethodInfo = method,
                ControllerTypeInfo = typeof(SampleController).GetTypeInfo()
            };

            ActionContext actionContext = new(httpContext, new RouteData(), descriptor);
            return new AuthorizationFilterContext(actionContext, []);
        }

        [Test]
        public async Task Assinatura_bloqueada_recusa_endpoint_comum_com_402()
        {
            AuthorizationFilterContext context = BuildContext(nameof(SampleController.Protected), blocked: true, root: false);

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(StatusCodes.Status402PaymentRequired));
        }

        [Test]
        public async Task Assinatura_bloqueada_libera_endpoint_marcado()
        {
            AuthorizationFilterContext context = BuildContext(nameof(SampleController.Billing), blocked: true, root: false);

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }

        [Test]
        public async Task Assinatura_bloqueada_recusa_ate_para_root()
        {
            // Papel root e regra de PERMISSAO; inadimplencia nao. Deixar root passar entregaria o
            // produto inteiro ao admin de uma conta bloqueada, que e justamente quem nao pagou.
            AuthorizationFilterContext context = BuildContext(nameof(SampleController.Protected), blocked: true, root: true);

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.TypeOf<StatusCodeResult>());
            Assert.That(((StatusCodeResult)context.Result!).StatusCode, Is.EqualTo(StatusCodes.Status402PaymentRequired));
        }

        [Test]
        public async Task Assinatura_em_dia_nao_muda_nada()
        {
            AuthorizationFilterContext context = BuildContext(nameof(SampleController.Protected), blocked: false, root: false);

            await new RequireAccessAttribute().OnAuthorizationAsync(context);

            Assert.That(context.Result, Is.Null);
        }
    }
}
