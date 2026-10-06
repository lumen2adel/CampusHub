using CampusHub.Classes.UserAccount;
using CampusHub.Data;
using CampusHub.Hubs;
using CampusHub.JwtServices;
using CampusHub.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using campushub.Services;


var builder = WebApplication.CreateBuilder(args);

// Fail fast with a clear message when required secrets are missing (see appsettings.Example.json)
foreach (var key in new[] { "ConnectionStrings:DataContext", "jwt:Secret" })
{
    if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
        throw new InvalidOperationException(
            $"Missing configuration '{key}'. Set it with 'dotnet user-secrets set \"{key}\" <value>' or the environment variable '{key.Replace(":", "__")}'.");
}

// Register DbContext
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DataContext"));
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
// Without SMTP credentials in Development (e.g. docker compose), log e-mails instead of sending them
var useDevEmail = builder.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["EmailSettings:Password"]);
if (useDevEmail)
    builder.Services.AddTransient<IEmailService, LoggingEmailService>();
else
    builder.Services.AddTransient<IEmailService, EmailService>();

builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("loginPolicy", context =>
    {
        var ip = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});





// Background work: one service refreshes the feed views, one forwards DB notifications to SignalR
builder.Services.AddHostedService<FeedRankingService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<MentionService>();
builder.Services.AddHostedService<NotificationListenerService>();




builder.Services.AddHttpClient();
// Same for reCAPTCHA: Development without a secret key accepts any token; other environments always verify
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(builder.Configuration["Captcha:SecretKey"]))
    builder.Services.AddScoped<ICaptchaValidator, DevelopmentCaptchaValidator>();
else
    builder.Services.AddScoped<ICaptchaValidator, CaptchaValidator>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();






// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    opt.IncludeXmlComments(xmlPath);
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    // Microsoft.OpenApi v2: requirements reference the scheme through the document
    opt.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Add JWT configuration
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("jwt"));
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["jwt:Secret"])),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["jwt:Audience"],
    };
    // Browsers cannot send an Authorization header on a WebSocket upgrade, so SignalR clients
    // pass the JWT as ?access_token=...; accept it only for hub endpoints.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/chathub") || path.StartsWithSegments("/notificationHub")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Authorization policies (used with [Authorize(Policy = Policies.CanModerate)])
builder.Services.AddAuthorization(options =>
{
    // TODO(Laith): register the Policies.CanModerate policy here.
    // See tests/CampusHub.IntegrationTests/ModerationTests.cs for the behaviour it must have.
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy.AllowAnyOrigin()
        //.WithOrigins(
        //    "https://api.laith2.me",
        //    "http://api.laith2.me",
        //    "https://localhost:3000",
        //    "http://localhost:3000",
        //    "https://laith2.me",
        //    "http://laith2.me")
            .AllowAnyMethod()
            .AllowAnyHeader();
            //.AllowCredentials();
    });
});

// Add Controllers
builder.Services.AddControllers();

var app = builder.Build(); // Build the app after all services are registered

// Opt-in (docker compose sets it): apply pending EF migrations before the app starts serving.
// Fine for a single instance; with several replicas, run migrations as a separate deploy step instead.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DataContext>().Database.Migrate();
}


app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.InjectStylesheet("/swagger-custom.css");
    c.DefaultModelsExpandDepth(-1);
    c.EnableValidator();
    c.EnableFilter();
    c.DocumentTitle = "APIs";
    c.DocExpansion(DocExpansion.None);
    c.ShowExtensions();
    c.DisplayRequestDuration();
    c.DisplayOperationId();
    c.EnableDeepLinking();
    c.ShowCommonExtensions();
});
app.MapHub<NotificationHub>("/notificationHub");
app.UseRateLimiter();


// Middleware configuration
//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("dev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<CampusHub.Hubs.ChatHub>("/chathub");
app.Run();