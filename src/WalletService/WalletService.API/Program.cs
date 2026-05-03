using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using WalletService.API.Health;
using WalletService.API.Middleware;
using WalletService.API.Persistence;
using WalletService.API.Persistence.Firestore;
using WalletService.API.Persistence.Firestore.Repositories;
using WalletService.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog + Loki ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        builder.Configuration["Logging:Loki:Uri"] ?? "http://loki:3100",
        labels: [new LokiLabel { Key = "service", Value = "wallet-service" }])
    .CreateLogger();
builder.Host.UseSerilog();

// ── Firestore ───────────────────────────────────────────────────────────────
builder.Services.Configure<FirestoreOptions>(
    builder.Configuration.GetSection("Firestore"));
builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection("InternalApi"));
builder.Services.AddSingleton<IFirestoreDbProvider, FirestoreDbProvider>();
builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<ILedgerRepository, LedgerRepository>();

// ── JWT Authentication (validates tokens issued by Auth Service) ────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// ── Application Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IWalletService, WalletService.API.Services.WalletService>();
builder.Services.AddScoped<IWalletManagementService, WalletManagementService>();
builder.Services.AddScoped<IUserLookupService, UserLookupService>();
builder.Services.AddHttpClient<IUserLookupService, UserLookupService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:AuthServiceUrl"] ?? "http://auth-service:8080");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SSW Wallet Service", Version = "v1" });
});
builder.Services.AddHealthChecks()
    .AddCheck<FirestoreHealthCheck>("firestore");

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<RateLimitMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
