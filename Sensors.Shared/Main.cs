using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Client.Options;
using Serilog;
using Serilog.Events;
using System;

namespace Sensors.Shared
{
    public class Main<TService> where TService : BaseReadingService
    {
        public int Run(string[] args, Action<IServiceCollection>? configureServices = null)
        {
            Log.Logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .CreateLogger();

            try
            {
                Log.Information("Starting host");
                BuildHost(args, configureServices).Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private IHost BuildHost(string[] args, Action<IServiceCollection>? configureServices = null) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    configureServices?.Invoke(services);

                    services.AddHostedService<TService>();
                    services.AddSingleton<IMqttClientOptions>(sp =>
                    {
                        var configuration = sp.GetRequiredService<IConfiguration>();
                        var sensorName = configuration.GetValue<string>("SensorName");
                        if (string.IsNullOrWhiteSpace(sensorName))
                        {
                            Log.Fatal("No 'SensorName' specified");
                            throw new InvalidOperationException("No 'SensorName' specified");
                        }

                        var serverName = configuration.GetValue<string>("ServerName");
                        if (string.IsNullOrWhiteSpace(serverName))
                        {
                            Log.Fatal("No 'ServerName' specified");
                            throw new InvalidOperationException("No 'ServerName' specified");
                        }

                        return new MqttClientOptionsBuilder()
                            .WithClientId(sensorName)
                            .WithTcpServer(serverName)
                            .WithCleanSession()
                            .Build();
                    });

                    services.AddSingleton<IMqttFactory, MqttFactory>();
                })
                .UseSerilog()
                .Build();
    }
}

