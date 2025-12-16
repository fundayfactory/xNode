using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace XNodeEditor
{
    public interface INoodleDrawer
    {
        string Name { get; }

        void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints);

        bool TryFindPointWithinDistance(NodePort outputPort, NodePort inputPort, Vector2 mousePosition, float zoom, Gradient gradient, List<Vector2> gridPoints, out Vector2 point, out int gridPointIndex);
    }
}