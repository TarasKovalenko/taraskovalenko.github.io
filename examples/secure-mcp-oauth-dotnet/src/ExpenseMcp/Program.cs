using System.Text;
using System.Threading.RateLimiting;
using ExpenseMcp.Audit;
using ExpenseMcp.Domain;
using ExpenseMcp.Security;
using ExpenseMcp.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

builder.Logging.AddJsonConsole();

builder.Services.AddOptions<McpSecurityOptions>()
    .Bind(builder.Configuration.GetSection(McpSecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.Resource, UriKind.Absolute, out _),
        "McpSecurity:Resource must be an absolute URI.")
    .Validate(options => options.RateLimit.PermitLimit > 0, "PermitLimit must be positive.")
    .Validate(options => options.RateLimit.WindowSeconds > 0, "WindowSeconds must be positive.")
    .ValidateOnStart();

var security = builder.Configuration
    .GetRequiredSection(McpSecurityOptions.SectionName)
    .Get<McpSecurityOptions>()
    ?? throw new InvalidOperationException("McpSecurity configuration is missing.");

var issuer = builder.Configuration["Authentication:Issuer"]
    ?? throw new InvalidOperationException("Authentication:Issuer is missing.");
var audience = builder.Configuration["Authentication:Audience"]
    ?? throw new InvalidOperationException("Authentication:Audience is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("ExpenseMcp.Security");
            SecurityLog.TokenValidationFailed(logger, context.Exception);
            return Task.CompletedTask;
        }
    };

    var symmetricKey = ResolveLocalSigningKey(builder);
    if (symmetricKey is not null)
    {
        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(symmetricKey));
    }
    else
    {
        options.Authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is missing.");
        options.RequireHttpsMetadata = true;
    }
})
.AddMcp(options =>
{
    options.ResourceMetadataUri = new Uri(security.ResourceMetadata, UriKind.RelativeOrAbsolute);
    options.ResourceMetadata = new()
    {
        Resource = security.Resource,
        ResourceName = "Expense MCP",
        AuthorizationServers = { security.AuthorizationServer },
        ScopesSupported = [security.InitialScope]
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ClientEntitlementService>();
builder.Services.AddSingleton<ExpenseStore>();
builder.Services.AddSingleton<IAuditSink, LoggerAuditSink>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter =
            security.RateLimit.WindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };

    options.AddPolicy("mcp", httpContext =>
    {
        var clientId = httpContext.User.FindFirst("client_id")?.Value ??
                       httpContext.User.FindFirst("azp")?.Value ??
                       "anonymous";
        var subject = httpContext.User.FindFirst("sub")?.Value ??
                      httpContext.Connection.RemoteIpAddress?.ToString() ??
                      "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{clientId}:{subject}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = security.RateLimit.PermitLimit,
                Window = TimeSpan.FromSeconds(security.RateLimit.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddMcpServer()
    .WithTools<ExpenseTools>()
    .WithHttpTransport(options => options.Stateless = true);

var app = builder.Build();

app.UseMiddleware<InitialScopeChallengeMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();
app.UseMiddleware<ToolAuthorizationMiddleware>();

app.MapMcp("/mcp")
    .RequireAuthorization()
    .RequireRateLimiting("mcp");

app.Run();

// A symmetric key is accepted only outside production hosting: the integration tests
// always need one, and local development uses one when no real IdP is available.
// Every other environment discovers signing keys from the authority metadata.
static string? ResolveLocalSigningKey(WebApplicationBuilder builder)
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        return builder.Configuration["Authentication:TestSigningKey"]
            ?? throw new InvalidOperationException("The test signing key is missing.");
    }

    if (builder.Environment.IsDevelopment())
    {
        var developmentKey = builder.Configuration["Authentication:LocalSigningKey"];
        return string.IsNullOrWhiteSpace(developmentKey) ? null : developmentKey;
    }

    return null;
}

public partial class Program;
