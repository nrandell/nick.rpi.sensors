using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sensors.Shared
{
    public abstract class BaseReadingService : BackgroundService
    {
        public IMqttFactory Factory { get; }
        public IMqttClientOptions Options { get; }
        public ILogger Logger { get; }
        public IHostApplicationLifetime Lifetime { get; }
        public string SensorName { get; }
        public string BaseTopic { get; } = "nick";

        protected BaseReadingService(IMqttFactory factory, IMqttClientOptions options, ILogger logger, IHostApplicationLifetime lifetime)
        {
            Factory = factory;
            Options = options;
            Logger = logger;
            Lifetime = lifetime;
            SensorName = options.ClientId;
        }

        private async Task<IMqttClient> Connect(CancellationToken stoppingToken)
        {
            var client = Factory.CreateMqttClient();
            var connected = await client.ConnectAsync(Options, stoppingToken);
            Logger.LogInformation("Connected as {Connected}", connected);
            return client;
        }

        protected async Task SendDiscovery(IMqttClient client, object discovery, string topic, CancellationToken stoppingToken)
        {
            var json = JsonSerializer.Serialize(discovery);
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(json).WithRetainFlag(true).Build();
            await client.PublishAsync(message, stoppingToken);
        }

        protected Task SendDiscovery(IMqttClient client, string deviceClass, string unitOfMeasurement, CancellationToken stoppingToken) =>
            SendDiscovery(client, deviceClass, deviceClass, unitOfMeasurement, stoppingToken);

        protected Task SendDiscovery(IMqttClient client, string deviceClass, string valueName, string unitOfMeasurement, CancellationToken stoppingToken) =>
            SendDiscovery(client, sensorId: SensorName,
                name: $"{SensorName}_{deviceClass}",
                stateTopic: $"{BaseTopic}/sensor/{SensorName}/state",
                deviceClass, valueName, unitOfMeasurement, stoppingToken);

        protected Task SendDiscovery(IMqttClient client, string sensorId, string name, string stateTopic, string deviceClass, string valueName, string unitOfMeasurement, CancellationToken stoppingToken) =>
            SendDiscovery(client,
                CreateDiscovery(name, stateTopic, deviceClass, valueName, unitOfMeasurement),
                CreateDiscoveryTopic(sensorId, deviceClass),
                stoppingToken);

        protected object CreateDiscovery(string name, string stateTopic, string deviceClass, string valueName, string unitOfMeasurement) =>
            new
            {
                name,
                device_class = deviceClass,
                state_topic = stateTopic,
                unit_of_measurement = unitOfMeasurement,
                value_template = $"{{{{ value_json.{valueName} }}}}"
            };

        protected string CreateDiscoveryTopic(string sensorId, string deviceClass) => $"homeassistant/sensor/{sensorId}/{deviceClass}/config";

        protected abstract Task SendDiscovery(IMqttClient client, CancellationToken stoppingToken);

        protected abstract Task RunContinuouslyAsync(IMqttClient client, TimeSpan delay, CancellationToken stoppingToken);

        protected Task SendState<T>(IMqttClient client, T state, CancellationToken stoppingToken) =>
            SendState<T>(client, $"{BaseTopic}/sensor/{SensorName}/state", state, stoppingToken);

        protected Task SendState<T>(IMqttClient client, string stateTopic, T state, CancellationToken stoppingToken)
        {
            var json = JsonSerializer.Serialize(state);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(stateTopic)
                .WithPayload(json)
                .WithRetainFlag(true)
                .Build();
            return client.PublishAsync(message, stoppingToken);
        }

        protected virtual Task ConfigureAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("Starting up");
            try
            {
                await ConfigureAsync(stoppingToken);
                var client = await Connect(stoppingToken);
                await SendDiscovery(client, stoppingToken);

                await RunContinuouslyAsync(client, TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (Exception ex)
            {
                if (!stoppingToken.IsCancellationRequested)
                {
                    Logger.LogError(ex, "Error running : {Excepton}", ex.Message);
                    Lifetime.StopApplication();
                }
            }
            finally
            {
                Logger.LogInformation("Finishing");
            }
        }
    }
}
