using System.Text;
using System.Text.Json.Serialization;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Hubs;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<LaresDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<LaresDbContext>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<HomeAccessService>();
builder.Services.AddScoped<IDeviceConnector, SimulatedConnector>();
builder.Services.AddSignalR();
builder.Services.AddScoped<DeviceHubNotifier>();

if (string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]))
{
    throw new InvalidOperationException(
        "Gemini:ApiKey is not configured. In development: " +
        "dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\" --project backend/Lares.Api");
}

builder.Services.AddHttpClient<IAiClient, GeminiClient>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddHostedService<AutomationSchedulerService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // WebSocket upgrades can't carry a custom Authorization header, so SignalR
        // sends the JWT via ?access_token= instead — pick it up here for /hubs/* requests.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

// Exposes the implicit Program class to WebApplicationFactory-based integration tests.
public partial class Program;
