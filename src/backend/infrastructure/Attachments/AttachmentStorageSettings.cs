namespace SolicitudesTechGov.Infrastructure.Attachments;

public sealed class AttachmentStorageSettings
{
    /// <summary>Ruta relativa al ContentRoot de la API (p. ej. <c>data/attachments</c>).</summary>
    public string RootRelativePath { get; set; } = "data/attachments";

    public long MaxBytesPerFile { get; set; } = 10 * 1024 * 1024;
    public int MaxFilesPerRequest { get; set; } = 10;

    /// <summary>Lista separada por comas, sin punto.</summary>
    public string AllowedExtensions { get; set; } = "pdf,png,jpg,jpeg,docx,xlsx,csv,zip";
}
