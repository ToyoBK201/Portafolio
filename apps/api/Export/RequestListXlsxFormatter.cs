using ClosedXML.Excel;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Api.Export;

internal static class RequestListXlsxFormatter
{
    public static byte[] ToWorkbookBytes(IReadOnlyList<RequestDto> items)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Solicitudes");

        var headers = new[]
        {
            "requestId", "title", "status", "requestType", "priority", "requestingUnitId", "requesterUserId",
            "desiredDate", "createdAtUtc"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        var row = 2;
        foreach (var r in items)
        {
            ws.Cell(row, 1).Value = r.RequestId.ToString();
            ws.Cell(row, 2).Value = r.Title;
            ws.Cell(row, 3).Value = r.Status;
            ws.Cell(row, 4).Value = r.RequestType;
            ws.Cell(row, 5).Value = r.Priority;
            ws.Cell(row, 6).Value = r.RequestingUnitId;
            ws.Cell(row, 7).Value = r.RequesterUserId.ToString();
            ws.Cell(row, 8).Value = r.DesiredDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "";
            ws.Cell(row, 9).Value = r.CreatedAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            row++;
        }

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
