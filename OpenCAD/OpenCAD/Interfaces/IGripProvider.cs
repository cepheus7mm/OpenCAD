using OpenCAD.Grips;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAD.Interfaces
{
    public interface IGripProvider
    {
        /// <summary>
        /// Returns all grip points for this entity.
        /// </summary>
        IEnumerable<Grip> GetGrips(OpenCADObject entity);

        /// <summary>
        /// Applies a delta to the specified grip and returns a new modified entity.
        /// </summary>
        OpenCADObject ApplyGripDelta(Grip activeGrip, Grip targetGrip, Vector2 delta, GripEditMode gripEditMode);
        OpenCADObject ApplyGripDelta(Grip activeGrip, OpenCADObject targetObject, Vector2 delta, GripEditMode gripEditMode);
    }
}
