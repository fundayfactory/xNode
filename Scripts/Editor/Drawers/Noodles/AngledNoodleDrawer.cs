using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor {
    [Serializable]
    public sealed class AngledNoodleDrawer : INoodleDrawer {
        public string Name => "Angled";

        private class PointData
        {
            public Vector2 point;
            public float normalizedDistance;
            public float distance;
        }

        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints) {
            int length = gridPoints.Count;
            float nodePadding = 25;

            var pointDatas = new List<PointData>();
            var totalLength = 0f;

            if (length == 2 && outputPort != null && inputPort != null && outputPort.node == inputPort.node) {
                Vector2 nodePos = NodeEditorWindow.current.GridToWindowPosition(outputPort.node.position);

                float nodeTop = nodePos.y - nodePadding / zoom + 16 / zoom;

                pointDatas.Add(new PointData{point = gridPoints[0]});
                pointDatas.Add(new PointData{point = new Vector2(gridPoints[0].x + nodePadding / zoom, gridPoints[0].y)});
                pointDatas.Add(new PointData{point = new Vector2(gridPoints[0].x + nodePadding / zoom, nodeTop)});
                pointDatas.Add(new PointData{point = new Vector2(gridPoints[1].x - nodePadding / zoom, nodeTop)});
                pointDatas.Add(new PointData{point = new Vector2(gridPoints[1].x - nodePadding / zoom, gridPoints[1].y)});
                pointDatas.Add(new PointData{point = gridPoints[1]});

                for (var i = 1; i < pointDatas.Count; i++) {
                    totalLength += (pointDatas[i].point - pointDatas[i - 1].point).magnitude;
                    pointDatas[i].distance = totalLength;
                }
            } else {
                for (var i = 0; i < length - 1; i++) {
                    if (gridPoints[i].x <= gridPoints[i + 1].x - ((nodePadding * 2f) / zoom)) {
                        Vector2 start_1 = gridPoints[i];
                        Vector2 end_1 = gridPoints[i + 1];

                        float midpoint = (start_1.x + end_1.x) * 0.5f;
                        start_1.x = midpoint;
                        end_1.x = midpoint;

                        Vector2 prev = pointDatas.Count > 0 ? pointDatas[pointDatas.Count - 1].point : gridPoints[i];

                        pointDatas.Add(new PointData { point = gridPoints[i], distance = totalLength += (gridPoints[i] - prev).magnitude});
                        pointDatas.Add(new PointData { point = start_1, distance = totalLength += (start_1 - gridPoints[i]).magnitude});
                        pointDatas.Add(new PointData { point = end_1, distance = totalLength += (end_1 - start_1).magnitude});
                        pointDatas.Add(new PointData { point = gridPoints[i+1], distance = totalLength += (gridPoints[i+1] - end_1).magnitude});
                    } else {
                        float midpoint = (gridPoints[i].y + gridPoints[i + 1].y) * 0.5f;
                        Vector2 start_1 = gridPoints[i];
                        Vector2 end_1 = gridPoints[i + 1];
                        start_1.x += nodePadding / zoom;
                        end_1.x -= nodePadding / zoom;
                        Vector2 start_2 = start_1;
                        Vector2 end_2 = end_1;
                        start_2.y = midpoint;
                        end_2.y = midpoint;

                        Vector2 prev = pointDatas.Count > 0 ? pointDatas[pointDatas.Count - 1].point : gridPoints[i];

                        pointDatas.Add(new PointData { point = gridPoints[i], distance = totalLength += (gridPoints[i] - prev).magnitude});
                        pointDatas.Add(new PointData { point = start_1, distance = totalLength += (start_1 - gridPoints[i]).magnitude});
                        pointDatas.Add(new PointData { point = start_2, distance = totalLength += (start_2 - start_1).magnitude});
                        pointDatas.Add(new PointData { point = end_2, distance = totalLength += (end_2 - start_2).magnitude});
                        pointDatas.Add(new PointData { point = end_1, distance = totalLength += (end_1 - end_2).magnitude});
                        pointDatas.Add(new PointData { point = gridPoints[i+1], distance = totalLength += (gridPoints[i+1] - end_1).magnitude});
                    }
                }
            }

            if (pointDatas.Count < 2)
                return;

            float halfLength = totalLength / 2f;

            Vector2 center = Vector2.zero;
            for (var i = 0; i < pointDatas.Count-1; i++) {
                PointData pd1 = pointDatas[i];
                PointData pd2 = pointDatas[i+1];

                if (pd1.distance <= halfLength && pd2.distance >= halfLength)
                    center = pd1.point + (pd2.point - pd1.point) * (pd2.distance - pd1.distance) / (totalLength - pd1.distance);

                pd1.normalizedDistance = pd2.distance / totalLength;
                Handles.color = gradient.Evaluate(pd1.normalizedDistance);
                NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, pd1.point, pd2.point);
            }

            if(outputPort != null && inputPort != null)
                NodeEditorWindow.current.SetConnectionLabelPoint(outputPort, inputPort, center);
        }
    }
}