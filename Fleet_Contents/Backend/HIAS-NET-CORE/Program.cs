using System.Text;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Fleet.Hubs;
using HIAS_NET_CORE.Fleet.Migrations;
using HIAS_NET_CORE.Repositories;
using HIAS_NET_CORE.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// ─── Database ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<DatabaseContext>();

// ─── Fleet repositories (all take only DatabaseContext, so Scoped is fine) ──
builder.Services.AddScoped<FleetDbDevicesRepository>();
builder.Services.AddScoped<FleetDbSettingsRepository>();
builder.Services.AddScoped<FleetDbRealtimeRepository>();
builder.Services.AddScoped<FleetDbLocationsRepository>();
builder.Services.AddScoped<FleetDbStatusRepository>();
builder.Services.AddScoped<FleetDbAlarmLogRepository>();
builder.Services.AddScoped<FleetDbAlarmStateRepository>();
builder.Services.AddScoped<FleetDbTripsRepository>();
builder.Services.AddScoped<FleetDbDwellRepository>();
builder.Services.AddScoped<FleetDbEmailLogRepository>();
builder.Services.AddScoped<FleetDbApiConfigRepository>();

// ─── Demo data generator (replaces FleetIngestService — no real TZone creds here) ──
builder.Services.AddHostedService<FleetSimService>();

// ─── MVC + SignalR ───────────────────────────────────────────────────────────
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// ─── CORS — allow the Vite dev server plus any deployed frontend origin(s),
// with credentials for SignalR. Production origins come from the
// Cors:AllowedOrigins config (comma-separated), set via env var
// Cors__AllowedOrigins on the host (e.g. Render).
const string CorsPolicy = "FleetDemoFrontend";
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.SetIsOriginAllowed(origin =>
              Uri.TryCreate(origin, UriKind.Absolute, out var u) &&
              (u.Host == "localhost" || u.Host == "127.0.0.1" ||
               allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

// ─── JWT auth — demo-only, see Controllers/AuthDemoController.cs ───────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key missing from appsettings.json.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "fleet-demo",
            ValidateAudience = true,
            ValidAudience = "fleet-demo",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };

        // WebSocket connections (SignalR) can't send custom headers, so the
        // frontend passes the JWT as ?access_token=... on the hub URL instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/fleet-alarm-hub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// ─── Run Fleet schema migrations before accepting traffic ───────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    await FleetMigrationRunner.RunAsync(db);
}

// Lets FleetAlarmChecker/FleetDwellChecker push live alarms over SignalR
// even though FleetSimService (not FleetIngestService) is what's generating readings here.
FleetAlarmPusher.Initialize(app.Services.GetRequiredService<IHubContext<FleetAlarmHub>>());

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FleetAlarmHub>("/fleet-alarm-hub");

app.Run();
