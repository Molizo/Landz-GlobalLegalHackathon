using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GLHackathonWebApp.Pages.Locations
{
    public class Property
    {
        public int UsefulSurface { get; set; }
        public int BuiltSurface { get; set; }
        public bool BuildingAccessFootpath { get; set; }
        public bool BuildingAccessAutomobile { get; set; }
        public string BuildingStructure { get; set; }
        public bool CommunalSpaces { get; set; }
        public bool ExternalSurfaces { get; set; }
        public int DistanceFromPerimeters { get; set; }
        public int MaxHeight { get; set; }
        public string Utilities { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}