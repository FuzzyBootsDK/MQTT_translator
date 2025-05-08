using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using MQTTnet;
using MQTT_translator;
using DotNetEnv;


class Program
{
    static async Task Main(string[] args)
    {
        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", 1883)
            .Build();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            Console.WriteLine($"Topic: {e?.ApplicationMessage?.Topic ?? "[Ingen topic]"}");
            if (e.ApplicationMessage?.Payload != null)
            {
                try
                {
                    var payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    Console.WriteLine($"Payload: {payloadString}");

                    // Attempt to deserialize into your model
                    var sensorData = JsonSerializer.Deserialize<SensorData_Model>(payloadString);

                    if (sensorData != null)
                    {
                        // Round temperature and humidity to two decimal points
                        sensorData.Temperature = Math.Round(sensorData.Temperature, 2);
                        sensorData.Humidity = Math.Round(sensorData.Humidity, 2);

                        Console.Clear();
                        Console.WriteLine("Listening for messages. Press Enter to exit.");
                        Console.WriteLine("Sensor Data Received:");
                        Console.WriteLine($"  Temperature: {sensorData.Temperature}");
                        Console.WriteLine($"  Humidity   : {sensorData.Humidity}");
                        Console.WriteLine($"  Location   : {sensorData.Location}");
                        Console.WriteLine($"  Date       : {sensorData.Date}");

                        // Save to database
                        using (var dbContext = new DBContext())
                        {
                            // Delete rows older than 6 months
                            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                            dbContext.SensorData.RemoveRange(dbContext.SensorData.Where(data => data.Date < sixMonthsAgo));
                            await dbContext.SaveChangesAsync();

                            // Add new sensor data
                            dbContext.SensorData.Add(sensorData);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not parse sensor data.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to decode or deserialize payload: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Payload: [Ingen payload]");
            }
        };

        await client.ConnectAsync(options);
        await client.SubscribeAsync("#");

        Console.WriteLine("Listening for messages. Press Enter to exit.");
        Console.ReadLine();
    }
}