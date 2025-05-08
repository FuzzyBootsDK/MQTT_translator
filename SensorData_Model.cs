using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTT_translator
{
    public class SensorData_Model
    { 
        public int ID { get; set; }
        public double Humidity { get; set; }
        public double Temperature { get; set; }
        public string Location { get; set; }
        public DateTime Date { get; set; }
    }
}
