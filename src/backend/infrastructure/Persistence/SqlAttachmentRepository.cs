using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Infrastructure.Attachments;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlAttachmentRepository : IRequestAttachmentRepository
{
    private readonly SolicitudesTechGovDbContext _db;
    private readonly LocalAttachmentFileStore _fileStore;
    private readonly AttachmentStorageSettings _settings;

    public SqlAttachmentRepository(
        SolicitudesTechGovDbContext db,
        LocalAttachmentFileStore fileStore,
        IOptions<AttachmentStorageSettings> options)
    {
        _db = db;
        _fileStore = fileStore;
        _settings = options.Value;
    }

    public async Task<IReadOnlyList<RequestAttachmentDto>> ListActiveAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var rows = await _db.RequestAttachments.AsNoTracking()
            .Where(x => x.RequestId == requestId && !x.IsDeleted)
            .OrderBy(x => x.UploadedAtUtc)
            .Select(x => new RequestAttachmentDto(
                x.AttachmentId,
                x.FileName,
                x.ContentType,
                x.SizeBytes,
                x.UploadedAtUtc))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public Task<int> CountActiveAsync(Guid requestId, CancellationToken cancellationToken) =>
        _db.RequestAttachments.CountAsync(x => x.RequestId == requestId && !x.IsDeleted, cancellationToken);

    public async Task<(RequestAttachmentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid uploadedByUserId,
        string originalFileName,
        string contentType,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (await CountActiveAsync(requestId, cancellationToken) >= _settings.MaxFilesPerRequest)
        {
            return (null, $"Se permiten como máximo {_settings.MaxFilesPerRequest} adjuntos por solicitud.");
        }

        var safeName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return (null, "El nombre del archivo es obligatorio.");
        }

        if (safeName.Length > 260)
        {
            safeName = safeName[..260];
        }

        var ext = NormalizeExtension(Path.GetExtension(safeName));
        if (string.IsNullOrEmpty(ext))
        {
            return (null, "El archivo debe tener una extensión permitida.");
        }

        var allowed = ParseAllowedExtensions();
        if (!allowed.Contains(ext))
        {
            return (null, $"Extensión no permitida. Permitidas: {_settings.AllowedExtensions}");
        }

        if (contentLength > _settings.MaxBytesPerFile)
        {
            return (null, $"El archivo supera el tamaño máximo de {_settings.MaxBytesPerFile} bytes.");
        }

        var attachmentId = Guid.NewGuid();
        var relativePath = $"{requestId:N}/{attachmentId:N}.{ext}";

        var limited = new StreamWithHardLimit(content, _settings.MaxBytesPerFile, contentLength > 0 ? contentLength : null, leaveInnerOpen: true);
        try
        {
            await _fileStore.WriteAsync(relativePath, limited, cancellationToken);
            var written = limited.TotalBytesRead;
            if (written == 0)
            {
                _fileStore.TryDelete(relativePath);
                return (null, "El archivo está vacío.");
            }

            var type = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
            if (type.Length > 200)
            {
                type = type[..200];
            }

            var now = DateTime.UtcNow;
            var record = new RequestAttachmentRecord
            {
                AttachmentId = attachmentId,
                RequestId = requestId,
                FileName = safeName,
                ContentType = type,
                SizeBytes = written,
                StoragePath = relativePath,
                UploadedByUserId = uploadedByUserId,
                UploadedAtUtc = now,
                IsDeleted = false
            };

            await _db.RequestAttachments.AddAsync(record, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            var dto = new RequestAttachmentDto(attachmentId, safeName, type, written, now);
            return (dto, null);
        }
        catch (InvalidOperationException ex)
        {
            _fileStore.TryDelete(relativePath);
            return (null, ex.Message);
        }
        catch (DbUpdateException)
        {
            _fileStore.TryDelete(relativePath);
            throw;
        }
        catch
        {
            _fileStore.TryDelete(relativePath);
            throw;
        }
        finally
        {
            limited.Dispose();
        }
    }

    public async Task<AttachmentFileInfo?> GetActiveByIdAsync(Guid requestId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var row = await _db.RequestAttachments.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.RequestId == requestId && x.AttachmentId == attachmentId && !x.IsDeleted,
                cancellationToken);
        if (row is null)
        {
            return null;
        }

        return new AttachmentFileInfo(row.FileName, row.ContentType, row.StoragePath);
    }

    private HashSet<string> ParseAllowedExtensions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in _settings.AllowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var e = NormalizeExtension(part.StartsWith('.') ? part : "." + part);
            if (!string.IsNullOrEmpty(e))
            {
                set.Add(e);
            }
        }

        return set;
    }

    private static string NormalizeExtension(string? extensionWithDot)
    {
        if (string.IsNullOrWhiteSpace(extensionWithDot))
        {
            return string.Empty;
        }

        var e = extensionWithDot.Trim().TrimStart('.').ToLowerInvariant();
        return string.IsNullOrEmpty(e) ? string.Empty : e;
    }

    /// <summary>
    /// Limita lectura a maxBytes; si expectedTotal está fijado, no lee más de eso.
    /// </summary>
    private sealed class StreamWithHardLimit : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;
        private readonly long? _expectedTotal;
        private readonly bool _leaveInnerOpen;
        private long _read;

        public StreamWithHardLimit(Stream inner, long maxBytes, long? expectedTotal, bool leaveInnerOpen)
        {
            _inner = inner;
            _maxBytes = maxBytes;
            _expectedTotal = expectedTotal;
            _leaveInnerOpen = leaveInnerOpen;
        }

        public long TotalBytesRead => _read;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var n = _inner.Read(buffer);
            ApplyRead(n);
            return n;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            ApplyRead(n);
            return n;
        }

        private void ApplyRead(int n)
        {
            if (n <= 0)
            {
                return;
            }

            checked
            {
                _read += n;
            }

            if (_read > _maxBytes)
            {
                throw new InvalidOperationException("El archivo supera el tamaño máximo permitido.");
            }

            if (_expectedTotal.HasValue && _read > _expectedTotal.Value)
            {
                throw new InvalidOperationException("El tamaño del contenido no coincide con lo declarado.");
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveInnerOpen)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
