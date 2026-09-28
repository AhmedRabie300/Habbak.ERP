using System.Text.Json;
using System.Text.Json.Nodes;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Settings;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Habbak.ERP.API.Auth;

/// <summary>
/// Hides sensitive fields (FieldPermissionCatalog) of <paramref name="entityType"/> from users whose
/// roles may not view them on the request's screen (ICurrentScreen): every JSON property with that field's name in the response — at any
/// depth — comes back as null. Only on the controllers that return that entity, so a "phone" on
/// some unrelated object elsewhere is left alone.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class MaskFieldsAttribute(string entityType) : Attribute, IFilterFactory
{
    public string EntityType { get; } = entityType;
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new MaskFieldsFilter(
            EntityType,
            serviceProvider.GetRequiredService<IUserAccessService>(),
            serviceProvider.GetRequiredService<Habbak.ERP.Application.Common.Interfaces.ICurrentScreen>(),
            serviceProvider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions);

    private sealed class MaskFieldsFilter(
        string entityType, IUserAccessService access, Habbak.ERP.Application.Common.Interfaces.ICurrentScreen screen, JsonSerializerOptions jsonOptions) : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult { Value: not null } result
                && context.HttpContext.User.Identity?.IsAuthenticated == true
                && FieldPermissionCatalog.FieldsOf(entityType).ToList() is { Count: > 0 } fields)
            {
                var rights = await access.GetCurrentAsync(context.HttpContext.RequestAborted);
                var hidden = fields.Where(f => !rights.FieldOn(screen.Code, entityType, f).View).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (hidden.Count > 0)
                {
                    var node = JsonSerializer.SerializeToNode(result.Value, result.Value.GetType(), jsonOptions);
                    Mask(node, hidden);
                    result.Value = node;
                    result.DeclaredType = typeof(JsonNode);
                }
            }

            await next();
        }

        private static void Mask(JsonNode? node, HashSet<string> hidden)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var name in obj.Select(p => p.Key).ToList())
                    {
                        if (hidden.Contains(name))
                        {
                            obj[name] = null;
                        }
                        else
                        {
                            Mask(obj[name], hidden);
                        }
                    }
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        Mask(item, hidden);
                    }
                    break;
            }
        }
    }
}
