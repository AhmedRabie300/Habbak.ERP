using FluentValidation;
using Habbak.ERP.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Habbak.ERP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        AddPostingEngine(services, assembly);
        AddApprovalEngine(services, assembly);
        services.AddScoped<Notifications.INotificationService, Notifications.NotificationService>();
        // Phase 3 POS Integration — نداء مباشر بعد SaveChanges بتاع OpenShift/CloseShift (Phase-3-Research.md §3.1).
        services.AddScoped<Common.Interfaces.ITimeEntryFeedService, Attendance.Services.TimeEntryFeedService>();

        services.AddMemoryCache();
        services.AddScoped<Settings.Auth.SessionIssuer>();
        services.AddScoped<Settings.Access.IUserAccessService, Settings.Access.UserAccessService>();
        services.AddScoped<Settings.Access.IAuditFieldPolicy, Settings.Access.AuditFieldPolicy>();

        return services;
    }

    /// <summary>
    /// The real Approval Workflow Engine (00-Project-Overview.md §12, Phase 2), replacing the
    /// former NullApprovalWorkflowService placeholder. Outcome handlers are discovered the same
    /// way posting resolvers are above — a new one is a new class implementing
    /// Approvals.Outcomes.IApprovalOutcomeHandler, nothing else to register.
    /// </summary>
    private static void AddApprovalEngine(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        services.AddScoped<Common.Interfaces.IApprovalWorkflowService, Approvals.Services.ApprovalWorkflowService>();
        services.AddScoped<Approvals.Services.IApprovalStepResolutionService, Approvals.Services.ApprovalStepResolutionService>();

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            if (typeof(Approvals.Outcomes.IApprovalOutcomeHandler).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(Approvals.Outcomes.IApprovalOutcomeHandler), type);
            }
        }
    }

    /// <summary>
    /// Resolvers are discovered, not listed (spec 4.4): a new one is a new class and nothing else,
    /// so there is no second place to forget to register it in.
    /// </summary>
    private static void AddPostingEngine(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            if (typeof(Posting.Resolvers.IPostingAccountResolver).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(Posting.Resolvers.IPostingAccountResolver), type);
            }

            if (typeof(Posting.Resolvers.IPostingCostCenterResolver).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(Posting.Resolvers.IPostingCostCenterResolver), type);
            }
        }

        services.AddScoped<Posting.Resolvers.IPostingResolverRegistry, Posting.Resolvers.PostingResolverRegistry>();
        services.AddScoped<Posting.IPostingTemplateEngine, Posting.PostingTemplateEngine>();
        services.AddScoped<POS.Posting.IPOSPostingService, POS.Posting.POSPostingService>();
    }
}
