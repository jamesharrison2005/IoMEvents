using System;
using System.Collections.Generic;
using System.Text;

namespace IomEvents.Domain
{
    public class Event
    {
        public Guid id { get; set; }
        public String title { get; set; } = string.Empty;
        public String description { get; set; } = string.Empty;
        public DateTime startDate { get; set; }
        public String location { get; set; } = string.Empty;
        public String Category { get; set; } = string.Empty;
        public String SourceUrl { get; set; } = string.Empty;
    }
}
