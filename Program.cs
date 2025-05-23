﻿using System;
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

    // JSON options for case-insensitive property matching
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
                Console.WriteLine($"Raw payload: {payloadString}");

                // Only attempt to deserialize if the payload looks like JSON
                if (!string.IsNullOrWhiteSpace(payloadString) && payloadString.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        var sensorData = JsonSerializer.Deserialize<SensorData_Model>(payloadString, _jsonOptions);
                        if (sensorData != null)
                        {
                            sensorData.Temperature = Math.Round(sensorData.Temperature, 2);
                            sensorData.Humidity = Math.Round(sensorData.Humidity, 2);
                            _sensorQueue.Enqueue(sensorData);

                            // Clear the console and write the deserialized JSON
                            Console.Clear();
                            Console.WriteLine("Press Enter to exit...");
                            string jsonOutput = JsonSerializer.Serialize(sensorData, new JsonSerializerOptions { WriteIndented = true });
                            Console.WriteLine("Deserialized Sensor Data:");
                            Console.WriteLine(jsonOutput);
                        }
                        else
                        {
                            Console.WriteLine("Could not parse sensor data.");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON deserialization error: {jsonEx.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Ignored non-JSON payload.");
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
        try
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
                        using var dbContext = new DBContext();
                        dbContext.SensorData.AddRange(batch);
                        await dbContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Database write error: {ex.Message}");
                    }
                }

                await Task.Delay(1000, token); // This will throw TaskCanceledException when cancelled
            }
        }
        catch (TaskCanceledException)
        {
            // Normal shutdown, no action needed
        }
    }


    /// <summary>
    /// Periodically cleans up old sensor data from the database.
    /// </summary>
    /// <param name="token">Cancellation token to stop the task.</param>
    private static async Task PeriodicCleanupAsync(CancellationToken token)
    {
        try
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
        catch (TaskCanceledException)
        {
            // Normal shutdown, no action needed
        }
    }
}