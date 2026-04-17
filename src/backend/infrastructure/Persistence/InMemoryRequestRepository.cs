using System.Collections.Concurrent;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;
using SolicitudesTechGov.Domain.Entities;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class InMemoryRequestRepository : IRequestRepository
{
    private readonly ConcurrentDictionary<Guid, Request> _store = new();

    public Task AddAsync(Request request, CancellationToken cancellationToken)
    {
        _store[request.RequestId] = request;
        return Task.CompletedTask;
    }

    public Task<RequestDto?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(requestId, out var request);
        if (request is null)
        {
            return Task.FromResult<RequestDto?>(null);
        }

        var dto = new RequestDto(
            request.RequestId,
            request.Title,
            request.Description,
            request.BusinessJustification,
            (byte)request.RequestType,
            (byte)request.Priority,
            request.RequestingUnitId,
            request.RequesterUserId,
            request.Status.ToString(),
            request.DesiredDate,
            request.CreatedAtUtc,
            request.SpecificPayloadJson);

        return Task.FromResult<RequestDto?>(dto);
    }

    public Task<RequestNotificationInfo?> GetNotificationInfoAsync(Guid requestId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_store.TryGetValue(requestId, out var request))
        {
            return Task.FromResult<RequestNotificationInfo?>(null);
        }

        return Task.FromResult<RequestNotificationInfo?>(
            new RequestNotificationInfo(
                request.RequestId,
                request.Title,
                request.RequesterUserId,
                null,
                null));
    }

    public Task<bool> TryUpdateStatusAsync(
        Guid requestId,
        string expectedCurrentStatus,
        string nextStatus,
        DateTime updatedAtUtc,
        DateTime? submittedAtUtc,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _ = submittedAtUtc;

        if (!_store.TryGetValue(requestId, out var request))
        {
            return Task.FromResult(false);
        }

        if (!string.Equals(request.Status.ToString(), expectedCurrentStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        if (!Enum.TryParse<RequestStatus>(nextStatus, true, out var parsed))
        {
            return Task.FromResult(false);
        }

        request.ApplyStatusChange(parsed, updatedAtUtc);
        return Task.FromResult(true);
    }

    public Task<bool> TryUpdateDraftAsync(
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
        _ = cancellationToken;

        if (!_store.TryGetValue(requestId, out var request))
        {
            return Task.FromResult(false);
        }

        if (request.Status != RequestStatus.Draft)
        {
            return Task.FromResult(false);
        }

        request.ApplyValidatedDraftUpdate(
            title,
            description,
            businessJustification,
            (RequestType)requestTypeId,
            (Priority)priorityId,
            requestingUnitId,
            desiredDate,
            specificPayloadJson,
            updatedAtUtc);

        return Task.FromResult(true);
    }

    public Task<PagedResult<RequestDto>> ListAsync(
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
        _ = cancellationToken;

        IEnumerable<Request> query = _store.Values;

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => string.Equals(x.Status.ToString(), status, StringComparison.OrdinalIgnoreCase));
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

        var ordered = ApplyOrdering(query, sortBy, sortDirection);
        var totalCount = ordered.Count();
        var offset = (page - 1) * pageSize;

        var items = ordered
            .Skip(offset)
            .Take(pageSize)
            .Select(request => new RequestDto(
                request.RequestId,
                request.Title,
                request.Description,
                request.BusinessJustification,
                (byte)request.RequestType,
                (byte)request.Priority,
                request.RequestingUnitId,
                request.RequesterUserId,
                request.Status.ToString(),
                request.DesiredDate,
                request.CreatedAtUtc,
                request.SpecificPayloadJson))
            .ToList();

        return Task.FromResult(new PagedResult<RequestDto>(items, totalCount, page, pageSize));
    }

    public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        Guid? requesterUserIdScopeOnly,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        IEnumerable<Request> query = _store.Values;

        if (requesterUserIdScopeOnly.HasValue)
        {
            query = query.Where(x => x.RequesterUserId == requesterUserIdScopeOnly.Value);
        }

        var dict = query
            .GroupBy(x => x.Status.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, int>>(dict);
    }

    private static IEnumerable<Request> ApplyOrdering(
        IEnumerable<Request> query,
        string? sortBy,
        string? sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var key = sortBy?.Trim().ToLowerInvariant();

        return key switch
        {
            "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => desc ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc)
        };
    }
}
