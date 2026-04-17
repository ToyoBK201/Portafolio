using Microsoft.EntityFrameworkCore;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SqlRequestCommentRepository : IRequestCommentRepository
{
    private const int MaxBodyLength = 8000;
    private readonly SolicitudesTechGovDbContext _db;

    public SqlRequestCommentRepository(SolicitudesTechGovDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RequestCommentDto>> ListForRequestAsync(
        Guid requestId,
        bool includeInternal,
        CancellationToken cancellationToken)
    {
        var query =
            from c in _db.RequestComments.AsNoTracking()
            join u in _db.AppUsers.AsNoTracking() on c.AuthorUserId equals u.UserId into uj
            from u in uj.DefaultIfEmpty()
            where c.RequestId == requestId && (includeInternal || !c.IsInternal)
            orderby c.CreatedAtUtc
            select new { c, AuthorDisplayName = u != null ? u.DisplayName : "" };

        var rows = await query.ToListAsync(cancellationToken);
        var list = new List<RequestCommentDto>(rows.Count);
        foreach (var row in rows)
        {
            var c = row.c;
            list.Add(new RequestCommentDto(
                c.CommentId,
                c.AuthorUserId,
                string.IsNullOrWhiteSpace(row.AuthorDisplayName) ? c.AuthorUserId.ToString() : row.AuthorDisplayName,
                c.Body,
                c.IsInternal,
                c.CreatedAtUtc));
        }

        return list;
    }

    public async Task<(RequestCommentDto? Item, string? ErrorMessage)> TryAddAndSaveAsync(
        Guid requestId,
        Guid authorUserId,
        string body,
        bool isInternal,
        CancellationToken cancellationToken)
    {
        var trimmed = body.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (null, "El comentario no puede estar vacío.");
        }

        if (trimmed.Length > MaxBodyLength)
        {
            return (null, $"El comentario supera los {MaxBodyLength} caracteres.");
        }

        await EnsureAuthorExistsAsync(authorUserId, cancellationToken);

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var record = new RequestCommentRecord
        {
            CommentId = id,
            RequestId = requestId,
            AuthorUserId = authorUserId,
            Body = trimmed,
            IsInternal = isInternal,
            CreatedAtUtc = now
        };

        await _db.RequestComments.AddAsync(record, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var name = await _db.AppUsers.AsNoTracking()
            .Where(x => x.UserId == authorUserId)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new RequestCommentDto(
            id,
            authorUserId,
            string.IsNullOrWhiteSpace(name) ? authorUserId.ToString() : name!,
            trimmed,
            isInternal,
            now);
        return (dto, null);
    }

    private async Task EnsureAuthorExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _db.AppUsers.AnyAsync(x => x.UserId == userId, cancellationToken);
        if (exists)
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        var email = $"{userId:N}@local.test";
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO dbo.AppUser (UserId, Email, DisplayName, ExternalSubjectId, IsActive, CreatedAtUtc)
               VALUES ({userId}, {email}, {"Usuario MVP"}, {null}, {true}, {createdAt});",
            cancellationToken);
    }
}
