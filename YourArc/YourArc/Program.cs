using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YourArc.Data;
using YourArc.Database;
using YourArc.Services;

var builder = WebApplication.CreateBuilder(args);

// Resolve connection string from DATABASE_URL env var (standard on Render) or appsettings.json
var connectionString = ResolveConnectionString(builder.Configuration);

// Database configuration with connection pooling and retry resilience
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    })
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register PasswordHasher and TokenService
builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is missing."));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// Run database migrations and pre-warm EF Core on startup to eliminate first-request cold-start penalty
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        app.Logger.LogInformation("Applying database migrations and warming up database connection...");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Database is up to date and connection warmed.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database warmup/migration failed on startup. Will attempt connection on incoming requests.");
    }
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Root & Health check endpoints (used by Render health checks and uptime pingers)
app.MapGet("/", () => Results.Ok(new
{
    status = "online",
    service = "YourArc API",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "healthy", database = "connected" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();

app.Run();

static string ResolveConnectionString(IConfiguration configuration)
{
    var envDbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(envDbUrl))
    {
        if (envDbUrl.StartsWith("postgres://") || envDbUrl.StartsWith("postgresql://"))
        {
            var uri = new Uri(envDbUrl);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? userInfo[0] : "";
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Keepalive=30;";
        }
        return envDbUrl;
    }

    return configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}