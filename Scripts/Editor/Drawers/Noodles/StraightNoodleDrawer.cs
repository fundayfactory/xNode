using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor
{
    [Serializable]
    public sealed class StraightNoodleDrawer : INoodleDrawer
    {
        public string Name => "Straight";

        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints)
        {
            int length = gridPoints.Count;

            for (var i = 0; i < length - 1; i++)
            {
                Vector2 point_a = gridPoints[i];
                Vector2 point_b = gridPoints[i + 1];

                // Draws the line with the coloring.
                Vector2 prev_point = point_a;

                // Approximately one segment per 5 pixels
                int segments = (int) Vector2.Distance(point_a, point_b) / 5;
                segments = Math.Max(segments, 1);
                var draw = 0;

                for (var j = 0; j <= segments; j++)
                {
                    draw++;
                    float t = j / (float) segments;
                    Vector2 lerp = Vector2.Lerp(point_a, point_b, t);

                    if (draw > 0)
                    {
                        if (i == length - 2)
                            Handles.color = gradient.Evaluate(t);

                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, prev_point, lerp);
                    }

                    prev_point = lerp;

                    if (stroke == NoodleStroke.Dashed && draw >= 2)
                        draw = -2;
                }
            }
        }
    }
}