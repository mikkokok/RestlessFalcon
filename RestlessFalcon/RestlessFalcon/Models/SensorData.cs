using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestlessFalcon.Models
{
    public class SensorData
    {
        public int Id { get; set; }
        public int SensorId { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public double ValvePosition { get; set; }
        public DateTime Time { get; set; }
    }
}
