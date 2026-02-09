using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Map.Models
{
    public sealed class SetDataRequest
    {
        public string status { get; set; } = "success";
        public int operation { get; set; }
        public int value { get; set; }
        public int train { get; set; }
    }
}