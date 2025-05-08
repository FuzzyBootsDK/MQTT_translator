using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MQTT_translator
{
    public class DBContext : DbContext
    {
        public DbSet<SensorData_Model> SensorData { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Load environment variables from the .env file
            DotNetEnv.Env.Load();

            // Retrieve individual fields from the environment variables
            var server = Environment.GetEnvironmentVariable("DB_SERVER");
            var database = Environment.GetEnvironmentVariable("DB_DATABASE");
            var userId = Environment.GetEnvironmentVariable("DB_USERID");
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
            var trustServerCertificate = Environment.GetEnvironmentVariable("DB_TRUST_SERVER_CERTIFICATE");

            // Validate that all required fields are present
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database) ||
                string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("One or more database connection fields are missing in the .env file.");
            }

            // Construct the connection string
            var connectionString = $"Server={server};Database={database};User Id={userId};Password={password};TrustServerCertificate={trustServerCertificate};";

            // Configure the DbContext with the connection string
            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
