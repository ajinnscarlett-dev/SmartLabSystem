using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartLab.Server;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// DATABASE
// ==========================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));


// ==========================================
// CONTROLLERS
// ==========================================

builder.Services.AddControllers();


// ==========================================
// JWT AUTHENTICATION
// ==========================================

string jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing. Configure Jwt:Key in appsettings.json.");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 characters long.");
}

string jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? "SmartLab";

string jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? "SmartLab.Client";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ValidateIssuer = true,

                ValidIssuer =
                    jwtIssuer,

                ValidateAudience = true,

                ValidAudience =
                    jwtAudience,

                ValidateLifetime = true,

                ClockSkew =
                    TimeSpan.FromMinutes(1),

                NameClaimType =
                    ClaimTypes.Name,

                RoleClaimType =
                    ClaimTypes.Role
            };

        // ==========================================
        // TEMPORARY JWT DIAGNOSTIC LOGGING
        // ==========================================

        options.Events =
            new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    Console.WriteLine(
                        "[JWT] Token validated successfully.");

                    Console.WriteLine(
                        $"[JWT] User: {context.Principal?.Identity?.Name ?? "(unknown)"}");

                    Console.WriteLine(
                        $"[JWT] Role: {context.Principal?.FindFirst(ClaimTypes.Role)?.Value ?? "(none)"}");

                    return Task.CompletedTask;
                },

                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine(
                        "==========================================");

                    Console.WriteLine(
                        "[JWT] AUTHENTICATION FAILED");

                    Console.WriteLine(
                        $"[JWT] Exception: {context.Exception.Message}");

                    if (context.Exception.InnerException != null)
                    {
                        Console.WriteLine(
                            $"[JWT] Inner: {context.Exception.InnerException.Message}");
                    }

                    Console.WriteLine(
                        "==========================================");

                    return Task.CompletedTask;
                }
            };
    });


// ==========================================
// AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();


// ==========================================
// TOKEN SERVICE
// ==========================================

builder.Services.AddSingleton<AuthTokenService>();


// ==========================================
// AUTOMATIC PC HEARTBEAT MONITOR
// ==========================================

builder.Services.AddHostedService<PCMonitorService>();


// ==========================================
// LAN SERVER DISCOVERY
// ==========================================

builder.Services.AddHostedService<ServerDiscoveryService>();


// ==========================================
// SWAGGER
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "SmartLab Server API",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Paste the JWT returned by /api/Auth/login."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();


// ==========================================
// SWAGGER
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// ==========================================
// HTTPS
// DEVELOPMENT: DO NOT FORCE HTTP → HTTPS
// PRODUCTION: FORCE HTTPS
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


// ==========================================
// AUTHENTICATION
// ==========================================

app.UseAuthentication();


// ==========================================
// AUTHORIZATION
// ==========================================

app.UseAuthorization();


// ==========================================
// CONTROLLERS
// ==========================================

app.MapControllers();


// ==========================================
// START SERVER
// ==========================================

app.Run();