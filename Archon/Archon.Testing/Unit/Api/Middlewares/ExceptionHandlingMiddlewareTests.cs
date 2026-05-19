using Archon.Api.ExceptionHandling;
using Archon.Api.Localization;
using Archon.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Text.Json;

namespace Archon.Testing.Unit.Api.Middlewares
{
    public sealed class ExceptionHandlingMiddlewareTests
    {
        private static DefaultHttpContext CreateContext()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.RequestServices = BuildServiceProvider();
            return context;
        }

        private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
        {
            return new ExceptionHandlingMiddleware(next);
        }

        private static async Task<Dictionary<string, JsonElement>> ReadResponseAsync(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using StreamReader reader = new StreamReader(response.Body);
            string json = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        }

        private static IStringLocalizer<ArchonApiResource> CreateLocalizer()
        {
            Mock<IStringLocalizer<ArchonApiResource>> mock = new Mock<IStringLocalizer<ArchonApiResource>>();
            mock.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, resourceNotFound: LooksLikeRawMessage(key)));
            mock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] args) => new LocalizedString(key, string.Format(key, args), resourceNotFound: LooksLikeRawMessage(key)));
            return mock.Object;
        }

        private static bool LooksLikeRawMessage(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Contains(' ');
        }

        private static IStringLocalizerFactory CreateLocalizerFactory()
        {
            Mock<IStringLocalizerFactory> factory = new Mock<IStringLocalizerFactory>();
            Mock<IStringLocalizer> localizer = new Mock<IStringLocalizer>();
            localizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
            localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] args) => new LocalizedString(key, key, resourceNotFound: true));
            factory.Setup(f => f.Create(It.IsAny<Type>())).Returns(localizer.Object);
            return factory.Object;
        }

        private static IServiceProvider BuildServiceProvider()
        {
            return new ServiceCollection()
                .AddSingleton(CreateLocalizer())
                .AddSingleton(CreateLocalizerFactory())
                .AddSingleton(new LocalizationCatalogOptions { ResourceTypes = Array.Empty<Type>() })
                .AddLogging()
                .BuildServiceProvider();
        }

        [Test]
        public async Task InvokeAsync_ShouldPassThrough_WhenNoException()
        {
            DefaultHttpContext context = CreateContext();
            bool nextCalled = false;
            RequestDelegate next = (ctx) => { nextCalled = true; return Task.CompletedTask; };
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn401_ForUnauthorizedAccessException()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new UnauthorizedAccessException("auth.unauthorized");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(401));
            Dictionary<string, JsonElement> response = await ReadResponseAsync(context.Response);
            Assert.That(response["message"].GetString(), Is.EqualTo("auth.unauthorized"));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn404_ForKeyNotFoundException()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new KeyNotFoundException("record.notFound");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Dictionary<string, JsonElement> response = await ReadResponseAsync(context.Response);
            Assert.That(response["message"].GetString(), Is.EqualTo("record.notFound"));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn400_ForArgumentException()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new ArgumentException("request.invalid");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn409_ForIntegrityException()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new IntegrityException("error.integrity");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn500_ForGenericException()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new Exception("unexpected");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Dictionary<string, JsonElement> response = await ReadResponseAsync(context.Response);
            Assert.That(response.ContainsKey("errors"), Is.True);
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn400_ForClientErrorInvalidOperation()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new InvalidOperationException("request.body.required");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task InvokeAsync_ShouldReturn500_ForInternalInvalidOperation()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new InvalidOperationException("NullReference inside service");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task InvokeAsync_ShouldIncludeProblemDetails_WhenErrorOccurs()
        {
            DefaultHttpContext context = CreateContext();
            RequestDelegate next = (ctx) => throw new ArgumentException("validation.failed");
            ExceptionHandlingMiddleware middleware = CreateMiddleware(next);

            await middleware.InvokeAsync(context);

            Dictionary<string, JsonElement> response = await ReadResponseAsync(context.Response);
            Assert.That(response.ContainsKey("errors"), Is.True);
            Assert.That(response["errors"].ValueKind, Is.EqualTo(JsonValueKind.Object));
        }
    }
}
