using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Persistence.Interceptors;
using Habbak.ERP.Infrastructure.Security;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Habbak.ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddVaultClient(configuration, environment);
        services.AddSingleton<IPiiHasher, HmacPiiHasher>();

        // Keyed so EncryptedStringConverter's key ring never shares a purpose with the unkeyed
        // ISecretProtector (2FA secrets, API/Auth/DataProtectionSecretProtector.cs) — rotating one
        // never touches the other (Docs/Implementation/HR-Core-Plan.md §0.3).
        services.AddKeyedSingleton<ISecretProtector, PiiSecretProtector>("HR.PII");

        // Scoped, not Singleton: it depends on the per-request ICurrentCompanyContext.
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseSqlServer(configuration.GetConnectionString("Default"))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IJournalEntryNumberGenerator, JournalEntryNumberGenerator>();
        services.AddScoped<IVoucherNumberGenerator, VoucherNumberGenerator>();
        services.AddScoped<IPostingService, PostingService>();
        services.AddScoped<Habbak.ERP.Application.Posting.IPostingFailureRecorder, PostingFailureRecorder>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddHostedService<MaintenanceHostedService>();
        services.AddHostedService<AttendanceDeviceProcessingHostedService>();

        // Supplier resolves for real now that Purchasing owns that mapping; Customer/Employee still
        // fail clearly pending Sales/HR (see CounterpartyAccountResolver's own XML doc).
        services.AddScoped<ICounterpartyAccountResolver, CounterpartyAccountResolver>();

        return services;
    }
}
