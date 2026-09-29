using System.Text;
using System.Text.Json;
using IoBuild.Api.Analytics.Application.Internal.CommandServices;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Infrastructure.InfluxDB;
using IoBuild.Api.Analytics.Interfaces.REST;
using IoBuild.Api.Contracts;
using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Infrastructure.InfluxDB;
using IoBuild.Api.Devices.Infrastructure.Mqtt;
using IoBuild.Api.Devices.Interfaces.REST;
using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.IAM.Interfaces.REST;
using IoBuild.Api.Observability;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Application.Internal.CommandServices;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using IoBuild.Api.Profiles.Interfaces.REST;
using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Application.Internal.QueryServices;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;
using IoBuild.Api.Publishing.Interfaces.REST;
using IoBuild.Api.Readiness;
using IoBuild.Api.Shared.Application.Cutover;
using IoBuild.Api.Shared.Interfaces.REST;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Application.Internal.QueryServices;
using IoBuild.Api.Subscriptions.Domain.Repositories;
using IoBuild.Api.Subscriptions.Domain.Services;
using IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Repositories;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using IoBuild.Api.Subscriptions.Interfaces.REST;
using IoBuild.Api.Workflows;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var rawConn = builder.Configuration.GetConnectionString("IoBuild")
    ?? builder.Configuration["ConnectionStrings:IoBuild"]
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__IoBuild")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings:IoBuild")
    ?? Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_IOBUILD");

var connectionString = !string.IsNullOrWhiteSpace(rawConn)
    ? rawConn
    : (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        ? "Server=mysql-monolith;Port=3306;Database=iobuild;User=root;Password=iobuild;"
        : "Server=localhost;Port=3306;Database=iobuild;User=root;Password=root;");

var rawSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("Jwt__Secret")
    ?? Environment.GetEnvironmentVariable("JWT_SECRET");
var jwtSecret = !string.IsNullOrWhiteSpace(rawSecret) ? rawSecret : "iobuild-development-secret-must-be-replaced-before-production";

Console.WriteLine($"[IoBuild] Environment: {builder.Environment.EnvironmentName} | Container: {Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")}");
Console.WriteLine($"[IoBuild] Using ConnectionString: {connectionString.Split(';').FirstOrDefault(p => p.StartsWith("Server", StringComparison.OrdinalIgnoreCase))};Database=iobuild;");

ServerVersion serverVersion;
try
{
    serverVersion = ServerVersion.AutoDetect(connectionString);
}
catch
{
    serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
}

builder.Services.AddDbContext<IoBuildDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));
builder.Services.AddSingleton<MigrationReadiness>();
builder.Services.AddSingleton<CutoverReadiness>();
builder.Services.AddScoped<ICutoverHarness, CutoverHarness>();
builder.Services.AddScoped<IMigrationRunner, EfMigrationRunner>();
builder.Services.AddScoped<WorkflowExecutor>();
builder.Services.AddScoped<IIntegrationDispatchQueue, IntegrationDispatchQueue>();
builder.Services.AddScoped<IWorkflow<RegisterUser, int>, RegisterUserWorkflow>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton(new JwtTokenIssuer(jwtSecret));
builder.Services.AddScoped<IamService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ProjectCommandService>();
builder.Services.AddScoped<IProjectCommandService>(sp => sp.GetRequiredService<ProjectCommandService>());
builder.Services.AddScoped<IProjectQueryService, ProjectQueryService>();
builder.Services.AddScoped<IUnitRepository, UnitRepository>();
builder.Services.AddScoped<IUnitCommandService, UnitCommandService>();
builder.Services.AddScoped<IUnitQueryService, UnitQueryService>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IClientCommandService, ClientCommandService>();
builder.Services.AddScoped<IClientQueryService, ClientQueryService>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IPlanCommandService, PlanCommandService>();
builder.Services.AddScoped<IPlanQueryService, PlanQueryService>();
builder.Services.AddScoped<CoreBusinessService>();
builder.Services.AddSingleton<MqttDeviceTransport>();
builder.Services.AddSingleton<IDeviceMqttPublisher>(services => services.GetRequiredService<MqttDeviceTransport>());
builder.Services.AddHostedService(services => services.GetRequiredService<MqttDeviceTransport>());
builder.Services.AddHttpClient<IInfluxTelemetrySink, InfluxHttpTelemetrySink>(c => c.Timeout = TimeSpan.FromMilliseconds(500));
builder.Services.AddHttpClient<ILiveEnergyService, LiveEnergyService>(c => c.Timeout = TimeSpan.FromMilliseconds(500));
builder.Services.AddHttpClient<ILiveDeviceStatusService, LiveDeviceStatusService>(c => c.Timeout = TimeSpan.FromMilliseconds(500));
builder.Services.AddScoped<IAnalyticsQueryService, AnalyticsQueryService>();
builder.Services.AddScoped<AnalyticsProjectionImporter>();
builder.Services.AddScoped<DeviceCommandService>();
builder.Services.AddScoped<DeviceTelemetryService>();
builder.Services.AddScoped<DeviceRegistryService>();
builder.Services.AddHostedService<DeviceRegistryAnnouncer>();
builder.Services.AddHttpClient<ICloudinaryUploader, CloudinaryHttpUploader>();
builder.Services.AddHttpClient<IPaymentProvider, StripeHttpPaymentProvider>();
builder.Services.AddScoped<ProfilePhotoWorkflow>();
builder.Services.AddScoped<StripeWebhookProcessor>(services => new StripeWebhookProcessor(
    services.GetRequiredService<IoBuildDbContext>(),
    builder.Configuration["Stripe:WebhookSecret"] ?? string.Empty));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCorsPolicy", policy =>
    {
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (configuredOrigins is { Length: > 0 } && configuredOrigins.Contains("*"))
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            var origins = configuredOrigins is { Length: > 0 }
                ? configuredOrigins
                : new[] { "http://localhost:5173", "http://localhost:3000" };

            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});
builder.Services.AddIoBuildObservability(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : string.Empty;
            if (string.IsNullOrEmpty(token) || await context.HttpContext.RequestServices.GetRequiredService<IamService>().IsRevokedAsync(token)) context.Fail("The token has been revoked.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - All Bounded Contexts",
        Version = "v1",
        Description = "Vista completa y unificada de todos los Bounded Contexts del Monolito Modular IoBuild."
    });
    options.SwaggerDoc("publishing", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - Publishing",
        Version = "v1",
        Description = "Bounded Context de Publishing: Projects, Units y Clients."
    });
    options.SwaggerDoc("devices", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - Devices",
        Version = "v1",
        Description = "Bounded Context de Devices: Catálogo, Registro, Telemetría y Comandos IoT."
    });
    options.SwaggerDoc("iam", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - IAM",
        Version = "v1",
        Description = "Bounded Context de IAM: Autenticación, Sesiones y Usuarios."
    });
    options.SwaggerDoc("subscriptions", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - Subscriptions",
        Version = "v1",
        Description = "Bounded Context de Subscriptions: Planes, Suscripciones y Pagos Stripe."
    });
    options.SwaggerDoc("profiles", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - Profiles",
        Version = "v1",
        Description = "Bounded Context de Profiles: Perfiles y Fotos."
    });
    options.SwaggerDoc("analytics", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IoBuild - Analytics",
        Version = "v1",
        Description = "Bounded Context de Analytics: Métricas y Series temporales."
    });

    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (docName == "v1") return true;

        var relativePath = apiDesc.RelativePath?.ToLowerInvariant() ?? string.Empty;
        var tags = apiDesc.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()
            .SelectMany(m => m.Tags)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        return docName switch
        {
            "publishing" => tags.Any(t => t.Contains("project") || t.Contains("unit") || t.Contains("client") || t.Contains("publishing"))
                            || relativePath.Contains("projects") || relativePath.Contains("units") || relativePath.Contains("clients"),
            "devices" => tags.Any(t => t.Contains("device")) || relativePath.Contains("devices") || relativePath.Contains("custom-device-types"),
            "iam" => tags.Any(t => t.Contains("iam") || t.Contains("auth") || t.Contains("session") || t.Contains("user"))
                     || relativePath.Contains("authentication") || relativePath.Contains("sessions") || relativePath.Contains("users"),
            "subscriptions" => tags.Any(t => t.Contains("subscription") || t.Contains("plan"))
                               || relativePath.Contains("subscriptions") || relativePath.Contains("plans") || relativePath.Contains("webhooks/stripe"),
            "profiles" => tags.Any(t => t.Contains("profile")) || relativePath.Contains("profiles"),
            "analytics" => tags.Any(t => t.Contains("analytic")) || relativePath.Contains("analytics"),
            _ => false
        };
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese 'Bearer {token}'",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todos (All Bounded Contexts)");
    c.SwaggerEndpoint("/swagger/publishing/swagger.json", "Publishing (Projects, Units, Clients)");
    c.SwaggerEndpoint("/swagger/devices/swagger.json", "Devices");
    c.SwaggerEndpoint("/swagger/iam/swagger.json", "IAM");
    c.SwaggerEndpoint("/swagger/subscriptions/swagger.json", "Subscriptions & Plans");
    c.SwaggerEndpoint("/swagger/profiles/swagger.json", "Profiles");
    c.SwaggerEndpoint("/swagger/analytics/swagger.json", "Analytics");
    c.RoutePrefix = "swagger";
});

app.UseForwardedHeaders();
app.UseCors("GatewayCorsPolicy");

if (builder.Configuration.GetValue<bool>("Migrations:ApplyOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var coordinator = new MigrationStartupCoordinator(scope.ServiceProvider.GetRequiredService<IMigrationRunner>(), scope.ServiceProvider.GetRequiredService<MigrationReadiness>());
    await coordinator.ApplyAsync(app.Lifetime.ApplicationStopping);

    var dbContext = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
    await DataSeeder.SeedAsync(dbContext, app.Lifetime.ApplicationStopping);
}

app.Use(async (context, next) =>
{
    var readiness = context.RequestServices.GetRequiredService<MigrationReadiness>();
    if (context.Request.Path.StartsWithSegments("/api/v1") && readiness.ShouldBlockRequests)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "migration_readiness_failed" });
        return;
    }
    await next();
});
app.Use(async (context, next) =>
{
    var cutover = context.RequestServices.GetRequiredService<CutoverReadiness>();
    var isWrite = context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
    var isCutoverControl = context.Request.Path.StartsWithSegments("/api/v1/cutover");
    if (context.Request.Path.StartsWithSegments("/api/v1") && isWrite && cutover.ShouldBlockWrites && !isCutoverControl)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "cutover_freeze_active" });
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

// ── Root & Swagger Redirect ──
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ── Health & Contracts (Shared) ──
app.MapGet("/health", (MigrationReadiness readiness) => readiness.IsReady ? Results.Ok(new { status = "ready" }) : Results.Json(new { status = "not-ready", reason = readiness.FailureReason }, statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapGet("/api/v1/contracts", () => Results.Ok(LegacyApiContractCatalog.All));

// ── Per-BC endpoint maps (DDD Interfaces/REST) ──
// Each BC's endpoints are defined in its own Interfaces/REST/*Endpoints.cs.
// Program.cs stays thin — composition root only.
app.MapIamEndpoints();
app.MapPublishingEndpoints();
app.MapProfilesEndpoints();
app.MapSubscriptionsEndpoints();
app.MapDevicesEndpoints();
app.MapAnalyticsEndpoints();
app.MapCutoverEndpoints();

app.Run();

public partial class Program;

// ── Shared request/response DTOs (wire contracts, unchanged) ──
// Kept in Program.cs for minimal diff; in a full split they would live in
// each BC's Interfaces/REST/Resources/*. For now they remain global so that
// existing tests and frontend contracts stay green.

public sealed record CreateProjectRequest(string Name, string Description, string Location, int TotalUnits, int? BuilderId = null, string? ImageUrl = null);
public sealed record CreateProfileRequest(
    int UserId,
    string Name,
    string Username,
    string? PhoneNumber = null,
    string? Address = null,
    string? SecondEmail = null,
    int? Age = null,
    string? PhotoUrl = null);
public sealed record CreateSubscriptionRequest(int BuilderId, int PlanId, DateTimeOffset StartDate, DateTimeOffset? EndDate);
public sealed record ReplaceProfilePhotoRequest(string ExpectedReference, string Content);
public sealed record ProjectStructureRequest(int Floors, int UnitsPerFloor, List<int>? FloorNumbers);
public sealed record CreateDeviceRequest(string Name, string Type, string Location, string? MacAddress, int ProjectId, string Status, int? UnitId = null);
public sealed record DeviceCommandRequest(string Attribute, JsonElement Value);
public sealed record DeviceResponse(int Id, string Name, string Type, string Location, string? MacAddress, int ProjectId, string Status, int? UnitId = null)
{
    public static DeviceResponse From(Device device) => new(device.Id, device.Name, device.Type, device.Location, device.MacAddress, device.ProjectId, device.Status, device.UnitId);
}
