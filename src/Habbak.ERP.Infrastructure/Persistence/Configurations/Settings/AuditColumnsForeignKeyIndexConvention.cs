using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

/// <summary>
/// EF's usual rule — every foreign key gets an index — except for the audit columns
/// (CreatedBy/UpdatedBy/DeletedBy → Users, see <see cref="UserReferences"/>). Those are written on
/// every insert and update of every table and read one row at a time, and users are never
/// hard-deleted, so an index would cost three extra writes per row and speed up nothing. Removing
/// the index after the fact does not work: the stock convention puts it straight back.
/// </summary>
public sealed class AuditColumnsForeignKeyIndexConvention(ProviderConventionSetBuilderDependencies dependencies)
    : ForeignKeyIndexConvention(dependencies)
{
    private static readonly HashSet<string> AuditColumns =
        [nameof(IAuditableEntity.CreatedBy), nameof(IAuditableEntity.UpdatedBy), nameof(IAuditableEntity.DeletedBy)];

    protected override IConventionIndex? CreateIndex(
        IReadOnlyList<IConventionProperty> properties, bool unique, IConventionEntityTypeBuilder entityTypeBuilder) =>
        properties.Count == 1 && AuditColumns.Contains(properties[0].Name)
            ? null
            : base.CreateIndex(properties, unique, entityTypeBuilder);
}
