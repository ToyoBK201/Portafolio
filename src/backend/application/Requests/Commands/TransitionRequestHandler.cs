using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests;
using SolicitudesTechGov.Application.Requests.Validation;
using SolicitudesTechGov.Domain;

namespace SolicitudesTechGov.Application.Requests.Commands;

public sealed class TransitionRequestHandler
{
    private static readonly Dictionary<string, (RequestStatus From, RequestStatus To, string[] AllowedRoles, bool RequiresReason)>
        Workflow = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Submit"] = (RequestStatus.Draft, RequestStatus.Submitted, ["Requester", "AreaCoordinator"], false),
            ["ReceiveForAnalysis"] = (RequestStatus.Submitted, RequestStatus.InTicAnalysis, ["TicAnalyst"], false),
            ["SendToApproval"] = (RequestStatus.InTicAnalysis, RequestStatus.PendingApproval, ["TicAnalyst"], false),
            ["RejectFromAnalysis"] = (RequestStatus.InTicAnalysis, RequestStatus.Rejected, ["TicAnalyst"], true),
            ["Approve"] = (RequestStatus.PendingApproval, RequestStatus.Approved, ["InstitutionalApprover"], false),
            ["ReturnToAnalysis"] = (RequestStatus.PendingApproval, RequestStatus.InTicAnalysis, ["InstitutionalApprover"], true),
            ["RejectFromApproval"] = (RequestStatus.PendingApproval, RequestStatus.Rejected, ["InstitutionalApprover"], true),
            ["StartExecution"] = (RequestStatus.Approved, RequestStatus.InProgress, ["Implementer"], false),
            ["RequestValidation"] = (RequestStatus.InProgress, RequestStatus.PendingRequesterValidation, ["Implementer"], false),
            ["AcceptDelivery"] = (RequestStatus.PendingRequesterValidation, RequestStatus.Closed, ["Requester"], false),
            ["ReturnToExecution"] = (RequestStatus.PendingRequesterValidation, RequestStatus.InProgress, ["Requester"], true),
            ["CancelByRequester"] = (RequestStatus.Submitted, RequestStatus.Cancelled, ["Requester"], true),
            ["CancelByAdmin"] = (RequestStatus.InTicAnalysis, RequestStatus.Cancelled, ["SystemAdministrator"], true)
        };

    private readonly IRequestRepository _requestRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserNotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public TransitionRequestHandler(
        IRequestRepository requestRepository,
        IAuditLogRepository auditLogRepository,
        IUserNotificationRepository notifications,
        IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _auditLogRepository = auditLogRepository;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<TransitionResult> HandleAsync(TransitionRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await _requestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return TransitionResult.NotFound();
        }

        if (!Workflow.TryGetValue(command.Transition, out var rule))
        {
            return TransitionResult.BadRequest("Invalid transition.");
        }

        if (!Enum.TryParse<RequestStatus>(request.Status, true, out var currentStatus))
        {
            return TransitionResult.BadRequest("Request has an invalid current status.");
        }

        if (currentStatus != rule.From)
        {
            return TransitionResult.BadRequest(
                $"Transition '{command.Transition}' is not allowed from status '{request.Status}'.");
        }

        if (rule.RequiresReason && string.IsNullOrWhiteSpace(command.Reason))
        {
            return TransitionResult.BadRequest("reason is required for this transition.");
        }

        var roleAllowed = rule.AllowedRoles.Any(x => string.Equals(x, command.ActorRole, StringComparison.OrdinalIgnoreCase));
        if (!roleAllowed && !command.IsSystemAdministrator)
        {
            return TransitionResult.Forbidden();
        }

        if (!command.IsSystemAdministrator &&
            (string.Equals(command.ActorRole, "Requester", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(command.ActorRole, "AreaCoordinator", StringComparison.OrdinalIgnoreCase)) &&
            request.RequesterUserId != command.ActorUserId)
        {
            return TransitionResult.Forbidden();
        }

        if (string.Equals(command.Transition, "Submit", StringComparison.OrdinalIgnoreCase))
        {
            var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
            var submitError = SubmitRequestValidator.ValidateForSubmit(request, utcToday);
            if (submitError is not null)
            {
                return TransitionResult.BadRequest(submitError);
            }
        }

        var now = DateTime.UtcNow;
        var submittedAtUtc = string.Equals(command.Transition, "Submit", StringComparison.OrdinalIgnoreCase)
            ? (DateTime?)now
            : null;

        var updated = await _requestRepository.TryUpdateStatusAsync(
            command.RequestId,
            rule.From.ToString(),
            rule.To.ToString(),
            now,
            submittedAtUtc,
            cancellationToken);

        if (!updated)
        {
            return TransitionResult.Conflict("The request changed state. Refresh and retry.");
        }

        await _auditLogRepository.AddRequestTransitionAsync(
            command.RequestId,
            command.ActorUserId,
            command.ActorRole,
            command.Transition,
            (byte)rule.From,
            (byte)rule.To,
            command.Reason,
            cancellationToken);

        await AppendTransitionNotificationsAsync(command, rule.From, rule.To, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return TransitionResult.NoContent();
    }

    private async Task AppendTransitionNotificationsAsync(
        TransitionRequestCommand command,
        RequestStatus from,
        RequestStatus to,
        CancellationToken cancellationToken)
    {
        const string category = "RequestTransition";
        var info = await _requestRepository.GetNotificationInfoAsync(command.RequestId, cancellationToken);
        if (info is null)
        {
            return;
        }

        var fromLabel = RequestStatusLabels.Es(from);
        var toLabel = RequestStatusLabels.Es(to);
        var shortTitle = info.Title.Length > 100 ? info.Title[..97] + "…" : info.Title;

        var reasonSuffix = "";
        if (!string.IsNullOrWhiteSpace(command.Reason))
        {
            var r = command.Reason.Trim();
            if (r.Length > 200)
            {
                r = r[..197] + "…";
            }

            reasonSuffix = $" Motivo: {r}";
        }

        var body =
            $"«{shortTitle}» pasó de «{fromLabel}» a «{toLabel}». Transición: {command.Transition}.{reasonSuffix}";

        const string title = "Actualización de solicitud";
        await _notifications.AddAsync(info.RequesterUserId, info.RequestId, title, body, category, cancellationToken);

        if (info.AssignedAnalystUserId is { } analystId &&
            analystId != command.ActorUserId &&
            to == RequestStatus.InTicAnalysis)
        {
            await _notifications.AddAsync(
                analystId,
                info.RequestId,
                title,
                $"Análisis TIC: {body}",
                category,
                cancellationToken);
        }

        if (info.AssignedImplementerUserId is { } implId &&
            implId != command.ActorUserId &&
            to == RequestStatus.InProgress)
        {
            await _notifications.AddAsync(
                implId,
                info.RequestId,
                title,
                $"Ejecución: {body}",
                category,
                cancellationToken);
        }
    }
}

public sealed record TransitionResult(int StatusCode, string? Error)
{
    public static TransitionResult NoContent() => new(204, null);
    public static TransitionResult NotFound() => new(404, null);
    public static TransitionResult Forbidden() => new(403, null);
    public static TransitionResult BadRequest(string error) => new(400, error);
    public static TransitionResult Conflict(string error) => new(409, error);
}
