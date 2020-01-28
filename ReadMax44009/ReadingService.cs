using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Nick.Rpi.Driver.Max44009;
using Sensors.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReadMax44009
{
    public class ReadingService : BaseReadingService
    {
        public ReadingService(IMqttFactory factory, IMqttClientOptions options, ILogger<ReadingService> logger, IHostApplicationLifetime lifetime)
            : base(factory, options, logger, lifetime)
        {
        }

        protected override Task SendDiscovery(IMqttClient client, CancellationToken stoppingToken)
        {
            return SendDiscovery(client, "illuminance", "lux", "lx", stoppingToken);
        }

        private Task SendReading(IMqttClient client, float lux, CancellationToken stoppingToken)
        {
            var data = new { lux };
            return SendState(client, data, stoppingToken);
        }

        protected override async Task RunContinuouslyAsync(IMqttClient client, TimeSpan delay, CancellationToken stoppingToken)
        {
            var max44009 = new Max44009("/dev/i2c-1", 0x4A);
            max44009.Reset();

            float oldLux = 0;
            var nextReportTime = DateTimeOffset.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                var lux = max44009.Read();
                var now = DateTimeOffset.UtcNow;
                if ((now > nextReportTime) || ShouldReport(oldLux, lux))
                {
                    await SendReading(client, lux, stoppingToken).ConfigureAwait(false);
                    oldLux = lux;
                    nextReportTime = now.AddSeconds(60);
                }
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }

        private bool ShouldReport(float oldLux, float lux)
        {
            if (lux > oldLux * 10)
            {
                return true;
            }
            if (lux < oldLux / 10)
            {
                return true;
            }
            return false;
        }
    }
}
