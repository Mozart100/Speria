using GiphyServer.Api.Clients;
using GiphyServer.Api.Configuration;
using GiphyServer.Api.Services;
using GiphyServer.Api.Startup;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------
// 1. Serilog — must be configured first so all subsequent logs go through it
// ------------------------------------------------------------------
builder.AddSerilogLogging();

// ------------------------------------------------------------------
// 2. Environment variable overrides
//    Map flat env vars (GIPHY_API_KEY, GIPHY_BASE_URL) into the
//    nested "Giphy:" config section consumed by IOptions<GiphyOptions>.
// ------------------------------------------------------------------
var giphyApiKeyFromEnv = Environment.GetEnvironmentVariable("GIPHY_API_KEY");
if (!string.IsNullOrWhiteSpace(giphyApiKeyFromEnv))
    builder.Configuration["Giphy:ApiKey"] = giphyApiKeyFromEnv;

var giphyBaseUrlFromEnv = Environment.GetEnvironmentVariable("GIPHY_BASE_URL");
if (!string.IsNullOrWhiteSpace(giphyBaseUrlFromEnv))
{
    // Normalise to always end with /v1/ so relative paths in GiphyHttpClient work correctly.
    var baseUrl = giphyBaseUrlFromEnv.TrimEnd('/');
    if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        baseUrl += "/v1";
    builder.Configuration["Giphy:BaseUrl"] = baseUrl + "/";
}

// ------------------------------------------------------------------
// 3. Configuration
// ------------------------------------------------------------------
builder.Services
    .Configure<GiphyOptions>(builder.Configuration.GetSection(GiphyOptions.SectionName));

// ------------------------------------------------------------------
// 4. AutoMapper
// ------------------------------------------------------------------
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

// ------------------------------------------------------------------
// 5. HTTP Clients (typed, via HttpClientFactory)
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
// 6. Application services
// ------------------------------------------------------------------
builder.Services.AddScoped<IGifService, GiphyGifService>();

// ------------------------------------------------------------------
// 7. MVC / Controllers
// ------------------------------------------------------------------
builder.Services.AddControllers();

// ------------------------------------------------------------------
// 8. CORS — permissive in Development; client runs on a different origin locally
// ------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// ------------------------------------------------------------------
// 9. API docs (Swagger / OpenAPI)
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
// 10. Problem details (global error responses)
// ------------------------------------------------------------------
builder.Services.AddProblemDetails();

var app = builder.Build();

// ------------------------------------------------------------------
// 11. Middleware pipeline
// ------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "GiphyServer API v1"));
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
                ? context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error.Message
                : null
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors();
app.UseRequestLogging();
app.MapControllers();

app.Run();
