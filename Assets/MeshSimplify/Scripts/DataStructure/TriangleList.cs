using System.Collections.Generic;

namespace Chaos
{
    /// <summary>
    /// A list of triangles. We encapsulate this as a class to be able to serialize a List of TriangleLists if we need to.
    /// Unity doesn't serialize a list of lists or an array of lists.
    /// </summary>
    public class TriangleList
    {
        public TriangleList(int capacity)
        {
            ListTriangles = new List<Triangle>(capacity);
        }

        public List<Triangle> ListTriangles;
    }
}
