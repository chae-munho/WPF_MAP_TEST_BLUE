using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Map.Models
{
    public sealed class GpsResponse
    {
        public double lat { get; set; }
        public double lng { get; set; }
        public string source { get; set; } = "";
    }
}
