using Archon.Api.Attributes;
using Archon.Infrastructure.IdentityManagement;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    public sealed class UsersManagementController : ApiControllerBase
    {
        private readonly IdentityUsersClient identityUsersClient;

        public UsersManagementController(IdentityUsersClient identityUsersClient)
        {
            this.identityUsersClient = identityUsersClient;
        }

        [RequireAccess("Permite listar usuarios do contrato ativo.")]
        [GetEndpoint]
        public async Task<IActionResult> GetByCurrentContract(CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            List<ContractUserDto> users = await identityUsersClient.GetUsersByContractAsync(contractId.Value, cancellationToken);
            return Http200(users);
        }

        [RequireAccess("Permite listar perfis do contrato ativo.")]
        [GetEndpoint]
        public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            List<ContractRoleDto> roles = await identityUsersClient.GetRolesByContractAsync(contractId.Value, cancellationToken);
            return Http200(roles);
        }

        [RequireAccess("Permite criar usuario no contrato ativo.")]
        [PostEndpoint]
        public async Task<IActionResult> Create([FromBody] CreateUserBodyRequest request, CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            CreateUserInContractPayload payload = new CreateUserInContractPayload
            {
                Username = request.Username,
                Email = request.Email,
                Password = request.Password,
                Name = request.Name,
                RoleId = request.RoleId,
                ContractId = contractId.Value
            };

            ContractUserDto user = await identityUsersClient.CreateUserInContractAsync(payload, cancellationToken);
            return Http201(user, "Usuário criado e vinculado ao contrato ativo.");
        }

        [RequireAccess("Permite alterar perfil do usuario no contrato ativo.")]
        [PutEndpoint("{userId:long}")]
        public async Task<IActionResult> UpdateRole(long userId, [FromBody] UpdateUserRoleBodyRequest request, CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            ContractUserDto user = await identityUsersClient.UpdateUserRoleInContractAsync(userId, contractId.Value, request.RoleId, cancellationToken);
            return Http200(user, "Perfil do usuário atualizado no contrato ativo.");
        }

        [RequireAccess("Permite ativar/desativar usuario.")]
        [PutEndpoint("{userId:long}")]
        public async Task<IActionResult> SetActive(long userId, [FromBody] SetActiveBodyRequest request, CancellationToken cancellationToken)
        {
            await identityUsersClient.SetUserActiveAsync(userId, request.IsActive, cancellationToken);
            return Http200(new { request.IsActive }, request.IsActive ? "Usuário reativado." : "Usuário desativado.");
        }

        [RequireAccess("Permite atualizar usuario do contrato ativo.")]
        [PutEndpoint("{userId:long}")]
        public async Task<IActionResult> Update(long userId, [FromBody] UpdateUserBodyRequest request, CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            await identityUsersClient.UpdateUserAsync(userId, request.Name, request.Password, request.IsActive, cancellationToken);
            ContractUserDto user = await identityUsersClient.UpdateUserRoleInContractAsync(userId, contractId.Value, request.RoleId, cancellationToken);
            return Http200(user, "Usuário atualizado.");
        }

        [RequireAccess("Permite consultar um perfil do contrato ativo.")]
        [GetEndpoint("{roleId:long}")]
        public async Task<IActionResult> GetRoleById(long roleId, CancellationToken cancellationToken)
        {
            ContractRoleDto? role = await identityUsersClient.GetRoleByIdAsync(roleId, cancellationToken);
            if (role is null)
            {
                return Http404("Perfil não encontrado.");
            }

            return Http200(role);
        }

        [RequireAccess("Permite criar um perfil no contrato ativo.")]
        [PostEndpoint]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleBodyRequest request, CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            CreateRolePayload payload = new()
            {
                Name = request.Name,
                Description = request.Description,
                ContractId = contractId.Value,
                IsRoot = request.IsRoot,
                IsDefault = request.IsDefault,
                AccessResourceIds = request.AccessResourceIds
            };

            ContractRoleDto role = await identityUsersClient.CreateRoleAsync(payload, cancellationToken);
            return Http201(role, "Perfil criado.");
        }

        [RequireAccess("Permite atualizar um perfil do contrato ativo.")]
        [PutEndpoint("{roleId:long}")]
        public async Task<IActionResult> UpdateRole(long roleId, [FromBody] UpdateRoleBodyRequestFull request, CancellationToken cancellationToken)
        {
            UpdateRolePayload payload = new()
            {
                Name = request.Name,
                Description = request.Description,
                IsRoot = request.IsRoot,
                IsDefault = request.IsDefault,
                AccessResourceIds = request.AccessResourceIds
            };

            ContractRoleDto role = await identityUsersClient.UpdateRoleAsync(roleId, payload, cancellationToken);
            return Http200(role, "Perfil atualizado.");
        }

        [RequireAccess("Permite excluir um perfil do contrato ativo.")]
        [DeleteEndpoint("{roleId:long}")]
        public async Task<IActionResult> DeleteRole(long roleId, CancellationToken cancellationToken)
        {
            await identityUsersClient.DeleteRoleAsync(roleId, cancellationToken);
            return Http200("Perfil excluído.");
        }

        [RequireAccess("Permite listar as permissões disponíveis no contrato ativo.")]
        [GetEndpoint]
        public async Task<IActionResult> GetAccessResources(CancellationToken cancellationToken)
        {
            long? contractId = ResolveCurrentContractId();
            if (!contractId.HasValue)
            {
                return Http403("Contrato ativo não identificado na sessão.");
            }

            List<AccessResourceDto> resources = await identityUsersClient.GetAccessResourcesByContractAsync(contractId.Value, cancellationToken);
            return Http200(resources);
        }

        private long? ResolveCurrentContractId()
        {
            string? value = User.FindFirst("contract_id")?.Value;
            return long.TryParse(value, out long parsed) ? parsed : null;
        }
    }

    public sealed class CreateUserBodyRequest
    {
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public long RoleId { get; set; }
    }

    public sealed class UpdateUserRoleBodyRequest
    {
        public long RoleId { get; set; }
    }

    public sealed class SetActiveBodyRequest
    {
        public bool IsActive { get; set; }
    }

    public sealed class UpdateUserBodyRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Password { get; set; }

        public bool IsActive { get; set; }

        public long RoleId { get; set; }
    }

    public sealed class CreateRoleBodyRequest
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsRoot { get; set; }

        public bool IsDefault { get; set; }

        public List<long> AccessResourceIds { get; set; } = [];
    }

    public sealed class UpdateRoleBodyRequestFull
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsRoot { get; set; }

        public bool IsDefault { get; set; }

        public List<long> AccessResourceIds { get; set; } = [];
    }
}
