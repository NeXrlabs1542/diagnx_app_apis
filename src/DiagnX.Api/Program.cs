using System.Text.Json.Serialization;
using DiagnX.Api.Common;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Data.Seed;
using DiagnX.Api.Features.Bookings;
using DiagnX.Api.Features.Catalog;
using DiagnX.Api.Features.Kyc;
using DiagnX.Api.Features.LabOps;
using DiagnX.Api.Services;
using DiagnX.Api.Services.Auth;
using DiagnX.Api.Services.Payments;
using DiagnX.Api.Services.Security;
using DiagnX.Api.Services.Sms;
using DiagnX.Api.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Machine-local overrides (DB password etc.) — git-ignored, never committed.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

// Render (and most PaaS) tell the app which port to bind via $PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---------------------------------------------------------------- database
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(ConnectionStrings.Resolve(builder.Configuration), npgsql => npgsql.EnableRetryOnFailure(3))
    .UseSnakeCaseNamingConvention());

// ---------------------------------------------------------------- options + services
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.AddSingleton<SecretKeys>();
builder.Services.AddSingleton<FieldEncryptor>();
builder.Services.AddSingleton<SignedUrl>();
builder.Services.AddSingleton<ISmsSender, LogSmsSender>();
builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IFileStorage, DbFileStorage>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<AppConfigService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DeviceTokenService>();
builder.Services.AddScoped<LookupService>();
builder.Services.AddScoped<KycService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<BookingWorkflow>();
builder.Services.AddScoped<PatientBookingService>();
builder.Services.AddScoped<LabOpsService>();
builder.Services.AddScoped<DbSeeder>();

builder.Services.AddHttpClient(LookupService.IndiaPostClient, c =>
{
    c.BaseAddress = new Uri("https://api.postalpincode.in/");
    c.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddHttpClient(LookupService.IfscClient, c =>
{
    c.BaseAddress = new Uri("https://ifsc.razorpay.com/");
    c.Timeout = TimeSpan.FromSeconds(8);
});

// ---------------------------------------------------------------- auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SecretKeys, Microsoft.Extensions.Options.IOptions<JwtOptions>>((o, keys, jwt) =>
    {
        o.MapInboundClaims = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keys.JwtSigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // Envelope-shaped 401 / 403 so the apps can handle every error the same way.
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                var expired = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                await WriteError(ctx.HttpContext, 401, expired ? ErrorCodes.TokenExpired : ErrorCodes.Unauthorized,
                    expired ? "Your session has expired" : "Please log in to continue");
            },
            OnForbidden = ctx => WriteError(ctx.HttpContext, 403, ErrorCodes.Forbidden, "You are not allowed to do this"),
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------- MVC + JSON
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Malformed JSON / wrong types -> our envelope instead of ProblemDetails.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var fields = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => new FieldError(ToCamel(kv.Key.Replace("$.", "")), ErrorCodes.InvalidValue,
                    kv.Value!.Errors[0].ErrorMessage is { Length: > 0 } m && !m.Contains("JSON") ? m : "Invalid value"))
                .ToList();
            var body = new ApiErrorResponse(false,
                new ApiError { Code = ErrorCodes.BadRequest, Message = "The request is not valid", Fields = fields },
                ctx.HttpContext.TraceIdentifier);
            return new BadRequestObjectResult(body);
        };
    });

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("X-Request-Id", "Retry-After")));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); // Render's proxy addresses aren't fixed
    o.KnownProxies.Clear();
});

// ---------------------------------------------------------------- Swagger (one document per app)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("patient", new OpenApiInfo { Title = "DiagnX Patient API", Version = "v1", Description = "Patient app — login, browse, compare, book, pay, track, reports, reviews." });
    c.SwaggerDoc("partner", new OpenApiInfo { Title = "DiagnX Partner API", Version = "v1", Description = "Lab partner app — login, KYC (spec AUTH/PRT/KYC/DOC), and post-approval lab operations." });
    c.SwaggerDoc("master", new OpenApiInfo { Title = "DiagnX Master & Files", Version = "v1", Description = "Shared dropdowns, config, PIN / IFSC lookups, signed file downloads." });
    c.SwaggerDoc("admin", new OpenApiInfo { Title = "DiagnX Admin API", Version = "v1", Description = "Back-office — KYC review, catalog, labs, bookings, config." });
    c.DocInclusionPredicate((doc, api) => api.GroupName == doc);
    c.CustomSchemaIds(t => t.FullName!.Replace("DiagnX.Api.", "").Replace("+", "."));
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Paste the accessToken from the OTP verify / admin login response.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>(),
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "DiagnX.Api.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

// ---------------------------------------------------------------- pipeline
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "DiagnX API";
        c.SwaggerEndpoint("/swagger/patient/swagger.json", "Patient app");
        c.SwaggerEndpoint("/swagger/partner/swagger.json", "Partner app");
        c.SwaggerEndpoint("/swagger/master/swagger.json", "Master & files");
        c.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin");
        c.DisplayRequestDuration();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", async (AppDbContext db) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    return Results.Json(new { status = dbOk ? "ok" : "degraded", database = dbOk, time = DateTime.UtcNow }, statusCode: dbOk ? 200 : 503);
}).ExcludeFromDescription();

// ---------------------------------------------------------------- migrate + seed
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();

static Task WriteError(HttpContext ctx, int status, string code, string message)
{
    ctx.Response.StatusCode = status;
    return ctx.Response.WriteAsJsonAsync(new ApiErrorResponse(false, new ApiError { Code = code, Message = message }, ctx.TraceIdentifier));
}

static string ToCamel(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

public partial class Program;
