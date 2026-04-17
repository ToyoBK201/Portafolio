using SolicitudesTechGov.Domain;

namespace SolicitudesTechGov.Application.Requests;

public static class RequestStatusLabels
{
    public static string Es(RequestStatus status) => status switch
    {
        RequestStatus.Draft => "Borrador",
        RequestStatus.Submitted => "Enviada",
        RequestStatus.InTicAnalysis => "En análisis TIC",
        RequestStatus.PendingApproval => "Pendiente de aprobación",
        RequestStatus.Approved => "Aprobada",
        RequestStatus.Rejected => "Rechazada",
        RequestStatus.InProgress => "En ejecución",
        RequestStatus.PendingRequesterValidation => "Pendiente de tu validación",
        RequestStatus.Closed => "Cerrada",
        RequestStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };
}
