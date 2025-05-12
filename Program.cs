using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTT_translator;
using Microsoft.EntityFrameworkCore;

class Program
{
    // Thread-safe queue to store incoming sensor data for processing
    private static readonly ConcurrentQueue<SensorData_Model> _sensorQueue = new();

    // Token source to manage cancellation of background tasks
    private static CancellationTokenSource _cts = new();

    static async Task Main(string[] args)
    {
        // Create an MQTT client using a factory
        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        // Configure MQTT client options (e.g., server address and port)
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", 1883) // Replace with actual broker address if needed
            .Build();

        // Event handler for processing received MQTT messages
        client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                // Decode the payload and deserialize it into a SensorData_Model object
                var payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                var sensorData = JsonSerializer.Deserialize<SensorData_Model>(payloadString);

                if (sensorData != null)
                {
                    // Round temperature and humidity values to 2 decimal places
                    sensorData.Temperature = Math.Round(sensorData.Temperature, 2);
                    sensorData.Humidity = Math.Round(sensorData.Humidity, 2);

                    // Enqueue the processed sensor data for further handling
                    _sensorQueue.Enqueue(sensorData);
                }
                else
                {
                    Console.WriteLine("Could not parse sensor data.");
                }
            }
            catch (Exception ex)
            {
                // Log any errors during decoding or deserialization
                Console.WriteLine($"Failed to decode or deserialize payload: {ex.Message}");
            }
        };

        // Connect to the MQTT broker and subscribe to all topics
        await client.ConnectAsync(options);
        await client.SubscribeAsync("#"); // Subscribes to all topics

        Console.WriteLine("Listening for messages. Press Enter to exit.");

        // Start background tasks for processing the queue and periodic cleanup
        var dbWorker = Task.Run(() => ProcessQueueAsync(_cts.Token));
        var cleanupWorker = Task.Run(() => PeriodicCleanupAsync(_cts.Token));

        // Wait for user input to terminate the application
        Console.ReadLine();
        _cts.Cancel(); // Signal cancellation to background tasks

        // Wait for all background tasks to complete
        await Task.WhenAll(dbWorker, cleanupWorker);
    }

    /// <summary>
    /// Processes the sensor data queue and writes batches to the database.
    /// </summary>
    /// <param name="token">Cancellation token to stop the task.</param>
    private static async Task ProcessQueueAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var batch = new List<SensorData_Model>();

            // Dequeue up to 50 items from the queue for batch processing
            while (_sensorQueue.TryDequeue(out var data))
            {
                batch.Add(data);
                if (batch.Count >= 50) break;
            }

            if (batch.Any())
            {
                try
                {
                    // Save the batch of sensor data to the database
                    using var dbContext = new DBContext();
                    dbContext.SensorData.AddRange(batch);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log any database write errors
                    Console.WriteLine($"Database write error: {ex.Message}");
                }
            }

            // Delay to control processing frequency
            await Task.Delay(1000, token); // Adjust delay as needed
        }
    }

    /// <summary>
    /// Periodically cleans up old sensor data from the database.
    /// </summary>
    /// <param name="token">Cancellation token to stop the task.</param>
    private static async Task PeriodicCleanupAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Remove sensor data older than 6 months
                using var dbContext = new DBContext();
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var oldData = dbContext.SensorData.Where(d => d.Date < sixMonthsAgo);
                dbContext.SensorData.RemoveRange(oldData);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log any cleanup errors
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }

            // Delay to run cleanup twice a day
            await Task.Delay(TimeSpan.FromHours(12), token);
        }
    }
}
