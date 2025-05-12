# MQTT Translator

The **MQTT Translator** is a .NET 8 application designed to process sensor data received via MQTT, store it in a SQL Server database, and periodically clean up old data. This application is built using C# 12.0 and leverages modern libraries like `MQTTnet` for MQTT communication and `Entity Framework Core` for database operations.

## Features

- **Real-Time MQTT Integration**: Subscribes to all MQTT topics and processes incoming messages in real-time.
- **Data Storage**: Stores sensor data (temperature, humidity, location, and timestamp) in a SQL Server database.
- **Data Cleanup**: Periodically removes sensor data older than six months to maintain database efficiency.
- **Scalable Design**: Uses a thread-safe queue to handle high volumes of incoming data efficiently.
- **Configurable Database Connection**: Reads database connection details from a `.env` file for flexibility and security.

## Prerequisites

- .NET 8 SDK
- SQL Server instance
- MQTT broker (e.g., Mosquitto)
- `.env` file for database configuration

## Installation

1. Clone the repository:
   


2. Install dependencies:
   Ensure the required NuGet packages are installed. These are defined in the `MQTT_translator.csproj` file:
   - `DotNetEnv`
   - `Microsoft.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.SqlServer`
   - `MQTTnet`
   - `Newtonsoft.Json`

   Run the following command to restore dependencies:
   

                    
3. Configure the `.env` file:
   Create a `.env` file in the project directory with the following variables:
   Server                 = "DB_SERVER"
   Database               = "DB_DATABASE"
   UserId                 = "DB_USERID"
   Password               = "DB_PASSWORD"
   TrustServerCertificate = "DB_TRUST_SERVER_CERTIFICATE"



## Usage

1. Start the MQTT broker (e.g., Mosquitto) and ensure it is running on the configured address and port (default: `127.0.0.1:1883`).

2. Run the application:

3. The application will:
   - Subscribe to all MQTT topics (`#`).
   - Process incoming sensor data messages.
   - Store the data in the database.
   - Periodically clean up data older than six months.

4. To stop the application, press `Enter`.

## Project Structure

- **`Program.cs`**: The main entry point of the application. Handles MQTT message processing, database operations, and cleanup tasks.
- **`SensorData_Model.cs`**: Defines the `SensorData_Model` class, which represents the structure of the sensor data.
- **`DBContext.cs`**: Configures the database context using `Entity Framework Core` and reads connection settings from the `.env` file.
- **`MQTT_translator.csproj`**: Project file defining dependencies and build settings.

## Sensor Data Model

The `SensorData_Model` class represents the structure of the sensor data:


## Dependencies

The project uses the following NuGet packages:
- [DotNetEnv](https://www.nuget.org/packages/DotNetEnv): For loading environment variables from a `.env` file.
- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore): For database operations.
- [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer): SQL Server provider for Entity Framework Core.
- [MQTTnet](https://www.nuget.org/packages/MQTTnet): For MQTT communication.
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json): For JSON serialization and deserialization.

## How It Works

1. **MQTT Message Handling**:
   - The application subscribes to all MQTT topics (`#`) and listens for incoming messages.
   - Each message payload is deserialized into a `SensorData_Model` object.
   - The data is rounded to two decimal places for temperature and humidity and enqueued for further processing.

2. **Data Processing**:
   - A background task dequeues sensor data in batches (up to 50 items) and writes them to the database.
   - The database operations are performed using `Entity Framework Core`.

3. **Periodic Cleanup**:
   - Another background task runs every 12 hours to remove sensor data older than six months from the database.

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request with your changes.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.

## Acknowledgments

- [MQTTnet](https://github.com/dotnet/MQTTnet) for providing a robust MQTT library.
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) for simplifying database operations.

---
Feel free to reach out if you have any questions or suggestions for improvement!
