using System.Collections.Concurrent;

namespace OpenCAD
{
    public class OpenCADObject
    {
        protected ConcurrentDictionary<int, Property> properties = new();
        protected ConcurrentDictionary<Guid, OpenCADObject> children = new();
    }
}
