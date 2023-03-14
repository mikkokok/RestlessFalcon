using System;
using System.Collections.Generic;

namespace RestlessFalcon.Models
{
    public class ElectricityPrice
    {
        public string Date { get; set; }

        public string Price { get; set; }
        public int Hour { get; set; }
    }
}
