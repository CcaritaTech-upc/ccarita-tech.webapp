using System.Buffers;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IoBuild.Api.Devices.Infrastructure.Mqtt;

public sealed class MqttDeviceTransport(Microsoft.Extensions.Configuration.IConfiguration configuration, IServiceScopeFactory scopes) : IDeviceMqttPublisher, IHostedService, IAsyncDisposable
{
    private readonly MQTTnet.IMqttClient client = new MQTTnet.MqttClientFactory().CreateMqttClient();
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private bool messageHandlerConfigured;
    private bool reconnectHandlerConfigured;
    private volatile bool stopping;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Mqtt:Enabled")) return;
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (!messageHandlerConfigured)
            {
                client.ApplicationMessageReceivedAsync += async arguments =>
                {
                    var topic = arguments.ApplicationMessage.Topic ?? string.Empty;
                    if (!topic.StartsWith("telemetry/", StringComparison.Ordinal) || !int.TryParse(topic["telemetry/".Length..], out var id)) return;
                    try { var seq = arguments.ApplicationMessage.Payload; var payloadBytes = new byte[seq.Length]; seq.CopyTo(payloadBytes); var payloadString = System.Text.Encoding.UTF8.GetString(payloadBytes); var json = System.Text.Json.JsonDocument.Parse(payloadString); var root = json.RootElement; var eventId = root.TryGetProperty("eventId", out var eventValue) ? eventValue.GetString() ?? $"{id}:{root.GetProperty("timestamp").GetString()}" : $"{id}:{root.GetProperty("timestamp").GetString()}"; var timestamp = root.TryGetProperty("timestamp", out var ts) ? DateTimeOffset.Parse(ts.GetString()!) : DateTimeOffset.UtcNow; var status = root.TryGetProperty("status", out var state) ? state.GetString() ?? "unknown" : "unknown"; var reported = root.TryGetProperty("reported", out var reportedValue) ? reportedValue.GetRawText() : "{}"; var energy = root.TryGetProperty("energy_kwh", out var energyValue) ? energyValue.GetDouble() : 0; var temperature = root.TryGetProperty("temperature_c", out var temperatureValue) ? temperatureValue.GetDouble() : 0; var voltage = root.TryGetProperty("voltage_v", out var voltageValue) ? voltageValue.GetDouble() : 0; using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<DeviceTelemetryService>().IngestAsync(new TelemetryMessage(id, eventId, timestamp, status, reported, energy, temperature, voltage)); } catch (Exception) { }
                };
                messageHandlerConfigured = true;
            }
            if (!reconnectHandlerConfigured)
            {
                client.DisconnectedAsync += async _ =>
                {
                    if (stopping || !configuration.GetValue<bool>("Mqtt:Enabled")) return;
                    for (var attempt = 0; attempt < 30 && !stopping && !client.IsConnected; attempt++)
                    {
                        try { await StartAsync(CancellationToken.None); }
                        catch (HttpRequestException) { await Task.Delay(TimeSpan.FromSeconds(1)); }
                    }
                };
                reconnectHandlerConfigured = true;
            }
            if (client.IsConnected) return;
            var host = configuration["Mqtt:Host"] ?? "localhost"; var port = configuration.GetValue("Mqtt:Port", 1883);
            Exception? failure = null;
            for (var attempt = 0; attempt < 3 && !client.IsConnected; attempt++)
            {
                try { await client.ConnectAsync(ConnectionOptions(host, port), cancellationToken); }
                catch (Exception exception) when (attempt < 2) { failure = exception; await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken); }
            }
            if (!client.IsConnected)
            {
                _ = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < 60 && !stopping && !client.IsConnected; attempt++)
                    {
                        try { await Task.Delay(TimeSpan.FromSeconds(2)); await StartAsync(CancellationToken.None); }
                        catch { }
                    }
                });
                return;
            }
            await client.SubscribeAsync(new MQTTnet.MqttClientSubscribeOptionsBuilder().WithTopicFilter(filter => filter.WithTopic("telemetry/#").WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)).Build(), cancellationToken);
            using (var scope = scopes.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DeviceRegistryService>().AnnounceAllAsync(cancellationToken);
                await scope.ServiceProvider.GetRequiredService<DeviceCommandService>().RepublishPendingAsync(cancellationToken);
            }
        }
        finally { connectionGate.Release(); }
    }
    private MQTTnet.MqttClientOptions ConnectionOptions(string host, int port)
    {
        var options = new MQTTnet.MqttClientOptionsBuilder().WithTcpServer(host, port).WithClientId("iobuild-devices").WithCleanSession();
        var username = configuration["Mqtt:Username"];
        if (!string.IsNullOrEmpty(username)) options.WithCredentials(username, configuration["Mqtt:Password"]);
        return options.Build();
    }
    public async Task StopAsync(CancellationToken cancellationToken) { stopping = true; if (client.IsConnected) await client.DisconnectAsync(new MQTTnet.MqttClientDisconnectOptions(), cancellationToken); }
    public async Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Mqtt:Enabled")) return;
        if (!client.IsConnected) await StartAsync(cancellationToken);
        if (!client.IsConnected) throw new HttpRequestException("MQTT is not configured or unavailable.");
        var message = new MQTTnet.MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).WithQualityOfServiceLevel(qos1 ? MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce : MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce).WithRetainFlag(retain).Build();
        await client.PublishAsync(message, cancellationToken);
    }
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
}
