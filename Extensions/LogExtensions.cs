using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Data;

namespace API.Extensions
{
    public static class LogExtensions
    {

        public static void AddCustomLogConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(loggingBulder =>
            {
                var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.WithClientIp()
                .Enrich.WithCorrelationId()
                .WriteTo.MSSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    new MSSqlServerSinkOptions() { TableName = "Logs", SchemaName = "dbo", AutoCreateSqlTable = true },
                    columnOptions: new ColumnOptions
                    {
                        AdditionalColumns = new Collection<SqlColumn>
                        {
                            new SqlColumn{ColumnName="ClientIP",PropertyName="ClientIp",DataType= SqlDbType.NVarChar},
                        },
                    });
                var logger = loggerConfiguration.CreateLogger();
                loggingBulder.Services.AddSingleton<ILoggerFactory>(
                    provider => new SerilogLoggerFactory(logger, dispose: false));

            });

        }

        public static void HttpRequestPipeline(this IApplicationBuilder app)
        {
            app.UseSerilogRequestLogging(
                options =>
                {
                    options.MessageTemplate = "{RequestScheme} {RequestHost} {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                    options.EnrichDiagnosticContext = (
                        diagnosticContext,
                        httpContext) =>
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress);
                    };
                });
        }
    }
}
