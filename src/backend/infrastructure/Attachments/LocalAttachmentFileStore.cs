using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SolicitudesTechGov.Infrastructure.Attachments;

public sealed class LocalAttachmentFileStore(IOptions<AttachmentStorageSettings> options, IHostEnvironment env)
{
    private readonly AttachmentStorageSettings _cfg = options.Value;

    public string AbsoluteRoot =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, _cfg.RootRelativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

    public string ToPhysicalPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(AbsoluteRoot, normalized));
    }

    public async Task WriteAsync(string relativePath, Stream stream, CancellationToken cancellationToken)
    {
        var full = ToPhysicalPath(relativePath);
        EnsureUnderRoot(full);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await stream.CopyToAsync(fs, cancellationToken);
    }

    public Stream OpenRead(string relativePath)
    {
        var full = ToPhysicalPath(relativePath);
        EnsureUnderRoot(full);
        return new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
    }

    public void TryDelete(string relativePath)
    {
        try
        {
            var full = ToPhysicalPath(relativePath);
            EnsureUnderRoot(full);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }
        catch
        {
            // best effort
        }
    }

    private void EnsureUnderRoot(string fullPath)
    {
        if (!fullPath.StartsWith(AbsoluteRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, AbsoluteRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }
    }
}
