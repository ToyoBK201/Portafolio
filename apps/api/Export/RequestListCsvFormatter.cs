using System.Text;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Api.Export;

internal static class RequestListCsvFormatter
{
    /// <summary>
    /// CSV UTF-8 con BOM (Excel reconoce acentos). Cabeceras en inglés para contrato estable.
    /// </summary>
    public static byte[] ToUtf8Bom(IReadOnlyList<RequestDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "requestId,title,status,requestType,priority,requestingUnitId,requesterUserId,desiredDate,createdAtUtc");

        foreach (var r in items)
        {
            sb.Append(Escape(r.RequestId.ToString())).Append(',');
            sb.Append(Escape(r.Title)).Append(',');
            sb.Append(Escape(r.Status)).Append(',');
            sb.Append(r.RequestType.ToString()).Append(',');
            sb.Append(r.Priority.ToString()).Append(',');
            sb.Append(r.RequestingUnitId.ToString()).Append(',');
            sb.Append(Escape(r.RequesterUserId.ToString())).Append(',');
            sb.Append(Escape(r.DesiredDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "")).Append(',');
            sb.AppendLine(Escape(r.CreatedAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture)));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string Escape(string value)
    {
        var mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        var v = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return mustQuote ? $"\"{v}\"" : v;
    }
}
