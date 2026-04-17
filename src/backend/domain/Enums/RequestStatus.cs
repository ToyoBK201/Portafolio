namespace SolicitudesTechGov.Domain;

public enum RequestStatus : byte
{
    Draft = 1,
    Submitted = 2,
    InTicAnalysis = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    InProgress = 7,
    PendingRequesterValidation = 8,
    Closed = 9,
    Cancelled = 10
}
