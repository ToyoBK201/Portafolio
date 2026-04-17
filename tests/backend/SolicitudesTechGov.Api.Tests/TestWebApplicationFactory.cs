using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SolicitudesTechGov.Application.Abstractions;
using SolicitudesTechGov.Application.Requests.Dtos;
using SolicitudesTechGov.Infrastructure.Attachments;
using SolicitudesTechGov.Infrastructure.Persistence;

namespace SolicitudesTechGov.Api.Tests;

/// <summary>
/// Sustituye SQL por repositorios en memoria para pruebas HTTP sin contenedor de BD.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(DbContextOptions<SolicitudesTechGovDbContext>));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(SolicitudesTechGovDbContext));

            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IAuditLogRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestAttachmentRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(LocalAttachmentFileStore));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IRequestCommentRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUserNotificationRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUnitOfWork));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IOrganizationalUnitCatalogRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IAppUserAdminRepository));
            ServiceCollectionTestExtensions.RemoveServiceDescriptors(services, typeof(IUserAuthenticationService));

            services.AddSingleton<IRequestRepository, InMemoryRequestRepository>();
            services.AddSingleton<IAuditLogRepository, NoOpAuditLogRepository>();
            services.AddSingleton<IRequestAttachmentRepository, NoOpAttachmentRepositoryStub>();
            services.AddSingleton<IRequestCommentRepository, NoOpRequestCommentRepositoryStub>();
            services.AddSingleton<IUserNotificationRepository, NoOpUserNotificationRepositoryStub>();
            services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();
            services.AddSingleton<IOrganizationalUnitCatalogRepository, InMemoryOrganizationalUnitCatalogRepository>();
            services.AddSingleton<IAppUserAdminRepository, InMemoryAppUserAdminRepository>();
            services.AddSingleton<IUserAuthenticationService, NoOpUserAuthenticationService>();
        });
    }

    private sealed class NoOpUserAuthenticationService : IUserAuthenticationService
    {
        public Task<LoginOutcome> TryLoginAsync(string email, string password, CancellationToken cancellationToken)
        {
            _ = email;
            _ = password;
            _ = cancellationToken;
            return Task.FromResult<LoginOutcome>(new LoginFailed(LoginFailureReason.InvalidCredentials));
        }
    }

    private sealed class NoOpAuditLogRepository : IAuditLogRepository
    {
        public Task<PagedResult<AdminAuditLogEntryDto>> ListGlobalAsync(
            int page,
            int pageSize,
            string? entityType,
            string? action,
            CancellationToken cancellationToken)
        {
            _ = entityType;
            _ = action;
            _ = cancellationToken;
            return Task.FromResult(new PagedResult<AdminAuditLogEntryDto>(
                Array.Empty<AdminAuditLogEntryDto>(),
                0,
                page,
                pageSize));
        }

        public Task<IReadOnlyList<RequestAuditEntryDto>> ListForRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<RequestAuditEntryDto>>(Array.Empty<RequestAuditEntryDto>());
        }

        public Task AddRequestCreatedAsync(
            Guid requestId,
            Guid actorUserId,
            byte toStatusId,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = toStatusId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddRequestTransitionAsync(
            Guid requestId,
            Guid actorUserId,
            string actorRole,
            string transition,
            byte fromStatusId,
            byte toStatusId,
            string? reason,
            CancellationToken cancellationToken)
        {
            _ = requestId;
            _ = actorUserId;
            _ = actorRole;
            _ = transition;
            _ = fromStatusId;
            _ = toStatusId;
            _ = reason;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task AddAdminCatalogEventAsync(
            Guid actorUserId,
            string actorRole,
            string action,
            string entityType,
            string entityId,
            string? payloadSummary,
            CancellationToken cancellationToken)
        {
            _ = actorUserId;
            _ = actorRole;
            _ = action;
            _ = entityType;
            _ = entityId;
            _ = payloadSummary;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
