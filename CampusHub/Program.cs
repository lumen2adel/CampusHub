using CampusHub.Classes.UserAccount;
using CampusHub.Data;
using CampusHub.Helper;
using CampusHub.Hubs;
using CampusHub.JwtServices;
using CampusHub.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext
builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DataContext"));
});

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(BaseMappingProfile));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailService>();

builder.Services.AddSignalR();






builder.Services.AddHostedService<TrendingPostsService>();
builder.Services.AddSingleton<TrendingPostsService>();


builder.Services.AddHostedService<PostgreSqlNotificationService>();
//builder.Services.AddHostedService<TrendingPostsService>();
builder.Services.AddHostedService<NotificationService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddHostedService<FeedRankingService>();
builder.Services.AddScoped<MentionService>();
builder.Services.AddHostedService<NotificationListenerService>();











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
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
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
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Controllers
builder.Services.AddControllers();

var app = builder.Build(); // Build the app after all services are registered


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