using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor {
    [Serializable]
    public sealed class AngledNoodleDrawer : INoodleDrawer {
        public string Name => "Angled";

        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints) {
            int length = gridPoints.Count;

            for (var i = 0; i < length - 1; i++) {
                if (i == length - 1) continue; // Skip last index

                if (gridPoints[i].x <= gridPoints[i + 1].x - (50 / zoom)) {
                    Vector2 start_1 = gridPoints[i];
                    Vector2 end_1 = gridPoints[i + 1];

                    float midpoint = (start_1.x + end_1.x) * 0.5f;
                    start_1.x = midpoint;
                    end_1.x = midpoint;

                    if (i == length - 2) {
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                        Handles.color = gradient.Evaluate(0.5f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_1, end_1);
                        Handles.color = gradient.Evaluate(1f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                    }
                    else {
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_1, end_1);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                    }
                } else {
                    float midpoint = (gridPoints[i].y + gridPoints[i + 1].y) * 0.5f;
                    Vector2 start_1 = gridPoints[i];
                    Vector2 end_1 = gridPoints[i + 1];
                    start_1.x += 25 / zoom;
                    end_1.x -= 25 / zoom;
                    Vector2 start_2 = start_1;
                    Vector2 end_2 = end_1;
                    start_2.y = midpoint;
                    end_2.y = midpoint;

                    if (i == length - 2) {
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                        Handles.color = gradient.Evaluate(0.25f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_1, start_2);
                        Handles.color = gradient.Evaluate(0.5f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_2, end_2);
                        Handles.color = gradient.Evaluate(0.75f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_2, end_1);
                        Handles.color = gradient.Evaluate(1f);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                    }
                    else {
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, gridPoints[i], start_1);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_1, start_2);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, start_2, end_2);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_2, end_1);
                        NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, end_1, gridPoints[i + 1]);
                    }
                }
            }
        }
    }
}