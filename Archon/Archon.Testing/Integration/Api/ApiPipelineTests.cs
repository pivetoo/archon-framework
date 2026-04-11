using Archon.Testing.Integration.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Archon.Testing.Integration.Api
{
    public sealed class ApiPipelineTests
    {
        [Test]
        public async Task SuccessEndpoint_ShouldReturnStandardEnvelope()
        {
            await using WebApplication app = await TestApiHost.CreateAsync();
            HttpClient client = app.GetTestClient();

            HttpResponseMessage response = await client.GetAsync("/api/testapi/success");
            JsonResultModel? result = await response.Content.ReadFromJsonAsync<JsonResultModel>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Message, Is.EqualTo("Completed."));
            Assert.That(result.Data.HasValue, Is.True);
            Assert.That(result.Data!.Value.GetProperty("value").GetString(), Is.EqualTo("ok"));
        }

        [Test]
        public async Task ExceptionHandlingMiddleware_ShouldReturnStandardErrorEnvelope()
        {
            await using WebApplication app = await TestApiHost.CreateAsync();
            HttpClient client = app.GetTestClient();

            HttpResponseMessage response = await client.GetAsync("/api/testapi/failure");
            JsonResultModel? result = await response.Content.ReadFromJsonAsync<JsonResultModel>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Message, Is.EqualTo("Invalid request."));
        }

        [Test]
        public async Task TenantResolutionMiddleware_ShouldPopulateTenantContext()
        {
            await using WebApplication app = await TestApiHost.CreateAsync();
            HttpClient client = app.GetTestClient();

            HttpResponseMessage response = await client.GetAsync("/api/testapi/tenant");
            JsonResultModel? result = await response.Content.ReadFromJsonAsync<JsonResultModel>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Data.HasValue, Is.True);
            Assert.That(result.Data!.Value.GetProperty("tenantId").GetString(), Is.EqualTo("default"));
        }

        [Test]
        public async Task ValidationFilter_ShouldReturnNormalizedValidationErrors()
        {
            await using WebApplication app = await TestApiHost.CreateAsync();
            HttpClient client = app.GetTestClient();

            HttpResponseMessage response = await client.PostAsJsonAsync("/api/testapi/validate", new { Name = "" });
            JsonResultModel? result = await response.Content.ReadFromJsonAsync<JsonResultModel>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Message, Is.Not.Empty);
            Assert.That(result.Errors.HasValue, Is.True);
            bool hasRequestName = result.Errors!.Value.TryGetProperty("request.Name", out _);
            bool hasName = result.Errors.Value.TryGetProperty("Name", out _);
            Assert.That(hasRequestName || hasName, Is.True);
        }

        [Test]
        public async Task ValidationFilter_ShouldRequireRequestBodyForNonNullableBodyParameters()
        {
            await using WebApplication app = await TestApiHost.CreateAsync();
            HttpClient client = app.GetTestClient();

            HttpResponseMessage response = await client.PostAsJsonAsync<object?>("/api/testapi/validate", null);
            JsonResultModel? result = await response.Content.ReadFromJsonAsync<JsonResultModel>();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Message, Is.Not.Empty);
            Assert.That(result.Data.HasValue, Is.False);
            Assert.That(result.Errors.HasValue, Is.False);
        }

        private sealed class JsonResultModel
        {
            public string Message { get; init; } = string.Empty;

            public JsonElement? Data { get; init; }

            public JsonElement? Errors { get; init; }
        }
    }
}
