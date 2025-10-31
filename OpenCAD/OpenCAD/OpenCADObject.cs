using System.Collections.Concurrent;

namespace OpenCAD
{
    public class OpenCADObject
    {
        protected ConcurrentDictionary<int, Property> properties = new();
        protected ConcurrentDictionary<Guid, OpenCADObject> children = new();

        protected bool _isDrawable = false;
        private Guid _id = Guid.NewGuid();

        public bool IsDrawable => _isDrawable;

        public void Add(OpenCADObject obj)
        {
            children.TryAdd(obj.ID, obj);
        }

        public Guid ID => _id;

        /// <summary>
        /// Remove a child object by reference
        /// </summary>
        public bool Remove(OpenCADObject obj)
        {
            return children.TryRemove(obj.ID, out _);
        }

        /// <summary>
        /// Remove a child object by ID
        /// </summary>
        public bool Remove(Guid id)
        {
            return children.TryRemove(id, out _);
        }
    }
}
