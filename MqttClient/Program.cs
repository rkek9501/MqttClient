using System;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Diagnostics;
//using MQTTnet.Exceptions;
//using MQTTnet.Adapter;

public enum Connections
{
    Init,
    Connected,
    Disconnected
}

public class MQFactory
{
    static MqttFactory factory;
    static IMqttClient client;
    static Connections connections { get; set; } = Connections.Init;
    public static async Task Main(string[] args)
    {
        SetFectory();

        if (factory != null && client != null)
        {
            await ConnectMqtt();
            await SetSubscribe();
        }

        Console.WriteLine(args);
        var line = "start";
        while (line != "")
        {
            line = Console.ReadLine();
            if (line == "")
            {
                await DisconnectClient();
            }
            if (line.IndexOf("pub") == 0)
            {
                string top = line.Split()[1];
                string msg = line.Split()[2];
                Console.WriteLine($"[{top}]: {msg}");
                await Publishing(top, msg);
            }
            if (line.IndexOf("discon") == 0)
            {
                await DisconnectClient();
            }
        }
    }

    private static void SetFectory()
    {
        try
        {
            factory = new MqttFactory(new MqttLogger());
            client = factory.CreateMqttClient();
        }
        catch (Exception e)
        {
            Console.WriteLine("Not Connected");
            Console.WriteLine(e.Message);
        }
        finally
        {
            CheckConnection();
        }
    }

    private static async Task ConnectMqtt()
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883)
            //.WithTls()
            //.WithCredentials("username", "password")
            .Build();

        await client.ConnectAsync(options, CancellationToken.None);
        connections = Connections.Connected;
        Console.WriteLine("connect!");
    }
    private static async Task ReconnectMqtt()
    {
        await client.ReconnectAsync(CancellationToken.None);
        connections = Connections.Connected;
        Console.WriteLine("reconnect!");
        await SetSubscribe();
    }

    private static async Task Publishing(string topic, string msg)
    {
        var appMsg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(msg)
            .Build();

        await client.PublishAsync(appMsg, CancellationToken.None);
    }

    private static async Task DisconnectClient()
    {
        connections = Connections.Disconnected;
        await client.DisconnectAsync();
        Console.WriteLine("Disconnected!");
        Thread.Sleep(2000);
    }

    static Task ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        Console.WriteLine($"{args.ReasonCode}");
        Console.WriteLine($"ClientId: {args.ClientId}");
        Console.WriteLine($"QoS: {args.ApplicationMessage.QualityOfServiceLevel}");
        Console.WriteLine($"Topic: {args.ApplicationMessage.Topic}");
        Console.WriteLine($"Payload: {args.ApplicationMessage.ConvertPayloadToString()}");

        return Task.CompletedTask;
    }
    static Task DisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        Console.WriteLine($"[Disconnected] {args.ClientWasConnected}");
        connections = Connections.Disconnected;

        if (!args.ClientWasConnected && connections == Connections.Connected)
        {
            //connections = Connections.Disconnected;
        }
        if (!args.ClientWasConnected)
        {
            Console.WriteLine($"[Disconnected] Reason {args.Reason}");
        }
        return Task.CompletedTask;
    }
    private static async Task SetSubscribe()
    {
        client.ApplicationMessageReceivedAsync += ApplicationMessageReceivedAsync;

        client.DisconnectedAsync += DisconnectedAsync;

        var subOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter("test/#", MqttQualityOfServiceLevel.AtLeastOnce)
            .WithTopicFilter("new/case", MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await client.SubscribeAsync(subOptions, CancellationToken.None);
        Console.WriteLine("Set Subscribe!");
    }

    static void CheckConnection()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                //Console.WriteLine("Ping");
                try
                {
                    if (connections == Connections.Disconnected)
                    {
                        //var ping = await client.TryPingAsync(CancellationToken.None);
                        bool ping = client.IsConnected;
                        Console.WriteLine($"ping: {ping}");
                        Console.WriteLine($"isConnected: {connections}");
                        Console.WriteLine("reconnect!");
                        await ReconnectMqtt();
                        Console.WriteLine("reconnected!");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
        });
    }
    sealed class MqttLogger : IMqttNetLogger
    {
        public bool IsEnabled { get; set; } = true;

        public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] parameters, Exception exception)
        {
            //Console.WriteLine($"[{logLevel}|{source}] {message}");
            ////Console.WriteLine($"[{logLevel}|{source}] params:{parameters?.ToString()}");
            //if (exception != null)
            //{
            //    Console.WriteLine(exception.Message);
            //} 
        }
    }
}
