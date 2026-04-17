using System.Text.Json;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Domain;

namespace SolicitudesTechGov.Application.Requests.Validation;

/// <summary>
/// Validación al enviar (Submit): campos comunes y <c>specificPayload</c> por <see cref="RequestType"/> (docs/04).
/// </summary>
public static class SubmitRequestValidator
{
    public static string? ValidateForSubmit(RequestDto request, DateOnly utcToday)
    {
        var title = request.Title.Trim();
        if (title.Length is < 5 or > 200)
        {
            return "El título debe tener entre 5 y 200 caracteres.";
        }

        var description = request.Description.Trim();
        if (description.Length is < 20 or > 8000)
        {
            return "La descripción debe tener entre 20 y 8000 caracteres.";
        }

        var justification = request.BusinessJustification.Trim();
        if (justification.Length < 20)
        {
            return "La justificación de negocio debe tener al menos 20 caracteres.";
        }

        if (!Enum.IsDefined(typeof(RequestType), request.RequestType))
        {
            return "Tipo de solicitud no válido.";
        }

        if (!Enum.IsDefined(typeof(Priority), request.Priority))
        {
            return "Prioridad no válida.";
        }

        if (request.RequestingUnitId <= 0)
        {
            return "Unidad solicitante no válida.";
        }

        if (request.DesiredDate is { } d && d < utcToday)
        {
            return "La fecha deseada no puede ser anterior al día actual (UTC).";
        }

        var payload = request.SpecificPayloadJson?.Trim();
        if (string.IsNullOrEmpty(payload))
        {
            return "Falta el bloque de datos específicos (specificPayload) para este tipo de solicitud.";
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return "El payload específico no es JSON válido.";
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return "El payload específico debe ser un objeto JSON.";
            }

            return (RequestType)request.RequestType switch
            {
                RequestType.HardwareAcquisition => ValidateHardware(root),
                RequestType.SoftwareLicensing => ValidateSoftware(root),
                RequestType.SystemDevelopment => ValidateSystemDevelopment(root),
                RequestType.InfrastructureConnectivity => ValidateInfrastructure(root),
                RequestType.MajorTechnicalSupport => ValidateMajorSupport(root),
                RequestType.InformationSecurity => ValidateInformationSecurity(root),
                RequestType.DataInteroperability => ValidateDataInteroperability(root),
                _ => "Tipo de solicitud no soportado."
            };
        }
    }

    private static string? ValidateHardware(JsonElement o)
    {
        if (!TryGetNonNegativeInt(o, "quantity", out var qty) || qty < 1)
        {
            return "Hardware: 'quantity' debe ser un entero mayor o igual a 1.";
        }

        if (!TryGetTrimmedString(o, "specification", out var spec) || spec.Length < 20)
        {
            return "Hardware: 'specification' es obligatorio (mínimo 20 caracteres).";
        }

        if (!TryGetTrimmedString(o, "replacementJustification", out var repl) || repl.Length < 10)
        {
            return "Hardware: 'replacementJustification' es obligatorio (mínimo 10 caracteres).";
        }

        if (!TryGetTrimmedString(o, "installationLocation", out var loc) || loc.Length == 0)
        {
            return "Hardware: 'installationLocation' es obligatorio.";
        }

        return null;
    }

    private static string? ValidateSoftware(JsonElement o)
    {
        if (!TryGetTrimmedString(o, "productName", out var pn) || pn.Length == 0)
        {
            return "Software: 'productName' es obligatorio.";
        }

        if (!TryGetTrimmedString(o, "licenseModel", out var lm) || lm.Length == 0)
        {
            return "Software: 'licenseModel' es obligatorio.";
        }

        if (!TryGetNonNegativeInt(o, "seatOrUserCount", out var seats) || seats < 1)
        {
            return "Software: 'seatOrUserCount' debe ser mayor o igual a 1.";
        }

        if (!TryGetTrimmedString(o, "environment", out var env) || env.Length == 0)
        {
            return "Software: 'environment' es obligatorio.";
        }

        if (o.TryGetProperty("directoryOrSsoIntegration", out var ssoEl) &&
            ssoEl.ValueKind == JsonValueKind.True)
        {
            if (!TryGetTrimmedString(o, "directoryOrSsoDetails", out var det) || det.Length < 3)
            {
                return "Software: si 'directoryOrSsoIntegration' es true, 'directoryOrSsoDetails' es obligatorio.";
            }
        }

        return null;
    }

    private static string? ValidateSystemDevelopment(JsonElement o)
    {
        if (!TryGetTrimmedString(o, "functionalScope", out var fs) || fs.Length < 50)
        {
            return "Desarrollo: 'functionalScope' es obligatorio (mínimo 50 caracteres).";
        }

        if (!TryGetNonNegativeInt(o, "affectedUsersEstimate", out _))
        {
            return "Desarrollo: 'affectedUsersEstimate' debe ser un número entero mayor o igual a 0.";
        }

        if (!HasNonEmptyStringArray(o, "systemsAffected"))
        {
            return "Desarrollo: 'systemsAffected' debe ser un array con al menos un elemento.";
        }

        if (!HasNonEmptyStringArray(o, "acceptanceCriteria"))
        {
            return "Desarrollo: 'acceptanceCriteria' debe ser un array con al menos un elemento.";
        }

        return null;
    }

    private static string? ValidateInfrastructure(JsonElement o)
    {
        if (!TryGetTrimmedString(o, "technicalDescription", out var td) || td.Length < 50)
        {
            return "Infraestructura: 'technicalDescription' es obligatorio (mínimo 50 caracteres).";
        }

        if (!TryGetTrimmedString(o, "maintenanceWindow", out var mw) || mw.Length == 0)
        {
            return "Infraestructura: 'maintenanceWindow' es obligatorio.";
        }

        return null;
    }

    private static string? ValidateMajorSupport(JsonElement o)
    {
        if (!TryGetTrimmedString(o, "symptoms", out var s) || s.Length < 20)
        {
            return "Soporte: 'symptoms' es obligatorio (mínimo 20 caracteres).";
        }

        if (!TryGetTrimmedString(o, "impactDescription", out var imp) || imp.Length == 0)
        {
            return "Soporte: 'impactDescription' es obligatorio.";
        }

        if (!HasNonEmptyStringArray(o, "affectedAreas"))
        {
            return "Soporte: 'affectedAreas' debe ser un array con al menos un elemento.";
        }

        return null;
    }

    private static string? ValidateInformationSecurity(JsonElement o)
    {
        if (!TryGetTrimmedString(o, "assetOrSystem", out var a) || a.Length == 0)
        {
            return "Seguridad: 'assetOrSystem' es obligatorio.";
        }

        if (!TryGetTrimmedString(o, "controlType", out var c) || c.Length == 0)
        {
            return "Seguridad: 'controlType' es obligatorio.";
        }

        if (!TryGetTrimmedString(o, "findingOrContext", out var f) || f.Length < 30)
        {
            return "Seguridad: 'findingOrContext' es obligatorio (mínimo 30 caracteres).";
        }

        return null;
    }

    private static string? ValidateDataInteroperability(JsonElement o)
    {
        if (!HasNonEmptyStringArray(o, "sourceSystems"))
        {
            return "Interoperabilidad: 'sourceSystems' debe ser un array con al menos un elemento.";
        }

        if (!HasNonEmptyStringArray(o, "targetSystems"))
        {
            return "Interoperabilidad: 'targetSystems' debe ser un array con al menos un elemento.";
        }

        if (!TryGetTrimmedString(o, "frequency", out var fr) || fr.Length == 0)
        {
            return "Interoperabilidad: 'frequency' es obligatorio.";
        }

        if (!TryGetTrimmedString(o, "dataQualityExpectation", out var dq) || dq.Length < 20)
        {
            return "Interoperabilidad: 'dataQualityExpectation' es obligatorio (mínimo 20 caracteres).";
        }

        if (!TryGetTrimmedString(o, "dataOwnerName", out var owner) || owner.Length == 0)
        {
            return "Interoperabilidad: 'dataOwnerName' es obligatorio.";
        }

        return null;
    }

    private static bool TryGetTrimmedString(JsonElement o, string name, out string value)
    {
        value = "";
        if (!o.TryGetProperty(name, out var p))
        {
            return false;
        }

        if (p.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = p.GetString()?.Trim() ?? "";
        return true;
    }

    private static bool TryGetNonNegativeInt(JsonElement o, string name, out int value)
    {
        value = 0;
        if (!o.TryGetProperty(name, out var p))
        {
            return false;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value))
        {
            return value >= 0;
        }

        return false;
    }

    private static bool HasNonEmptyStringArray(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var el in p.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(el.GetString()))
            {
                return true;
            }
        }

        return false;
    }
}
