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
}
