using Microsoft.EntityFrameworkCore;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class SolicitudesTechGovDbContext : DbContext
{
    public SolicitudesTechGovDbContext(DbContextOptions<SolicitudesTechGovDbContext> options) : base(options)
    {
    }

    public DbSet<RequestRecord> Requests => Set<RequestRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    public DbSet<AppUserRecord> AppUsers => Set<AppUserRecord>();
    public DbSet<OrganizationalUnitRecord> OrganizationalUnits => Set<OrganizationalUnitRecord>();
    public DbSet<RequestAttachmentRecord> RequestAttachments => Set<RequestAttachmentRecord>();
    public DbSet<RequestCommentRecord> RequestComments => Set<RequestCommentRecord>();
    public DbSet<UserNotificationRecord> UserNotifications => Set<UserNotificationRecord>();
    public DbSet<AppRoleRecord> AppRoles => Set<AppRoleRecord>();
    public DbSet<UserRoleRecord> UserRoles => Set<UserRoleRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestRecord>().ToTable("Request", "dbo");
        modelBuilder.Entity<RequestRecord>().HasKey(x => x.RequestId);
        modelBuilder.Entity<RequestRecord>().Property(x => x.RequestId).ValueGeneratedNever();

        modelBuilder.Entity<AuditLogRecord>().ToTable("AuditLog", "dbo");
        modelBuilder.Entity<AuditLogRecord>().HasKey(x => x.AuditId);

        modelBuilder.Entity<AppUserRecord>().ToTable("AppUser", "dbo");
        modelBuilder.Entity<AppUserRecord>().HasKey(x => x.UserId);
        modelBuilder.Entity<AppUserRecord>().Property(x => x.UserId).ValueGeneratedNever();

        modelBuilder.Entity<OrganizationalUnitRecord>().ToTable("OrganizationalUnit", "dbo");
        modelBuilder.Entity<OrganizationalUnitRecord>().HasKey(x => x.UnitId);
        modelBuilder.Entity<OrganizationalUnitRecord>().Property(x => x.UnitId).UseIdentityColumn();

        modelBuilder.Entity<RequestAttachmentRecord>().ToTable("RequestAttachment", "dbo");
        modelBuilder.Entity<RequestAttachmentRecord>().HasKey(x => x.AttachmentId);
        modelBuilder.Entity<RequestAttachmentRecord>().Property(x => x.AttachmentId).ValueGeneratedNever();

        modelBuilder.Entity<RequestCommentRecord>().ToTable("RequestComment", "dbo");
        modelBuilder.Entity<RequestCommentRecord>().HasKey(x => x.CommentId);
        modelBuilder.Entity<RequestCommentRecord>().Property(x => x.CommentId).ValueGeneratedNever();

        modelBuilder.Entity<UserNotificationRecord>().ToTable("UserNotification", "dbo");
        modelBuilder.Entity<UserNotificationRecord>().HasKey(x => x.NotificationId);
        modelBuilder.Entity<UserNotificationRecord>().Property(x => x.NotificationId).ValueGeneratedNever();

        modelBuilder.Entity<AppRoleRecord>().ToTable("AppRole", "dbo");
        modelBuilder.Entity<AppRoleRecord>().HasKey(x => x.RoleId);

        modelBuilder.Entity<UserRoleRecord>().ToTable("UserRole", "dbo");
        modelBuilder.Entity<UserRoleRecord>().HasKey(x => x.UserRoleId);
        modelBuilder.Entity<UserRoleRecord>().Property(x => x.UserRoleId).ValueGeneratedOnAdd();
    }
}
