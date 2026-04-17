using SolicitudesTechGov.Application.Requests.Commands;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Infrastructure.Persistence;

namespace SolicitudesTechGov.Application.Tests;

public static class CreateDraftRequestHandlerTests
{
    // Note: convert this smoke test to xUnit/NUnit after installing the SDK packages.
    public static async Task<bool> SmokeCreateDraftAsync()
    {
        var handler = new CreateDraftRequestHandler(
            new InMemoryRequestRepository(),
            new NoOpAuditLogRepository(),
            new InMemoryUnitOfWork());

        var result = await handler.HandleAsync(
            new CreateDraftRequestCommand(
                "Solicitud de licencias Office",
                "Se requiere licencia para personal de nueva contratación en la unidad.",
                "Garantizar continuidad operativa y cumplimiento institucional.",
                RequestType: 2,
                Priority: 2,
                RequestingUnitId: 3,
                RequesterUserId: Guid.NewGuid(),
                DesiredDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
                SpecificPayloadJson: "{\"productName\":\"Office\"}"),
            CancellationToken.None);

        return result.Status == "Draft";
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task<PagedResult<AdminAuditLogEntryDto>> ListGlobalAsync(
            int page,
            int pageSize,
            string? entityType,
            string? action,
            CancellationToken cancellationToken)
        {
            _ = entityType;
            _ = action;
            _ = cancellationToken;
            return Task.FromResult(new PagedResult<AdminAuditLogEntryDto>(
                Array.Empty<AdminAuditLogEntryDto>(),
                0,
                page,
                pageSize));
        }

        public Task<IReadOnlyList<RequestAuditEntryDto>> ListForRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<RequestAuditEntryDto>>(Array.Empty<RequestAuditEntryDto>());
        }

        public Task AddRequestCreatedAsync(Guid requestId, Guid actorUserId, byte toStatusId, CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = toStatusId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddRequestTransitionAsync(
            Guid requestId,
            Guid actorUserId,
            string actorRole,
            string transition,
            byte fromStatusId,
            byte toStatusId,
            string? reason,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = actorRole;
            _ = transition;
            _ = fromStatusId;
            _ = toStatusId;
            _ = reason;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddAdminCatalogEventAsync(
            Guid actorUserId,
            string actorRole,
            string action,
            string entityType,
            string entityId,
            string? payloadSummary,
            CancellationToken cancellationToken)
        {
            _ = actorUserId;
            _ = actorRole;
            _ = action;
            _ = entityType;
            _ = entityId;
            _ = payloadSummary;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
