using Archon.Api.Attributes;
using Archon.Application.Services;
using Archon.Core.Notifications;
using Archon.Core.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace Archon.Api.Controllers
{
    public sealed class NotificationsController : ApiControllerBase
    {
        private readonly INotificationService notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            this.notificationService = notificationService;
        }

        [RequireAccess("Permite listar as notificações do usuário atual.")]
        [GetEndpoint("[action]")]
        public async Task<IActionResult> Get([FromQuery] PagedRequest request, [FromQuery] bool unreadOnly, CancellationToken cancellationToken)
        {
            var result = await notificationService.GetForCurrentUser(request, unreadOnly, cancellationToken);
            return Http200(result);
        }

        [RequireAccess("Permite consultar a quantidade de notificações não lidas do usuário atual.")]
        [GetEndpoint("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        {
            int count = await notificationService.GetUnreadCountForCurrentUser(cancellationToken);
            return Http200(new { count });
        }

        [RequireAccess("Permite consultar uma notificação por id.")]
        [GetEndpoint("{id:long}")]
        public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
        {
            var result = await notificationService.GetById(id, cancellationToken);
            return result is null ? Http404(Localizer["record.notFound"]) : Http200(result);
        }

        [RequireAccess("Permite cadastrar uma notificação.")]
        [PostEndpoint("[action]")]
        public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
        {
            IActionResult? validationResult = ValidateBody(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var result = await notificationService.Create(request, cancellationToken);
            return Http201(result, Localizer["record.created"]);
        }

        [RequireAccess("Permite marcar uma notificação como lida.")]
        [PostEndpoint("{id:long}/read")]
        public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
        {
            await notificationService.MarkAsRead(id, cancellationToken);
            return Http204();
        }

        [RequireAccess("Permite marcar todas as notificações do usuário atual como lidas.")]
        [PostEndpoint("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        {
            await notificationService.MarkAllAsReadForCurrentUser(cancellationToken);
            return Http204();
        }

        [RequireAccess("Permite excluir uma notificação.")]
        [DeleteEndpoint("{id:long}")]
        public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
        {
            await notificationService.Delete(id, cancellationToken);
            return Http204();
        }

        [RequireAccess("Permite limpar todas as notificações do usuário atual.")]
        [DeleteEndpoint("clear-all")]
        public async Task<IActionResult> ClearAll(CancellationToken cancellationToken)
        {
            await notificationService.ClearAllForCurrentUser(cancellationToken);
            return Http204();
        }
    }
}
