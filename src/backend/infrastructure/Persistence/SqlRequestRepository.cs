using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain.Entities;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlRequestRepository : IRequestRepository
{
    private readonly SolicitudesTechGovDbContext _dbContext;

    public SqlRequestRepository(SolicitudesTechGovDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Request request, CancellationToken cancellationToken)
    {
        await EnsureRequesterAndUnitExistAsync(request, cancellationToken);

        var record = new RequestRecord
        {
            RequestId = request.RequestId,
            Title = request.Title,
            Description = request.Description,
            BusinessJustification = request.BusinessJustification,
            RequestTypeId = (byte)request.RequestType,
            PriorityId = (byte)request.Priority,
            RequestingUnitId = request.RequestingUnitId,
            RequesterUserId = request.RequesterUserId,
            StatusId = (byte)request.Status,
            DesiredDate = request.DesiredDate,
            SpecificPayloadJson = request.SpecificPayloadJson,
            CreatedAtUtc = request.CreatedAtUtc,
            UpdatedAtUtc = request.UpdatedAtUtc
        };

        await _dbContext.Requests.AddAsync(record, cancellationToken);

    }

    public async Task<RequestDto?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestId == requestId, cancellationToken);

        if (record is null)
        {
            return null;
        }

        return new RequestDto(
            record.RequestId,
            record.Title,
            record.Description,
            record.BusinessJustification,
            record.RequestTypeId,
            record.PriorityId,
            record.RequestingUnitId,
            record.RequesterUserId,
            ((SolicitudesTechGov.Domain.RequestStatus)record.StatusId).ToString(),
            record.DesiredDate,
            record.CreatedAtUtc,
            record.SpecificPayloadJson);
    }

    public async Task<RequestNotificationInfo?> GetNotificationInfoAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.Requests
            .AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .Select(x => new RequestNotificationInfo(
                x.RequestId,
                x.Title,
                x.RequesterUserId,
                x.AssignedAnalystUserId,
                x.AssignedImplementerUserId))
            .FirstOrDefaultAsync(cancellationToken);
        return row;
    }

    public async Task<bool> TryUpdateStatusAsync(
        Guid requestId,
        string expectedCurrentStatus,
        string nextStatus,
        DateTime updatedAtUtc,
        DateTime? submittedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SolicitudesTechGov.Domain.RequestStatus>(expectedCurrentStatus, true, out var fromStatus))
        {
            throw new InvalidOperationException($"Unknown status '{expectedCurrentStatus}'.");
        }

        if (!Enum.TryParse<SolicitudesTechGov.Domain.RequestStatus>(nextStatus, true, out var toStatus))
        {
            throw new InvalidOperationException($"Unknown status '{nextStatus}'.");
        }

        var affectedRows = await _dbContext.Requests
            .Where(x => x.RequestId == requestId && x.StatusId == (byte)fromStatus)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(x => x.StatusId, (byte)toStatus)
                    .SetProperty(x => x.UpdatedAtUtc, updatedAtUtc)
                    .SetProperty(x => x.SubmittedAtUtc, x => submittedAtUtc ?? x.SubmittedAtUtc),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<bool> TryUpdateDraftAsync(
        Guid requestId,
        string title,
        string description,
        string businessJustification,
        byte requestTypeId,
        byte priorityId,
        int requestingUnitId,
        DateOnly? desiredDate,
        string? specificPayloadJson,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = string.IsNullOrWhiteSpace(specificPayloadJson) ? null : specificPayloadJson;

        var affected = await _dbContext.Requests
            .Where(x => x.RequestId == requestId && x.StatusId == (byte)SolicitudesTechGov.Domain.RequestStatus.Draft)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(x => x.Title, title)
                    .SetProperty(x => x.Description, description)
                    .SetProperty(x => x.BusinessJustification, businessJustification)
                    .SetProperty(x => x.RequestTypeId, requestTypeId)
                    .SetProperty(x => x.PriorityId, priorityId)
                    .SetProperty(x => x.RequestingUnitId, requestingUnitId)
                    .SetProperty(x => x.DesiredDate, desiredDate)
                    .SetProperty(x => x.SpecificPayloadJson, payload)
                    .SetProperty(x => x.UpdatedAtUtc, updatedAtUtc),
                cancellationToken);

        return affected > 0;
    }

    public async Task<PagedResult<RequestDto>> ListAsync(
        string? status,
        Guid? requesterUserId,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Requests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<SolicitudesTechGov.Domain.RequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.StatusId == (byte)parsedStatus);
        }

        if (requesterUserId.HasValue)
        {
            query = query.Where(x => x.RequesterUserId == requesterUserId.Value);
        }

        if (createdFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= createdFromUtc.Value);
        }

        if (createdToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= createdToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = ApplyOrdering(query, sortBy, sortDirection);
        var offset = (page - 1) * pageSize;

        var items = await orderedQuery
            .Skip(offset)
            .Take(pageSize)
            .Select(record => new RequestDto(
                record.RequestId,
                record.Title,
                record.Description,
                record.BusinessJustification,
                record.RequestTypeId,
                record.PriorityId,
                record.RequestingUnitId,
                record.RequesterUserId,
                ((SolicitudesTechGov.Domain.RequestStatus)record.StatusId).ToString(),
                record.DesiredDate,
                record.CreatedAtUtc,
                record.SpecificPayloadJson))
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestDto>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        Guid? requesterUserIdScopeOnly,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Requests.AsNoTracking().AsQueryable();

        if (requesterUserIdScopeOnly.HasValue)
        {
            query = query.Where(x => x.RequesterUserId == requesterUserIdScopeOnly.Value);
        }

        var groups = await query
            .GroupBy(x => x.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            var name = ((SolicitudesTechGov.Domain.RequestStatus)g.StatusId).ToString();
            dict[name] = g.Count;
        }

        return dict;
    }

    private static IQueryable<RequestRecord> ApplyOrdering(
        IQueryable<RequestRecord> query,
        string? sortBy,
        string? sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var key = sortBy?.Trim().ToLowerInvariant();

        return key switch
        {
            "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "status" => desc ? query.OrderByDescending(x => x.StatusId) : query.OrderBy(x => x.StatusId),
            _ => desc ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc)
        };
    }

    private async Task EnsureRequesterAndUnitExistAsync(Request request, CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.AppUsers
            .AnyAsync(x => x.UserId == request.RequesterUserId, cancellationToken);

        if (!userExists)
        {
            var createdAt = DateTime.UtcNow;
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO dbo.AppUser (UserId, Email, DisplayName, ExternalSubjectId, IsActive, CreatedAtUtc)
                   VALUES ({request.RequesterUserId}, {request.RequesterUserId + "@local.test"}, {"Usuario MVP"}, {null}, {true}, {createdAt});",
                cancellationToken);
        }

        var unitExists = await _dbContext.OrganizationalUnits
            .AnyAsync(x => x.UnitId == request.RequestingUnitId, cancellationToken);

        if (!unitExists)
        {
            var code = $"UNIT-{request.RequestingUnitId}";
            var name = $"Unidad {request.RequestingUnitId}";
            var createdAt = DateTime.UtcNow;
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"SET IDENTITY_INSERT dbo.OrganizationalUnit ON;
                   INSERT INTO dbo.OrganizationalUnit (UnitId, Code, Name, IsActive, CreatedAtUtc)
                   VALUES ({request.RequestingUnitId}, {code}, {name}, {true}, {createdAt});
                   SET IDENTITY_INSERT dbo.OrganizationalUnit OFF;",
                cancellationToken);
        }
    }
}
