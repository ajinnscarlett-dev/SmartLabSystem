using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartLab.Server;
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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ValidateIssuer = true,
                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"]
                    ?? "SmartLab",

                ValidateAudience = true,
                ValidAudience =
                    builder.Configuration["Jwt:Audience"]
                    ?? "SmartLab.Client",

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromMinutes(1)
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
// SWAGGER
// ==========================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
// ==========================================

app.UseHttpsRedirection();


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
