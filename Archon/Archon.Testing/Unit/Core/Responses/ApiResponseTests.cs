using System.Text.Json;
using Archon.Core.Responses;

namespace Archon.Testing.Unit.Core.Responses
{
    public sealed class ApiResponseTests
    {
        [Test]
        public void Serialize_ShouldIgnoreNullProperties()
        {
            ApiResponse response = new ApiResponse
            {
                Message = "Completed."
            };

            string json = JsonSerializer.Serialize(response);

            Assert.That(json, Does.Contain("\"Message\"").Or.Contain("\"message\""));
            Assert.That(json, Does.Not.Contain("Data"));
            Assert.That(json, Does.Not.Contain("Errors"));
            Assert.That(json, Does.Not.Contain("Pagination"));
        }

        [Test]
        public void Serialize_ShouldIncludeNonNullProperties()
        {
            ApiResponse response = new ApiResponse
            {
                Message = "Completed.",
                Data = new
                {
                    Id = 10
                },
                Errors = new[] { "error-1" },
                Pagination = new
                {
                    Page = 1
                }
            };

            string json = JsonSerializer.Serialize(response);

            Assert.That(json, Does.Contain("\"Id\":10").Or.Contain("\"id\":10"));
            Assert.That(json, Does.Contain("error-1"));
            Assert.That(json, Does.Contain("\"Page\":1").Or.Contain("\"page\":1"));
        }
    }
}
