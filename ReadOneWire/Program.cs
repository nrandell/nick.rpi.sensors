using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sensors.Shared;

namespace ReadOneWire
{
    public static class Program
    {
        public static int Main(string[] args) =>
            new Main<ReadingService>()
            .Run(args, services =>
            {
                var provider = services.BuildServiceProvider();
                var configuration = provider.GetRequiredService<IConfiguration>();

                services.Configure<ReadingServiceOptions>(options => options.SensorNamesFile = configuration.GetValue<string>("SensorNames"));
            });
    }
}
