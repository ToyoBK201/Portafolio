using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain.Entities;

namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed class UpdateDraftRequestHandler
{
    private readonly IRequestRepository _requestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDraftRequestHandler(IRequestRepository requestRepository, IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// <c>null</c> si no existe; solicitud actualizada; o el DTO previo si no está en borrador (conflicto de negocio).
    /// </summary>
    public async Task<UpdateDraftHandleResult> HandleAsync(UpdateDraftRequestCommand command, CancellationToken cancellationToken)
    {
        var existing = await _requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (existing is null)
        {
            return UpdateDraftHandleResult.NotFound();
        }

        if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateDraftHandleResult.NotDraft(existing);
        }

        var (nt, nd, nj, rt, pr, unit) = Request.ValidateDraftContent(
            command.Title,
            command.Description,
            command.BusinessJustification,
            command.RequestType,
            command.Priority,
            command.RequestingUnitId);

        var updated = await _requestRepository.TryUpdateDraftAsync(
            command.RequestId,
            nt,
            nd,
            nj,
            (byte)rt,
            (byte)pr,
            unit,
            command.DesiredDate,
            command.SpecificPayloadJson,
            DateTime.UtcNow,
            cancellationToken);

        if (!updated)
        {
            return UpdateDraftHandleResult.NotDraft(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = await _requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        return UpdateDraftHandleResult.Ok(dto!);
    }
}

public sealed class UpdateDraftHandleResult
{
    private UpdateDraftHandleResult(int kind, RequestDto? dto)
    {
        Kind = kind;
        Request = dto;
    }

    /// <summary>0 = Ok, 1 = NotFound, 2 = NotDraft</summary>
    public int Kind { get; }

    public RequestDto? Request { get; }

    public static UpdateDraftHandleResult Ok(RequestDto dto) => new(0, dto);

    public static UpdateDraftHandleResult NotFound() => new(1, null);

    public static UpdateDraftHandleResult NotDraft(RequestDto current) => new(2, current);
}
