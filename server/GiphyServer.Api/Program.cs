using GiphyServer.Api.Clients;
using GiphyServer.Api.Configuration;
using GiphyServer.Api.Startup;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------
// 1. Serilog — configured first so every subsequent log uses it
// ------------------------------------------------------------------
builder.AddSerilogLogging();

// ------------------------------------------------------------------
// 2. Environment variable overrides
//    Flat env vars → nested config sections consumed by IOptions<T>.
// ------------------------------------------------------------------
var giphyApiKey = Environment.GetEnvironmentVariable("GIPHY_API_KEY");
if (!string.IsNullOrWhiteSpace(giphyApiKey))
    builder.Configuration["Giphy:ApiKey"] = giphyApiKey;

var giphyBaseUrl = Environment.GetEnvironmentVariable("GIPHY_BASE_URL");
if (!string.IsNullOrWhiteSpace(giphyBaseUrl))
{
    var baseUrl = giphyBaseUrl.TrimEnd('/');
    if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        baseUrl += "/v1";
    builder.Configuration["Giphy:BaseUrl"] = baseUrl + "/";
}

var cacheTtl = Environment.GetEnvironmentVariable("CACHE_TTL_MINUTES");
if (!string.IsNullOrWhiteSpace(cacheTtl) && int.TryParse(cacheTtl, out var ttlMinutes))
    builder.Configuration["Cache:TtlMinutes"] = ttlMinutes.ToString();

// ------------------------------------------------------------------
// 3. Strongly-typed configuration
// ------------------------------------------------------------------
builder.Services.Configure<GiphyOptions>(
    builder.Configuration.GetSection(GiphyOptions.SectionName));

builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

// ------------------------------------------------------------------
// 4. AutoMapper
// ------------------------------------------------------------------
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

// ------------------------------------------------------------------
// 5. Typed HttpClient — IGiphyClient → GiphyHttpClient
// ------------------------------------------------------------------
builder.Services
    .AddHttpClient<IGiphyClient, GiphyHttpClient>(client =>
    {
        var baseUrl = builder.Configuration[$"{GiphyOptions.SectionName}:BaseUrl"]
                      ?? "https://api.giphy.com/v1/";
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

// ------------------------------------------------------------------
// 6. Redis + ICacheService
// ------------------------------------------------------------------
builder.AddRedis();

// ------------------------------------------------------------------
// 7. Application services — decorator chain:
//    IGifService → CachingGiphyServiceDecorator → GiphyGifService
// ------------------------------------------------------------------
builder.Services.AddGifServices();

// ------------------------------------------------------------------
// 8. MVC / Controllers
// ------------------------------------------------------------------
builder.Services.AddControllers();

// ------------------------------------------------------------------
// 9. CORS — permissive for local development
// ------------------------------------------------------------------
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ------------------------------------------------------------------
// 10. API docs
// ------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "GiphyServer API",
        Version = "v1",
        Description = "Returns GIF URLs from the Giphy platform."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ------------------------------------------------------------------
// 11. Health checks
//     Base liveness check is built-in. Add provider-specific checks here
//     as the project grows, e.g.:
//       .AddRedis(connectionString)
//       .AddUrlGroup(new Uri("https://api.giphy.com"), "giphy")
// ------------------------------------------------------------------
builder.Services.AddHealthChecks();

// ------------------------------------------------------------------
// 12. Problem details
// ------------------------------------------------------------------
builder.Services.AddProblemDetails();

var app = builder.Build();

// ------------------------------------------------------------------
// 12. Middleware pipeline
// ------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui =>
        ui.SwaggerEndpoint("/swagger/v1/swagger.json", "GiphyServer API v1"));
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = app.Environment.IsDevelopment()
                ? context.Features
                    .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()
                    ?.Error.Message
                : null
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors();
app.UseRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
