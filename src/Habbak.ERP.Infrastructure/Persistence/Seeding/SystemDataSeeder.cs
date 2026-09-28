using Habbak.ERP.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Bootstrap of system-wide reference data — the sidebar MenuItem tree and DataGrid/field-label text
/// (00-System-Wide-Corrections-01.md, sections 3 and 4). Runs on every startup. MenuItems is seeded
/// per missing item (added in Batch B5, after Batch B1-B5's new items had silently never reached a
/// database seeded before they existed) so a later release's new screen still gets in; FieldLabels
/// still only seeds when completely empty. Neither ever touches/overwrites an existing row, so
/// edits later made from the (not yet built) settings admin screen are never overwritten. Builds its
/// own AppDbContext directly
/// (rather than resolving one from the app's DI container) so the audit interceptor uses the
/// non-HTTP SystemCurrentCompanyContext instead of the request-bound one, which would otherwise
/// throw outside a request.
/// </summary>
public static class SystemDataSeeder
{
    public static async Task SeedAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"))
            .AddInterceptors(new AuditSaveChangesInterceptor(new SystemCurrentCompanyContext()));

        await using var db = new AppDbContext(optionsBuilder.Options, new SystemCurrentCompanyContext());

        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return;
            }

            // Per-item, not "only if the whole table is empty": a real database seeded before some
            // screen existed would otherwise never receive that screen's MenuItem once MenuItemSeedData
            // grows to include it (caught in Batch B5 — HR_JOB_GRADES and 7 other Batch B1-B5 screens
            // had silently never reached the real HabbakErp database because MenuItems already had
            // rows from long before Batch B1). Two passes, same shape as MenuItemSeedData.Build()
            // itself: groups (RouteKey null) first, so a still-missing leaf's parent already has a
            // real Id by the second pass, whether that parent already existed or was just inserted.
            var freshMenuItems = MenuItemSeedData.Build();
            var existingMenuItemCodes = await db.MenuItems.Select(m => m.Code).ToListAsync(cancellationToken);

            foreach (var group in freshMenuItems.Where(m => m.RouteKey is null && !existingMenuItemCodes.Contains(m.Code)))
            {
                db.MenuItems.Add(new Domain.Common.MenuItem
                {
                    Code = group.Code, NameAr = group.NameAr, NameEn = group.NameEn, DisplayOrder = group.DisplayOrder, RouteKey = null, IsActive = true
                });
            }
            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            var parentIdsByCode = await db.MenuItems.Where(m => m.RouteKey == null).ToDictionaryAsync(m => m.Code, m => m.Id, cancellationToken);
            foreach (var leaf in freshMenuItems.Where(m => m.RouteKey is not null && !existingMenuItemCodes.Contains(m.Code)))
            {
                db.MenuItems.Add(new Domain.Common.MenuItem
                {
                    Code = leaf.Code, NameAr = leaf.NameAr, NameEn = leaf.NameEn,
                    ParentId = parentIdsByCode[leaf.Parent!.Code], DisplayOrder = leaf.DisplayOrder, RouteKey = leaf.RouteKey, IsActive = true
                });
            }
            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.FieldLabels.AnyAsync(cancellationToken))
            {
                db.FieldLabels.AddRange(FieldLabelSeedData.Build());
                await db.SaveChangesAsync(cancellationToken);
            }

            // Screen registry (Docs/Modules/00-Project-Overview.md §12.2, Phase 2) — per-item like
            // MenuItems above, so a later release's new screen still reaches an already-seeded DB.
            var existingScreenCodes = await db.Screens.Select(s => s.Code).ToListAsync(cancellationToken);
            var missingScreens = ScreenSeedData.Build().Where(s => !existingScreenCodes.Contains(s.Code)).ToList();
            if (missingScreens.Count > 0)
            {
                db.Screens.AddRange(missingScreens);
                await db.SaveChangesAsync(cancellationToken);
            }

            // HR Core "seed-only" lookups (Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1) —
            // system-wide, no admin screen in this phase, so they must ship pre-populated.
            if (!await db.Nationalities.AnyAsync(cancellationToken))
            {
                db.Nationalities.AddRange(HrCoreLookupSeedData.BuildNationalities());
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.RelationshipTypes.AnyAsync(cancellationToken))
            {
                db.RelationshipTypes.AddRange(HrCoreLookupSeedData.BuildRelationshipTypes());
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.MilitaryStatuses.AnyAsync(cancellationToken))
            {
                db.MilitaryStatuses.AddRange(HrCoreLookupSeedData.BuildMilitaryStatuses());
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.QualificationTypes.AnyAsync(cancellationToken))
            {
                db.QualificationTypes.AddRange(HrCoreLookupSeedData.BuildQualificationTypes());
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await db.TerminationReasons.AnyAsync(cancellationToken))
            {
                db.TerminationReasons.AddRange(HrCoreLookupSeedData.BuildTerminationReasons());
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (SqlException)
        {
            // The database exists but hasn't been migrated yet (a fresh clone before the first
            // `dotnet ef database update`, or the ApiTests factory, which creates/migrates its own
            // throwaway database only after the host has finished starting) — nothing to seed
            // until the schema is there; the app's own startup/tests surface the real problem.
        }
    }
}
