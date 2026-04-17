using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlAuditLogRepository : IAuditLogRepository
{
    private readonly SolicitudesTechGovDbContext _dbContext;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public SqlAuditLogRepository(SolicitudesTechGovDbContext dbContext, ICorrelationIdAccessor correlationIdAccessor)
    {
        _dbContext = dbContext;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<PagedResult<AdminAuditLogEntryDto>> ListGlobalAsync(
        int page,
        int pageSize,
        string? entityType,
        string? action,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize is < 1 or > 200)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
        }

        var q = _dbContext.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var et = entityType.Trim();
            q = q.Where(x => x.EntityType == et);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var a = action.Trim();
            q = q.Where(x => x.Action == a);
        }

        var total = await q.CountAsync(cancellationToken);
        var offset = (page - 1) * pageSize;
        var rows = await q
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.AuditId)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.ConvertAll(static x => new AdminAuditLogEntryDto(
            x.AuditId,
            x.OccurredAtUtc,
            x.CorrelationId,
            x.ActorUserId,
            x.ActorRole,
            x.Action,
            x.EntityType,
            x.EntityId,
            x.RequestId,
            StatusLabel(x.FromStatusId),
            StatusLabel(x.ToStatusId),
            x.PayloadSummary,
            x.Success));

        return new PagedResult<AdminAuditLogEntryDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<RequestAuditEntryDto>> ListForRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.AuditId)
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(static x => new RequestAuditEntryDto(
            x.AuditId,
            x.OccurredAtUtc,
            x.CorrelationId,
            x.ActorUserId,
            x.ActorRole,
            x.Action,
            StatusLabel(x.FromStatusId),
            StatusLabel(x.ToStatusId),
            x.PayloadSummary));
    }

    private static string? StatusLabel(byte? id) =>
        id is null ? null : ((RequestStatus)id.Value).ToString();

    public async Task AddRequestCreatedAsync(
        Guid requestId,
        Guid actorUserId,
        byte toStatusId,
        CancellationToken cancellationToken)
    {
        var audit = new AuditLogRecord
        {
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = _correlationIdAccessor.GetCorrelationId(),
            ActorUserId = actorUserId,
            ActorRole = "Requester",
            Action = "RequestCreated",
            EntityType = "Request",
            EntityId = requestId.ToString(),
            RequestId = requestId,
            ToStatusId = toStatusId,
            PayloadSummary = "{\"event\":\"CreateDraft\"}",
            Success = true
        };

        await _dbContext.AuditLogs.AddAsync(audit, cancellationToken);
    }

    public async Task AddRequestTransitionAsync(
        Guid requestId,
        Guid actorUserId,
        string actorRole,
        string transition,
        byte fromStatusId,
        byte toStatusId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var payloadSummary = string.IsNullOrWhiteSpace(reason)
            ? $$"""{"transition":"{{transition}}"}"""
            : $$"""{"transition":"{{transition}}","reason":"{{reason!.Trim()}}"}""";

        var audit = new AuditLogRecord
        {
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = _correlationIdAccessor.GetCorrelationId(),
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Action = transition,
            EntityType = "Request",
            EntityId = requestId.ToString(),
            RequestId = requestId,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            PayloadSummary = payloadSummary,
            Success = true
        };

        await _dbContext.AuditLogs.AddAsync(audit, cancellationToken);
    }

    public async Task AddAdminCatalogEventAsync(
        Guid actorUserId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string? payloadSummary,
        CancellationToken cancellationToken)
    {
        var audit = new AuditLogRecord
        {
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = _correlationIdAccessor.GetCorrelationId(),
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            RequestId = null,
            FromStatusId = null,
            ToStatusId = null,
            PayloadSummary = payloadSummary,
            Success = true
        };

        await _dbContext.AuditLogs.AddAsync(audit, cancellationToken);
    }
}
