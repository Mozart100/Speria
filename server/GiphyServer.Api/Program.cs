using GiphyServer.Api.Clients;
using GiphyServer.Api.Configuration;
using GiphyServer.Api.HealthChecks;
using GiphyServer.Api.Startup;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.Mvc;
using Polly;
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

var resultLimit = Environment.GetEnvironmentVariable("GIPHY_RESULT_LIMIT");
if (!string.IsNullOrWhiteSpace(resultLimit) && int.TryParse(resultLimit, out var limit))
    builder.Configuration["Giphy:ResultLimit"] = Math.Clamp(limit, 1, 50).ToString();

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
// 5. Typed HttpClient with Polly resilience pipeline
//    Retry:          exponential back-off (1s → 2s → 4s), transient 5xx only
//    Circuit breaker: open after 5 failures, reset after 30 seconds
// ------------------------------------------------------------------
builder.Services
    .AddHttpClient<IGiphyClient, GiphyHttpClient>(client =>
    {
        var baseUrl = builder.Configuration[$"{GiphyOptions.SectionName}:BaseUrl"]
                      ?? "https://api.giphy.com/v1/";
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    })
    .AddResilienceHandler("giphy", pipeline =>
    {
        // Retry on connection errors and 5xx only — NOT on 401/403/429.
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay             = TimeSpan.FromSeconds(1),
            BackoffType       = DelayBackoffType.Exponential,
            UseJitter         = false,
            ShouldHandle      = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode >= 500),
        });

        // Circuit breaker: stop hammering Giphy when it is persistently down.
        // Polly v8 uses ratio-based circuit breaking: open when ≥5 requests in 60s all fail.
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            MinimumThroughput = 5,
            FailureRatio      = 1.0,
            SamplingDuration  = TimeSpan.FromSeconds(60),
            BreakDuration     = TimeSpan.FromSeconds(30),
            ShouldHandle      = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode >= 500),
        });
    });

// ------------------------------------------------------------------
// 6. Redis connection string — resolved once and shared with health checks
// ------------------------------------------------------------------
var redisConnString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                      ?? builder.Configuration["Redis:ConnectionString"]
                      ?? "localhost:6379";

builder.AddRedis(redisConnString);

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
// 11. Health checks — Redis connectivity + Giphy API reachability
// ------------------------------------------------------------------
builder.Services
    .AddHealthChecks()
    .AddRedis(redisConnString, name: "redis")
    .AddCheck<GiphyHealthCheck>("giphy");

// ------------------------------------------------------------------
// 12. Global exception handler + ProblemDetails
//     GlobalExceptionHandler maps domain exceptions to RFC 7807 responses.
// ------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ------------------------------------------------------------------
// 13. Middleware pipeline
// ------------------------------------------------------------------
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui =>
        ui.SwaggerEndpoint("/swagger/v1/swagger.json", "GiphyServer API v1"));
}

app.UseCors();
app.UseRequestLogging();
app.MapControllers();

app.Run();
