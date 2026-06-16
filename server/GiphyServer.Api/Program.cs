using GiphyServer.Api.Clients;
using GiphyServer.Api.Configuration;
using GiphyServer.Api.Services;
using GiphyServer.Api.Startup;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------
// 1. Environment variable override
//    Map GIPHY_API_KEY → Giphy:ApiKey so the standard IOptions<GiphyOptions>
//    binding picks it up transparently, regardless of how it was named in
//    the environment.
// ------------------------------------------------------------------
var giphyApiKeyFromEnv = Environment.GetEnvironmentVariable("GIPHY_API_KEY");
if (!string.IsNullOrWhiteSpace(giphyApiKeyFromEnv))
    builder.Configuration["Giphy:ApiKey"] = giphyApiKeyFromEnv;

// ------------------------------------------------------------------
// 2. Configuration
// ------------------------------------------------------------------
builder.Services
    .Configure<GiphyOptions>(builder.Configuration.GetSection(GiphyOptions.SectionName));

// ------------------------------------------------------------------
// 3. AutoMapper
// ------------------------------------------------------------------
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

// ------------------------------------------------------------------
// 4. HTTP Clients (typed, via HttpClientFactory)
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
// 5. Application services
// ------------------------------------------------------------------
builder.Services.AddScoped<IGifService, GiphyGifService>();

// ------------------------------------------------------------------
// 6. MVC / Controllers
// ------------------------------------------------------------------
builder.Services.AddControllers();

// ------------------------------------------------------------------
// 7. API docs (Swagger / OpenAPI)
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

    // Include XML comments for Swagger UI
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ------------------------------------------------------------------
// 8. Problem details (global error responses)
// ------------------------------------------------------------------
builder.Services.AddProblemDetails();

var app = builder.Build();

// ------------------------------------------------------------------
// 9. Middleware pipeline
// ------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(ui => ui.SwaggerEndpoint("/swagger/v1/swagger.json", "GiphyServer API v1"));
}

// Global exception handler — returns RFC 7807 Problem Details on unhandled exceptions.
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

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
