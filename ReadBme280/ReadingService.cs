using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Nick.Rpi.Driver.Bme280;
using Sensors.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Nick.Rpi.Driver.Bme280.Constants;

namespace ReadBme280
{
    public class ReadingService : BaseReadingService
    {
        public ReadingService(IMqttFactory factory, IMqttClientOptions options, ILogger<ReadingService> logger, IHostApplicationLifetime lifetime)
            : base(factory, options, logger, lifetime)
        {
        }

        protected override async Task SendDiscovery(IMqttClient client, CancellationToken stoppingToken)
        {
            await SendDiscovery(client, "humidity", "%", stoppingToken);
            await SendDiscovery(client, "temperature", "°C", stoppingToken);
            await SendDiscovery(client, "pressure", "hPa", stoppingToken);
        }

        private Task SendReading(IMqttClient client, bme280_data sensorData, CancellationToken stoppingToken)
        {
            var data = new
            {
                humidity = sensorData.HumidityPercent,
                pressure = sensorData.PressureHPa,
                temperature = sensorData.TemperatureCentigrade
            };
            return SendState(client, data, stoppingToken);
        }

        protected override async Task RunContinuouslyAsync(IMqttClient client, TimeSpan delay, CancellationToken stoppingToken)
        {
            var bme280 = new BME280("/dev/i2c-1", 0x76);
            var settings = new bme280_settings
            {
                osr_h = BME280_OVERSAMPLING_1X,
                osr_p = BME280_OVERSAMPLING_1X,
                osr_t = BME280_OVERSAMPLING_1X,
                filter = BME280_FILTER_COEFF_OFF
            };

            bme280.SetSensorSettings(ref settings, BME280_OSR_HUM_SEL | BME280_OSR_PRESS_SEL | BME280_OSR_TEMP_SEL);

            var oldData = new bme280_data();
            var nextReportTime = DateTimeOffset.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                bme280.SetSensorMode(BME280_FORCED_MODE);
                await Task.Delay(100, stoppingToken).ConfigureAwait(false);

                var data = bme280.GetSensorData(BME280_ALL);
                var now = DateTimeOffset.UtcNow;
                if ((now > nextReportTime) || ShouldReport(ref oldData, ref data))
                {
                    await SendReading(client, data, stoppingToken);
                    oldData = data;
                    nextReportTime = now.AddSeconds(60);
                }
                await Task.Delay(delay, stoppingToken);
            }
        }

        private bool ShouldReport(ref bme280_data oldData, ref bme280_data newData)
        {
            if (Math.Abs(oldData.TemperatureCentigrade - newData.TemperatureCentigrade) > 0.5)
            {
                return true;
            }
            if (Math.Abs(oldData.PressureHPa - newData.PressureHPa) > 1)
            {
                return true;
            }
            if (Math.Abs(oldData.HumidityPercent - newData.HumidityPercent) > 1)
            {
                return true;
            }
            return false;
        }
    }
}
