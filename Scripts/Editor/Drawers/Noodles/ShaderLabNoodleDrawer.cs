using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor {
    [Serializable]
    public sealed class ShaderLabNoodleDrawer : INoodleDrawer {
        public string Name => "ShaderLab";

        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints)
        {
            int length = gridPoints.Count;

            Vector2 start = gridPoints[0];
            Vector2 end = gridPoints[length - 1];

            // Modify first and last point in array so we can loop trough them nicely.
            gridPoints[0] += GetTangentForPort(outputPort) * (20 / zoom);
            gridPoints[length - 1] += GetTangentForPort(inputPort) * (20 / zoom);

            // Draw first vertical lines going out from nodes
            Handles.color = gradient.Evaluate(0f);
            NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start, gridPoints[0]);
            Handles.color = gradient.Evaluate(1f);
            NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end, gridPoints[length - 1]);

            for (var i = 0; i < length - 1; i++) {
                Vector2 point_a = gridPoints[i];
                Vector2 point_b = gridPoints[i + 1];

                // Draws the line with the coloring.
                Vector2 prev_point = point_a;

                // Approximately one segment per 5 pixels
                int segments = (int)Vector2.Distance(point_a, point_b) / 5;
                segments = Math.Max(segments, 1);
                var draw = 0;

                for (var j = 0; j <= segments; j++) {
                    draw++;
                    float t = j / (float)segments;
                    Vector2 lerp = Vector2.Lerp(point_a, point_b, t);

                    if (draw > 0) {
                        if (i == length - 2) Handles.color = gradient.Evaluate(t);

                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, prev_point, lerp);
                    }

                    prev_point = lerp;

                    if (stroke == NoodleStroke.Dashed && draw >= 2)
                        draw = -2;
                }
            }

            gridPoints[0] = start;
            gridPoints[length - 1] = end;
        }

        public bool TryFindPointWithinDistance(NodePort outputPort, NodePort inputPort, Vector2 mousePosition, float zoom, Gradient gradient, List<Vector2> gridPoints, out Vector2 point, out int gridPointIndex)
        {
            point = Vector2.zero;
            gridPointIndex = -1;
            return false;
        }

        private Vector2 GetTangentForPort(NodePort port) {
            if (port == null)
                return Vector2.left;

            if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), port.fieldName, out Node.InputAttribute attIn) && attIn.isVerticalAligned)
                return Vector2.down;

            if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), port.fieldName, out Node.OutputAttribute attOut) && attOut.isVerticalAligned)
                return Vector2.up;

            return port.direction == NodePort.IO.Input ? Vector2.left : Vector2.right;
        }
    }
}