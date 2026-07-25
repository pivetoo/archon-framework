using Archon.Api.MultiTenancy;
using Archon.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Archon.Testing.Unit.Api.Middlewares
{
    /// <summary>
    /// Sem implementacao de <see cref="ISessionValidator"/> o middleware rodava e nao validava NADA.
    /// Como `UseSessionValidation()` no Program.cs passa a impressao de que sessao revogada e barrada,
    /// a lacuna ficava invisivel — no ecossistema inteiro nao existia nenhuma implementacao registrada.
    /// </summary>
    public sealed class SessionValidationTests
    {
        private static IApplicationBuilder BuildApp(Action<IServiceCollection>? configure = null)
        {
            ServiceCollection services = new();
            configure?.Invoke(services);

            return new ApplicationBuilder(services.BuildServiceProvider());
        }

        [Test]
        public void UseSessionValidation_ShouldThrow_WhenNoValidatorIsRegistered()
        {
            IApplicationBuilder app = BuildApp();

            InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => app.UseSessionValidation());

            Assert.That(exception!.Message, Does.Contain("ISessionValidator"));
        }

        [Test]
        public void UseSessionValidation_ShouldRegisterMiddleware_WhenValidatorIsRegistered()
        {
            IApplicationBuilder app = BuildApp(services => services.AddSingleton<ISessionValidator, AlwaysValidSessionValidator>());

            Assert.DoesNotThrow(() => app.UseSessionValidation());
        }

        private sealed class AlwaysValidSessionValidator : ISessionValidator
        {
            public Task<bool> IsSessionValidAsync(string sessionId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }
        }
    }
}
