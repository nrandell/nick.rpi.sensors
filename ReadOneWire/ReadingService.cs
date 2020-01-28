using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using Nick.Rpi.Driver.Temperature;
using Sensors.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ReadOneWire
{
    public class ReadingService : BaseReadingService
    {
        private readonly ReadingServiceOptions _serviceOptions;
        private IReadOnlyDictionary<string, string>? _sensorNames;

        public ReadingService(IMqttFactory factory, IMqttClientOptions options, ILogger<ReadingService> logger, IHostApplicationLifetime lifetime, IOptions<ReadingServiceOptions> serviceOptions)
            : base(factory, options, logger, lifetime)
        {
            _serviceOptions = serviceOptions.Value;
        }

        protected override async Task ConfigureAsync(CancellationToken stoppingToken)
        {
            _sensorNames = await LoadSensorNames(stoppingToken);
        }

        private string StateTopic(string name) => $"{BaseTopic}/sensor/{SensorName}/{name}/state";
        private string SensorId(string id) => $"{SensorName}_{id}";
        private string Name(string name) => $"{SensorName}_{name}";

        protected async override Task SendDiscovery(IMqttClient client, CancellationToken stoppingToken)
        {
            var sensorNames = _sensorNames;
            if (sensorNames != null)
            {
                foreach (var (id, name) in sensorNames)
                {
                    await SendDiscovery(client,
                        sensorId: SensorId(id),
                        name: Name(name),
                        stateTopic: StateTopic(name),
                        deviceClass: "temperature",
                        valueName: "temperature",
                        unitOfMeasurement: "°C",
                        stoppingToken);
                }
            }
        }

        private async Task<IReadOnlyDictionary<string, string>> LoadSensorNames(CancellationToken ct)
        {
            var fileName = _serviceOptions.SensorNamesFile;
            if (!File.Exists(fileName))
            {
                Logger.LogError("Failed to find sensor file: {File}", fileName);
                throw new ArgumentException($"Cannot find sensor file: {fileName}");
            }

            await using var stream = File.OpenRead(fileName);
            var map = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct);
            return map;
        }

        private class SensorState
        {
            public string Id { get; }
            public string Name { get; }
            public double Value { get; set; }
            public DateTimeOffset NextReport { get; set; }
            public DateTimeOffset ReportTime { get; set; }

            public SensorState(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public bool Update(double value, DateTimeOffset timestamp)
            {
                if ((timestamp > NextReport) ||
                    (Math.Abs(value - Value) > 1))
                {
                    Value = value;
                    ReportTime = timestamp;
                    NextReport = timestamp.AddSeconds(60);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private Task SendReading(IMqttClient client, SensorState state, CancellationToken stoppingToken)
        {
            var data = new
            {
                temperature = state.Value
            };
            return SendState(client, StateTopic(state.Name), data, stoppingToken);
        }

        protected override async Task RunContinuouslyAsync(IMqttClient client, TimeSpan delay, CancellationToken stoppingToken)
        {
            var names = await LoadSensorNames(stoppingToken);
            var stateMap = new Dictionary<string, SensorState>();

            var oneWire = new OneWire();
            while (!stoppingToken.IsCancellationRequested)
            {
                var readings = await oneWire.Read(stoppingToken);
                foreach (var reading in readings)
                {
                    if (!stateMap.TryGetValue(reading.Device, out var state))
                    {
                        if (!names.TryGetValue(reading.Device, out var name))
                        {
                            Logger.LogWarning("Failed to find name for {Device}", reading.Device);
                            name = reading.Device;
                        }

                        state = new SensorState(reading.Device, name);
                        stateMap.Add(reading.Device, state);
                    }
                    if (state.Update(reading.DegreesCentigrade, reading.Timestamp))
                    {
                        await SendReading(client, state, stoppingToken);
                    }
                }
            }
        }
    }
}
