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
                Vector2 pointA = gridPoints[i];
                Vector2 pointB = gridPoints[i + 1];

                // Draws the line with the coloring.
                Vector2 prevPoint = pointA;

                // Approximately one segment per 5 pixels
                int segments = (int)Vector2.Distance(pointA, pointB) / 5;
                segments = Math.Max(segments, 1);
                var draw = 0;

                for (var j = 0; j <= segments; j++)
                {
                    draw++;
                    float t = j / (float)segments;
                    Vector2 lerp = Vector2.Lerp(pointA, pointB, t);

                    if (draw > 0)
                    {
                        if (i == length - 2) Handles.color = gradient.Evaluate(t);

                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, prevPoint, lerp);
                    }

                    prevPoint = lerp;

                    if (stroke == NoodleStroke.Dashed && draw >= 2) draw = -2;
                }
            }
        }

        public bool TryFindPointWithinDistance(NodePort outputPort, NodePort inputPort, Vector2 mousePosition,
            float zoom, Gradient gradient, List<Vector2> gridPoints, out Vector2 point, out int gridPointIndex)
        {
            point = Vector2.zero;
            gridPointIndex = -1;

            return false;
        }
    }
}