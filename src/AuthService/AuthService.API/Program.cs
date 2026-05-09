using System.Text;
using AuthService.API.Health;
using AuthService.API.Middleware;
using AuthService.API.Persistence;
using FirebaseAdmin;
using AuthService.API.Persistence.Firestore;
using AuthService.API.Persistence.Firestore.Repositories;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Google.Apis.Auth.OAuth2;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
var builder = WebApplication.CreateBuilder(args);

// ── Serilog + Loki ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.GrafanaLoki(
        builder.Configuration["Logging:Loki:Uri"] ?? "http://loki:3100",
        labels: [new LokiLabel { Key = "service", Value = "auth-service" }])
    .CreateLogger();
builder.Host.UseSerilog();

// ── Firestore ───────────────────────────────────────────────────────────────
builder.Services.Configure<FirestoreOptions>(
    builder.Configuration.GetSection("Firestore"));
builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection("InternalApi"));
builder.Services.AddSingleton<IFirestoreDbProvider, FirestoreDbProvider>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddSingleton<ILoginEventRepository, LoginEventRepository>();
builder.Services.AddSingleton<IFailedLoginByIpRepository, FailedLoginByIpRepository>();
builder.Services.AddSingleton<IAdminStatsRepository, AdminStatsRepository>();
builder.Services.AddSingleton<IAdminStatsRefresher, AdminStatsRefresher>();
builder.Services.AddSingleton<IAuthRegistrationStore, AuthRegistrationStore>();

var firebaseCredentialsPath = builder.Configuration["Firestore:CredentialsPath"]
    ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

if (string.IsNullOrWhiteSpace(firebaseCredentialsPath) || !File.Exists(firebaseCredentialsPath))
{
    throw new InvalidOperationException(
        "Firebase credentials file is missing. Set Firestore:CredentialsPath or GOOGLE_APPLICATION_CREDENTIALS.");
}

var firebaseApp = FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(firebaseCredentialsPath)
});

builder.Services.AddSingleton(_ => FirebaseAdmin.Auth.FirebaseAuth.GetAuth(firebaseApp));

// ── JWT Authentication ───────────────────────────────────────────────────────
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
builder.Services.AddScoped<IAuthService, AuthService.API.Services.AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDefaultAdminSeeder, DefaultAdminSeeder>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<IWalletProvisioningService, WalletProvisioningService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:WalletServiceUrl"] ?? "http://wallet-service:8080");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ── CORS Configuration ──────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost", "http://localhost:80", "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Authorization");
    });
});

// Memory cache for caching proxied avatar images
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SSW Auth Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
});
builder.Services.AddHealthChecks()
    .AddCheck<FirestoreHealthCheck>("firestore");

var app = builder.Build();

// ── Bootstrap ───────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var defaultAdminSeeder = scope.ServiceProvider.GetRequiredService<IDefaultAdminSeeder>();
    await defaultAdminSeeder.SeedIfMissingAsync();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
