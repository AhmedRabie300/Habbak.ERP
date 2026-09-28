using System.Text;
using System.Text.Json.Serialization;
using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Logging;
using Habbak.ERP.API.Middleware;
using Habbak.ERP.Application;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Infrastructure;
using Habbak.ERP.Infrastructure.Persistence.Seeding;
using Habbak.ERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using VaultSharp;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (00-Project-Overview.md §22, Docs/Implementation/HR-Core-Plan.md §0.5). Replaces
// the default Microsoft.Extensions.Logging providers wholesale — ILogger<T> calls throughout the
// existing code keep working unchanged (Serilog.Extensions.Logging, pulled in by Serilog.AspNetCore,
// bridges them). PiiDestructuringPolicy strips [PiiField] properties before they ever reach a sink,
// so a future `logger.LogInformation("{@Employee}", employee)` never leaks PII, even at Debug level.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Destructure.With<PiiDestructuringPolicy>()
    .Enrich.FromLogContext());

// Add services to the container.

// Enums as strings ("Other", not 4) on both request binding and response bodies — matches every
// DTO/enum in this API consistently and is far more debuggable from the TypeScript frontend.
// Order -3000: ahead of [ApiController]'s model-state filter (-2000), so a user without the permission
// gets 403, not a 400 describing the fields of a form they may not submit.
builder.Services.AddControllers(options => options.Filters.Add<ScreenPermissionFilter>(-3000))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Every controller in this API declares its own inline request/response records (established
    // convention — see e.g. AddLineRequest on both ChecksController and BankReconciliationsController),
    // so Swashbuckle's default schemaId (the bare type name) collides constantly across ~80+
    // controllers. Use the full namespace-qualified name instead — guarantees uniqueness regardless
    // of how many controllers reuse the same short name for their own nested request type.
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    // Lets "Authorize" in Swagger UI attach a Bearer token to every request — get one from
    // POST /api/v1/auth/login (accessToken in the response), paste as "Bearer {token}".
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "أدخل: Bearer {token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCompanyContext, CurrentCompanyContext>();
builder.Services.AddScoped<ICurrentUserRoles, ClaimsUserRoles>();
builder.Services.AddScoped<IRequestInfo, HttpRequestInfo>();
builder.Services.AddScoped<ICurrentScreen, HttpCurrentScreen>();

// Encrypts the authenticator secrets and signs the two-factor sign-in challenge. Storage: Vault when
// configured (Docs/Implementation/HR-Core-Plan.md §0.6 — VaultDataProtectionRepository), else the
// local filesystem as before — deliberately mutually exclusive (whichever XmlRepository ends up set
// on KeyManagementOptions wins), so the two storage backends can never silently conflict. DPAPI
// wrapping only makes sense for the filesystem path: it ties the key ring to this one Windows
// machine, which would defeat the entire point of centralizing keys in Vault for a multi-instance
// deployment, so it is skipped when Vault is configured.
var vaultConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Vault:Address"]);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Habbak.ERP");

if (vaultConfigured)
{
    builder.Services.AddOptions<KeyManagementOptions>()
        .Configure<IVaultClient>((options, vaultClient) =>
            options.XmlRepository = new VaultDataProtectionRepository(vaultClient, builder.Configuration));
}
else
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys")));
    if (OperatingSystem.IsWindows())
    {
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
    }
}

builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// JWT Bearer (00-Project-Overview.md, section 7), issued by POST /api/v1/auth/login. The signing
// key in appsettings.Development.json is dev-only — production must supply its own "Jwt:Key".
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        // Keep claim names as issued ("role", not the long ClaimTypes.Role URI) — AuthClaims reads them by these names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHealthChecks().AddCheck<Habbak.ERP.API.HealthChecks.VaultHealthCheck>("vault", tags: ["vault"]);

// Dev-only: lets the Vite dev server (a different origin/port) call this API locally.
// Real deployment serves the built frontend from the same origin, so this won't be needed there.
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// System-wide reference data (00-System-Wide-Corrections-01.md, sections 3-4) — idempotent,
// only inserts on a genuinely empty table.
await SystemDataSeeder.SeedAsync(builder.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCorsPolicy);
}

app.UseExceptionHandler();

// The Vite dev server always calls the API over plain HTTP (never navigates to it directly), so
// redirecting to HTTPS here only breaks that call whenever an HTTPS port happens to be bound too
// (e.g. Visual Studio's "https" launch profile) — skip it in Development; keep it for real deployments.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/vault", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("vault")
});

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the API host in tests
// (00-Project-Overview.md, section 25).
public partial class Program;
