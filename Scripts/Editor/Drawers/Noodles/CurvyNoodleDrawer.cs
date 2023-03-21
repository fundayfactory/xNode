using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor.Noodles
{
    [Serializable]
    public sealed class CurvyNoodleDrawer : INoodleDrawer
    {
        public string Name => "Curvy";

        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints)
        {
            Vector2 outputTangent = Vector2.right;
            int length = gridPoints.Count;

            for (var i = 0; i < length - 1; i++)
            {
                Vector2 inputTangent;
                // Cached most variables that repeat themselves here to avoid so many indexer calls :p
                Vector2 point_a = gridPoints[i];
                Vector2 point_b = gridPoints[i + 1];
                float dist_ab = Vector2.Distance(point_a, point_b);

                if (i == 0)
                    outputTangent = zoom * dist_ab * 0.01f * Vector2.right;

                if (i < length - 2)
                {
                    Vector2 point_c = gridPoints[i + 2];
                    Vector2 ab = (point_b - point_a).normalized;
                    Vector2 cb = (point_b - point_c).normalized;
                    Vector2 ac = (point_c - point_a).normalized;
                    Vector2 p = (ab + cb) * 0.5f;
                    float tangentLength = (dist_ab + Vector2.Distance(point_b, point_c)) * 0.005f * zoom;
                    float side = ((ac.x * (point_b.y - point_a.y)) - (ac.y * (point_b.x - point_a.x)));

                    p = tangentLength * Mathf.Sign(side) * new Vector2(-p.y, p.x);
                    inputTangent = p;
                }
                else
                {
                    inputTangent = zoom * dist_ab * 0.01f * Vector2.left;
                }

                // Calculates the tangents for the bezier's curves.
                float zoomCoef = 50 / zoom;
                Vector2 tangent_a = point_a + outputTangent * zoomCoef;
                Vector2 tangent_b = point_b + inputTangent * zoomCoef;

                // Hover effect.
                int division = Mathf.RoundToInt(.2f * dist_ab) + 3;

                // Coloring and bezier drawing.
                var draw = 0;
                Vector2 bezierPrevious = point_a;

                for (var j = 1; j <= division; ++j)
                {
                    if (stroke == NoodleStroke.Dashed)
                    {
                        draw++;

                        if (draw >= 2)
                            draw = -2;

                        if (draw < 0)
                            continue;

                        if (draw == 0)
                            bezierPrevious = NoodleDrawerUtility.CalculateBezierPoint(point_a, tangent_a, tangent_b, point_b, (j - 1f) / (float)division);
                    }

                    if (i == length - 2)
                        Handles.color = gradient.Evaluate((j + 1f) / division);

                    Vector2 bezierNext = NoodleDrawerUtility.CalculateBezierPoint(point_a, tangent_a, tangent_b, point_b, j / (float)division);
                    NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, bezierPrevious, bezierNext);
                    bezierPrevious = bezierNext;
                }

                outputTangent = -inputTangent;
            }
        }
    }
}