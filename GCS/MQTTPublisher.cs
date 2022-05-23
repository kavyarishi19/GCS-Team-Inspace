using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCS
{
    internal class MQTTPublisher
    {
        async Task PublishMessageAsync(MqttClient client, string messagePayload, string topic = "inspace/1014")
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(messagePayload)
                .WithAtLeastOnceQoS()
                .Build();

            if (client.IsConnected)
            {
                await client.PublishAsync(message);
            }
        }

        async Task GCS_PublishMessage(string messageToPublish, string tcpServer, int portNumber)
        {
            var mqttFactory = new MqttFactory();
            MqttClient client = (MqttClient)mqttFactory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(tcpServer, portNumber)
                .WithCleanSession()
                .Build();

            client.UseConnectedHandler(e =>
            {
                Trace.WriteLine("Connection with Broker successful!");
            });

            client.UseDisconnectedHandler(e =>
            {
                Console.WriteLine("Disconnected! ✅");
            });

            await client.ConnectAsync(options);

            Trace.WriteLine($"Message To Publish: {messageToPublish}");
            await PublishMessageAsync(client, messageToPublish);

            await client.DisconnectAsync();
        }
    }
}
