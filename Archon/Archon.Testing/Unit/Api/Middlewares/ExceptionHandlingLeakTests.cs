using Archon.Api.ExceptionHandling;
using Archon.Api.Localization;
using Archon.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Text.Json;

namespace Archon.Testing.Unit.Api.Middlewares
{
    /// <summary>
    /// Excecao de dominio carrega uma CHAVE de localizacao, nao um texto. Quando a chave nao existia no
    /// catalogo, o middleware devolvia a chave crua como mensagem — o cliente via
    /// `proposal.send.approvalRequired` na tela, e a lacuna de traducao nao aparecia em lugar nenhum.
    /// </summary>
    public sealed class ExceptionHandlingLeakTests
    {
        private const string ChaveInexistente = "proposal.send.approvalRequired";
        private const string MensagemGenerica = "error.unexpected.short";

        // Catalogo que nao resolve NADA, simulando chave ausente.
        private static IServiceProvider BuildServiceProvider()
        {
            Mock<IStringLocalizer<ArchonApiResource>> archon = new();
            archon.Setup(item => item[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key, resourceNotFound: key != MensagemGenerica));
            archon.Setup(item => item[It.IsAny<string>(), It.IsAny<object[]>()])
                .Returns((string key, object[] args) => new LocalizedString(key, key, resourceNotFound: true));

            Mock<IStringLocalizer> vazio = new();
            vazio.Setup(item => item[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
            vazio.Setup(item => item[It.IsAny<string>(), It.IsAny<object[]>()])
                .Returns((string key, object[] args) => new LocalizedString(key, key, resourceNotFound: true));

            Mock<IStringLocalizerFactory> factory = new();
            factory.Setup(item => item.Create(It.IsAny<Type>())).Returns(vazio.Object);

            return new ServiceCollection()
                .AddSingleton(archon.Object)
                .AddSingleton(factory.Object)
                .AddSingleton(new LocalizationCatalogOptions { ResourceTypes = Array.Empty<Type>() })
                .AddLogging()
                .BuildServiceProvider();
        }

        private static DefaultHttpContext CreateContext()
        {
            DefaultHttpContext context = new();
            context.Response.Body = new MemoryStream();
            context.RequestServices = BuildServiceProvider();
            return context;
        }

        private static async Task<string> ReadMessageAsync(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new(response.Body);
            Dictionary<string, JsonElement> payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await reader.ReadToEndAsync())!;
            return payload["message"].GetString() ?? string.Empty;
        }

        [Test]
        public async Task InvokeAsync_ShouldNotLeakLocalizationKey_WhenDomainKeyIsMissing()
        {
            DefaultHttpContext context = CreateContext();
            ExceptionHandlingMiddleware middleware = new(_ => throw new BusinessRuleException(ChaveInexistente));

            await middleware.InvokeAsync(context);

            string message = await ReadMessageAsync(context.Response);

            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(message, Does.Not.Contain(ChaveInexistente), "a chave crua nao pode chegar ao cliente");
            Assert.That(message, Is.EqualTo(MensagemGenerica));
        }

        [Test]
        public async Task InvokeAsync_ShouldNotThrow_WhenResponseAlreadyStarted()
        {
            DefaultHttpContext context = CreateContext();

            // Simula excecao depois que a resposta ja comecou (streaming, download de arquivo). Sem a
            // guarda de HasStarted, o proprio middleware de erro estourava uma segunda excecao ao tentar
            // trocar status e corpo.
            context.Features.Set<IHttpResponseFeature>(new RespostaJaIniciada());

            ExceptionHandlingMiddleware middleware = new(_ => throw new BusinessRuleException(ChaveInexistente));

            Assert.DoesNotThrowAsync(async () => await middleware.InvokeAsync(context));
        }

        private sealed class RespostaJaIniciada : IHttpResponseFeature
        {
            public Stream Body { get; set; } = new MemoryStream();

            public bool HasStarted => true;

            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

            public string? ReasonPhrase { get; set; }

            public int StatusCode { get; set; } = StatusCodes.Status200OK;

            public void OnCompleted(Func<object, Task> callback, object state)
            {
            }

            public void OnStarting(Func<object, Task> callback, object state)
            {
            }
        }
    }
}
