using System.Collections.Concurrent;

namespace OpenCAD
{
    public static class OpenCADObjectExtensions
    {
        /// <summary>
        /// Get all children of an OpenCADObject
        /// </summary>
        public static IEnumerable<OpenCADObject> GetChildren(this OpenCADObject obj)
        {
            // Access the protected children field through reflection or make it accessible
            var childrenField = typeof(OpenCADObject).GetField("children", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (childrenField?.GetValue(obj) is ConcurrentDictionary<Guid, OpenCADObject> children)
            {
                return children.Values;
            }
            
            return Enumerable.Empty<OpenCADObject>();
        }
    }
}