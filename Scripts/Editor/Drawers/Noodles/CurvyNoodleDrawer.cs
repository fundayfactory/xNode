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
            Vector2 outputTangent = GetTangentForPort(outputPort);
            int length = gridPoints.Count;

            for (var i = 0; i < length - 1; i++)
            {
                Vector2 inputTangent;

                // Cached most variables that repeat themselves here to avoid so many indexer calls :p
                Vector2 pointA = gridPoints[i];
                Vector2 pointB = gridPoints[i + 1];
                float distAb = Vector2.Distance(pointA, pointB);

                if (i == 0) outputTangent = zoom * distAb * 0.01f * GetTangentForPort(outputPort);

                if (i < length - 2)
                {
                    Vector2 pointC = gridPoints[i + 2];
                    Vector2 ab = (pointB - pointA).normalized;
                    Vector2 cb = (pointB - pointC).normalized;
                    Vector2 ac = (pointC - pointA).normalized;
                    Vector2 p = (ab + cb) * 0.5f;
                    float tangentLength = (distAb + Vector2.Distance(pointB, pointC)) * 0.005f * zoom;
                    float side = ((ac.x * (pointB.y - pointA.y)) - (ac.y * (pointB.x - pointA.x)));

                    p = tangentLength * Mathf.Sign(side) * new Vector2(-p.y, p.x);
                    inputTangent = p;
                }
                else
                {
                    inputTangent = zoom * distAb * 0.01f * GetTangentForPort(inputPort);
                }

                // Calculates the tangents for the bezier's curves.
                float zoomCoef = 50 / zoom;
                Vector2 tangentA = pointA + outputTangent * zoomCoef;
                Vector2 tangentB = pointB + inputTangent * zoomCoef;

                // Hover effect.
                int division = Mathf.RoundToInt(.2f * distAb) + 3;

                // Coloring and bezier drawing.
                var draw = 0;
                Vector2 bezierPrevious = pointA;

                for (var j = 1; j <= division; ++j)
                {
                    if (stroke == NoodleStroke.Dashed)
                    {
                        draw++;

                        if (draw >= 2) draw = -2;

                        if (draw < 0) continue;

                        if (draw == 0)
                            bezierPrevious = NoodleDrawerUtility.CalculateBezierPoint(pointA, tangentA, tangentB,
                                pointB, (j - 1f) / (float)division);
                    }

                    if (i == length - 2) Handles.color = gradient.Evaluate((j + 1f) / division);

                    Vector2 bezierNext = NoodleDrawerUtility.CalculateBezierPoint(pointA, tangentA, tangentB,
                        pointB, j / (float)division);

                    NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, bezierPrevious, bezierNext);
                    bezierPrevious = bezierNext;
                }

                outputTangent = -inputTangent;
            }
        }

        public bool TryFindPointWithinDistance(NodePort outputPort, NodePort inputPort, Vector2 mousePosition,
            float zoom, Gradient gradient, List<Vector2> gridPoints, out Vector2 point, out int gridPointIndex)
        {
            point = Vector2.zero;
            gridPointIndex = -1;

            return false;
        }

        private Vector2 GetTangentForPort(NodePort port)
        {
            if (port == null)
                return Vector2.left;

            if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), port.fieldName,
                    out Node.InputAttribute attIn) && attIn.isVerticalAligned)
                return Vector2.down;

            if (NodeEditorUtilities.GetCachedAttrib(port.node.GetType(), port.fieldName,
                    out Node.OutputAttribute attOut) && attOut.isVerticalAligned)
                return Vector2.up;

            return port.direction == NodePort.IO.Input ? Vector2.left : Vector2.right;
        }
    }
}