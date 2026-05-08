using Archon.Application.Integrations;
using Archon.Application.Services;
using Archon.Core.Responses;
using Archon.Infrastructure.RestApi;
using Rest = Archon.Infrastructure.RestApi.RestApi;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityUsersClient
    {
        private const string IntegrationName = "identity-management";

        private readonly Rest restApi;
        private readonly IIntegrationService integrationService;

        public IdentityUsersClient(Rest restApi, IIntegrationService integrationService)
        {
            this.restApi = restApi;
            this.integrationService = integrationService;
        }

        public async Task<List<IdentityUserDto>> GetActiveUsersAsync(CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<IdentityUserDto>>> response = await restApi.Fetch<ApiResponse<List<IdentityUserDto>>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetActive").WithSecret(secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/GetActive returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        public async Task<IdentityUserDto?> GetUserByIdAsync(long userId, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<IdentityUserDto>> response = await restApi.Fetch<ApiResponse<IdentityUserDto>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetById/{userId}").WithSecret(secret!), ct);

            if (!response.Ok)
            {
                if (response.Status == 404)
                {
                    return null;
                }

                throw new HttpRequestException($"IdentityManagement /api/Users/GetById/{userId} returned {response.Status}");
            }

            return response.Data?.Data;
        }

        public async Task<List<ContractUserDto>> GetUsersByContractAsync(long contractId, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<ContractUserDto>>> response = await restApi.Fetch<ApiResponse<List<ContractUserDto>>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetByContract/{contractId}").WithSecret(secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/GetByContract/{contractId} returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        public async Task<ContractUserDto> CreateUserInContractAsync(CreateUserInContractPayload payload, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<ContractUserDto>> response = await restApi.Fetch<ApiResponse<ContractUserDto>>(
                RestRequest.Post($"{baseUrl}/api/Users/CreateInContract", payload).WithSecret(secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/CreateInContract returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task<ContractUserDto> UpdateUserRoleInContractAsync(long userId, long contractId, long roleId, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            object body = new { ContractId = contractId, RoleId = roleId };
            RestResponse<ApiResponse<ContractUserDto>> response = await restApi.Fetch<ApiResponse<ContractUserDto>>(
                RestRequest.Put($"{baseUrl}/api/Users/UpdateRoleInContract/{userId}", body).WithSecret(secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/UpdateRoleInContract/{userId} returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task SetUserActiveAsync(long userId, bool isActive, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            object body = new { IsActive = isActive };
            RestResponse<ApiResponse<object>> response = await restApi.Fetch<ApiResponse<object>>(
                RestRequest.Put($"{baseUrl}/api/Users/SetActive/{userId}", body).WithSecret(secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/SetActive/{userId} returned {response.Status}");
            }
        }

        public async Task<List<ContractRoleDto>> GetRolesByContractAsync(long contractId, CancellationToken ct = default)
        {
            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<ContractRoleDto>>> response = await restApi.Fetch<ApiResponse<List<ContractRoleDto>>>(
                RestRequest.Get($"{baseUrl}/api/Roles/GetByContract/{contractId}").WithSecret(secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Roles/GetByContract/{contractId} returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        private async Task<(string? baseUrl, string? secret)> ResolveIntegrationAsync(CancellationToken ct)
        {
            Integration? integration = await integrationService.GetByNameAsync(IntegrationName, ct);
            if (integration is null)
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' was not found in table 'integrations'.");
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(integration.BaseUrl))
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' is configured without baseurl.");
                return (null, null);
            }

            string? secret = integration.GetParameter("IntegrationSecret");
            if (string.IsNullOrWhiteSpace(secret))
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' is configured without IntegrationSecret.");
            }

            return (integration.BaseUrl, secret);
        }
    }

    public sealed class IdentityUserDto
    {
        public long Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; }
    }

    public sealed class ContractUserDto
    {
        public long UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset? LastLoginAt { get; set; }

        public long RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        public bool IsRoot { get; set; }

        public DateTimeOffset AssignedAt { get; set; }
    }

    public sealed class ContractRoleDto
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public long ContractId { get; set; }

        public bool IsRoot { get; set; }

        public bool IsDefault { get; set; }
    }

    public sealed class CreateUserInContractPayload
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public long RoleId { get; set; }

        public long ContractId { get; set; }
    }
}
