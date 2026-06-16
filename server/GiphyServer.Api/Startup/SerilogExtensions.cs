using Serilog;
using Serilog.Events;

namespace GiphyServer.Api.Startup;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL")
                     ?? builder.Configuration["Seq:ServerUrl"]
                     ?? "http://localhost:5341";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Seq(seqUrl)
            .CreateLogger();

        builder.Host.UseSerilog();
        return builder;
    }
}
