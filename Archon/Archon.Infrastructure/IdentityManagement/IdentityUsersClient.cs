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
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<IdentityUserDto>>> response = await restApi.Fetch<ApiResponse<List<IdentityUserDto>>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetActive").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/GetActive returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        public async Task<IdentityUserDto?> GetUserByIdAsync(long userId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<IdentityUserDto>> response = await restApi.Fetch<ApiResponse<IdentityUserDto>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetById/{userId}").WithTenantApiKey(tenantId, secret!), ct);

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
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<ContractUserDto>>> response = await restApi.Fetch<ApiResponse<List<ContractUserDto>>>(
                RestRequest.Get($"{baseUrl}/api/Users/GetByContract/{contractId}").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/GetByContract/{contractId} returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        public async Task<ContractUserDto> CreateUserInContractAsync(CreateUserInContractPayload payload, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<ContractUserDto>> response = await restApi.Fetch<ApiResponse<ContractUserDto>>(
                RestRequest.Post($"{baseUrl}/api/Users/CreateInContract", payload).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/CreateInContract returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task UpdateUserAsync(long userId, string name, string? password, bool isActive, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            object body = new
            {
                Id = userId,
                Name = name,
                Password = password,
                IsActive = isActive
            };

            RestResponse<ApiResponse<object>> response = await restApi.Fetch<ApiResponse<object>>(
                RestRequest.Put($"{baseUrl}/api/Users/Update/{userId}", body).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/Update/{userId} returned {response.Status}");
            }
        }

        public async Task<ContractUserDto> UpdateUserRoleInContractAsync(long userId, long contractId, long roleId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            object body = new { ContractId = contractId, RoleId = roleId };
            RestResponse<ApiResponse<ContractUserDto>> response = await restApi.Fetch<ApiResponse<ContractUserDto>>(
                RestRequest.Put($"{baseUrl}/api/Users/UpdateRoleInContract/{userId}", body).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/UpdateRoleInContract/{userId} returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task SetUserActiveAsync(long userId, bool isActive, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            object body = new { IsActive = isActive };
            RestResponse<ApiResponse<object>> response = await restApi.Fetch<ApiResponse<object>>(
                RestRequest.Put($"{baseUrl}/api/Users/SetActive/{userId}", body).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Users/SetActive/{userId} returned {response.Status}");
            }
        }

        public async Task<ContractRoleDto?> GetRoleByIdAsync(long roleId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<ContractRoleDto>> response = await restApi.Fetch<ApiResponse<ContractRoleDto>>(
                RestRequest.Get($"{baseUrl}/api/Roles/GetById/{roleId}").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                if (response.Status == 404)
                {
                    return null;
                }

                throw new HttpRequestException($"IdentityManagement /api/Roles/GetById/{roleId} returned {response.Status}");
            }

            return response.Data?.Data;
        }

        public async Task<ContractRoleDto> CreateRoleAsync(CreateRolePayload payload, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<ContractRoleDto>> response = await restApi.Fetch<ApiResponse<ContractRoleDto>>(
                RestRequest.Post($"{baseUrl}/api/Roles/Create", payload).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Roles/Create returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task<ContractRoleDto> UpdateRoleAsync(long roleId, UpdateRolePayload payload, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<ContractRoleDto>> response = await restApi.Fetch<ApiResponse<ContractRoleDto>>(
                RestRequest.Put($"{baseUrl}/api/Roles/Update/{roleId}", payload).WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok || response.Data?.Data is null)
            {
                throw new HttpRequestException($"IdentityManagement /api/Roles/Update/{roleId} returned {response.Status}");
            }

            return response.Data.Data;
        }

        public async Task DeleteRoleAsync(long roleId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<object>> response = await restApi.Fetch<ApiResponse<object>>(
                RestRequest.Delete($"{baseUrl}/api/Roles/Delete/{roleId}").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                string message = response.Data?.Message ?? $"IdentityManagement /api/Roles/Delete/{roleId} returned {response.Status}";
                throw new HttpRequestException(message);
            }
        }

        public async Task<List<AccessResourceDto>> GetAccessResourcesByContractAsync(long contractId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<AccessResourceDto>>> response = await restApi.Fetch<ApiResponse<List<AccessResourceDto>>>(
                RestRequest.Get($"{baseUrl}/api/AccessResources/GetByContract/{contractId}").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/AccessResources/GetByContract/{contractId} returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        public async Task<List<ContractRoleDto>> GetRolesByContractAsync(long contractId, CancellationToken ct = default)
        {
            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<ApiResponse<List<ContractRoleDto>>> response = await restApi.Fetch<ApiResponse<List<ContractRoleDto>>>(
                RestRequest.Get($"{baseUrl}/api/Roles/GetByContract/{contractId}").WithTenantApiKey(tenantId, secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement /api/Roles/GetByContract/{contractId} returned {response.Status}");
            }

            return response.Data?.Data ?? [];
        }

        private async Task<(string? baseUrl, string? tenantId, string? apiKey)> ResolveIntegrationAsync(CancellationToken ct)
        {
            Integration? integration = await integrationService.GetByNameAsync(IntegrationName, ct);
            if (integration is null)
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' was not found in table 'integrations'.");
                return (null, null, null);
            }

            if (string.IsNullOrWhiteSpace(integration.BaseUrl))
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' is configured without baseurl.");
                return (null, null, null);
            }

            string? tenantId = integration.GetParameter("TenantId");
            string? apiKey = integration.GetParameter("ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("IdentityUsersClient: integration 'identity-management' is configured without ApiKey.");
            }

            return (integration.BaseUrl, tenantId, apiKey);
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

        public List<long> AccessResourceIds { get; set; } = [];
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

    public sealed class CreateRolePayload
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public long ContractId { get; set; }

        public bool IsRoot { get; set; }

        public bool IsDefault { get; set; }

        public List<long> AccessResourceIds { get; set; } = [];
    }

    public sealed class UpdateRolePayload
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsRoot { get; set; }

        public bool IsDefault { get; set; }

        public List<long> AccessResourceIds { get; set; } = [];
    }

    public sealed class AccessResourceDto
    {
        public long Id { get; set; }

        public long SystemApplicationId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public string Controller { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;
    }
}
