using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Infrastructure.Attachments;
using SolicitudesTechGov.Infrastructure.Persistence;

namespace SolicitudesTechGov.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.Configure<AttachmentStorageSettings>(configuration.GetSection("Attachments"));
        services.AddSingleton<LocalAttachmentFileStore>();
        services.AddDbContext<SolicitudesTechGovDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IRequestRepository, SqlRequestRepository>();
        services.AddScoped<IAuditLogRepository, SqlAuditLogRepository>();
        services.AddScoped<IRequestAttachmentRepository, SqlAttachmentRepository>();
        services.AddScoped<IRequestCommentRepository, SqlRequestCommentRepository>();
        services.AddScoped<IUserNotificationRepository, SqlUserNotificationRepository>();
        services.AddScoped<IOrganizationalUnitCatalogRepository, SqlOrganizationalUnitCatalogRepository>();
        services.AddScoped<IAppUserAdminRepository, SqlAppUserAdminRepository>();
        services.AddScoped<IUserAuthenticationService, SqlUserAuthenticationService>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
