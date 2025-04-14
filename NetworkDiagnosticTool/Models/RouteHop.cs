using System;
using System.Collections.Generic;
using System.Linq;

namespace NetworkDiagnosticTool.Models
{
    public class RouteHop
    {
        public int HopNumber { get; set; }
        public string? HostName { get; set; }
        public string? IpAddress { get; set; }
        public string? Location { get; set; }
        public string? CountryCode { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public List<int> DelayTimes { get; set; } = new List<int>();
        public double AverageDelay => DelayTimes.Any(d => d >= 0) ? 
            DelayTimes.Where(d => d >= 0).Average() : -1;
        public bool IsTimeout => DelayTimes.All(d => d < 0);

        public RouteHop()
        {
            HostName = string.Empty;
            IpAddress = string.Empty;
            Location = string.Empty;
        }
    }
} 