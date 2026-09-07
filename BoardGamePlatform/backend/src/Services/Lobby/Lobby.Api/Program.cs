using System.Reflection;
using BuildingBlocks.Infrastructure.Logging;
using BuildingBlocks.Infrastructure.Middleware;
using BuildingBlocks.Infrastructure.Persistence;
using Lobby.Application.Persistence;
using Lobby.Infrastructure.Extensions;
using Lobby.Infrastructure.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.AddSerilogLogging("Lobby");

// Register infrastructure: DbContext, MediatR, FluentValidation, AutoMapper, JWT auth.
builder.Services.AddLobbyInfrastructure(builder.Configuration);

// Allow the frontend dev server to call this API cross-origin.
var allowedOrigin = builder.Configuration.GetValue<string>("Cors:AllowedOrigin") ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Lobby API",
        Version = "v1",
        Description = "Rooms, waiting room lifecycle and real-time lobby updates."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseRouting();
app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LobbyHub>("/hubs/lobby");

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    service = "Lobby",
    status = "running",
    version = "1.0.0"
}));

app.Services.MigrateDatabase<LobbyDbContext>();

app.Run();