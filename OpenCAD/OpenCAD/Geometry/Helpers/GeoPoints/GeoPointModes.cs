using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Geometry.Helpers.GeoPoints
{
    [Flags]
    public enum GeoPointModes
    {
        None = 0,
        Vertex = 1,        
        Middle = 2,           
        Center = 4,          
        Point = 8,          
        Quadrant = 16,     
        Intersection = 32,         
        Anchor = 64,           
        Perpendicular = 128,      
        Tangent = 256,     
        NearestPoint = 512,    
        ApparentCrossing = 2048,
        ExtensionSnap = 4096
    }
}
