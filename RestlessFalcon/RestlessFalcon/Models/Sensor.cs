﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestlessFalcon.Models
{
    public class Sensor
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public string Location { get; set; }
        public string Model { get; set; }
    }
}
