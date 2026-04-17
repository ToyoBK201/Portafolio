using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;
using SolicitudesTechGov.Domain.Entities;

namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed class CreateDraftRequestHandler
{
    private readonly IRequestRepository _requestRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftRequestHandler(
        IRequestRepository requestRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequestDto> HandleAsync(CreateDraftRequestCommand command, CancellationToken cancellationToken)
    {
        var request = Request.CreateDraft(
            command.Title,
            command.Description,
            command.BusinessJustification,
            (RequestType)command.RequestType,
            (Priority)command.Priority,
            command.RequestingUnitId,
            command.RequesterUserId,
            command.DesiredDate,
            command.SpecificPayloadJson);

        await _requestRepository.AddAsync(request, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogRepository.AddRequestCreatedAsync(
            request.RequestId,
            request.RequesterUserId,
            (byte)request.Status,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RequestDto(
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
    }
}
