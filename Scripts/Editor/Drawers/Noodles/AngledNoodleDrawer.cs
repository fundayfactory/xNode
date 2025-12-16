using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor {

    public static class NoodleDirectionExtension
    {
        public static NoodleDirection Opposite(this NoodleDirection direction)
        {
            NoodleDirection newDirection = NoodleDirection.None;

            if ((direction & NoodleDirection.Left) != 0)
                newDirection |= NoodleDirection.Right;
            if ((direction & NoodleDirection.Right) != 0)
                newDirection |= NoodleDirection.Left;
            if ((direction & NoodleDirection.Up) != 0)
                newDirection |= NoodleDirection.Down;
            if ((direction & NoodleDirection.Down) != 0)
                newDirection |= NoodleDirection.Up;
            return newDirection;
        }
    }

    [Flags]
    public enum NoodleDirection
    {
        None    = 0,
        Up      = 1,
        Down    = 2,
        Left    = 4,
        Right   = 8,
        All     = Up | Down | Left | Right
    }

    [Serializable]
    public sealed class AngledNoodleDrawer : INoodleDrawer {
        public string Name => "Angled";

        private class PointData
        {
            public Vector2 point;
            public int gridPointIndex;
            public float normalizedDistance;
            public float distance;
        }

        private static Vector2 DirectionToVector(NoodleDirection direction)
        {
            return new Vector2((direction & NoodleDirection.Right) != 0 ? 1 : (direction & NoodleDirection.Left) != 0 ? -1 : 0, (direction & NoodleDirection.Up) != 0 ? 1 : (direction & NoodleDirection.Down) != 0 ? -1 : 0);
        }

        private class PointDirectionalData
        {
            public Vector2 point;
            public int gridPointIndex;
            public NoodleDirection outDirection;
            public NoodleDirection inDirection;
            public NoodleDirection validOutDirections => NoodleDirection.All ^ outDirection ^ inDirection;
        }


        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints)
        {
            if (TryGeneratePointData(outputPort, inputPort, zoom, gridPoints, out var pointDatas, out var totalLength))
                return;

            float halfLength = totalLength / 2f;

            Vector2 center = Vector2.zero;
            for (var i = 0; i < pointDatas.Count-1; i++)
            {
                PointData pd1 = pointDatas[i];
                PointData pd2 = pointDatas[i+1];

                if (pd1.distance <= halfLength && pd2.distance >= halfLength)
                     center = pd1.point + (pd2.point - pd1.point) * ((halfLength - pd1.distance) / (pd2.distance - pd1.distance));

                pd1.normalizedDistance = pd2.distance / totalLength;
                Handles.color = gradient.Evaluate(pd1.normalizedDistance);
                NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, pd1.point, pd2.point);
            }

            if(outputPort != null && inputPort != null)
                NodeEditorWindow.current.SetConnectionLabelPoint(outputPort, inputPort, NodeEditorWindow.current.WindowToGridPosition(center));
        }

        private bool TryGeneratePointData(NodePort outputPort, NodePort inputPort, float zoom, List<Vector2> gridPoints, out List<PointData> pointDatas, out float totalLength)
        {
            pointDatas = new List<PointData>();
            totalLength = 0f;

            int length = gridPoints.Count;

            if (length < 2)
                return false;

            float nodePadding = 25;
            float nodePaddingZoomed = nodePadding / zoom;

            if(length == 2)
            {
                // Direct connetions between two nodes, or dragged connection, without reroute points
                Vector2 start = gridPoints[0];
                Vector2 end = gridPoints[1];

                if (outputPort != null && inputPort != null && outputPort.node == inputPort.node)
                {
                    Vector2 nodePos = NodeEditorWindow.current.GridToWindowPosition(outputPort.node.position);
                    float nodeTop = nodePos.y - (nodePadding - 16) / zoom;

                    pointDatas.Add(new PointData{point = start, gridPointIndex = 0});
                    pointDatas.Add(new PointData{point = new Vector2(start.x + nodePaddingZoomed, start.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData{point = new Vector2(start.x + nodePaddingZoomed, nodeTop), gridPointIndex = 0});
                    pointDatas.Add(new PointData{point = new Vector2(end.x - nodePaddingZoomed, nodeTop), gridPointIndex = 0});
                    pointDatas.Add(new PointData{point = new Vector2(end.x - nodePaddingZoomed, end.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData{point = end, gridPointIndex = 1});
                }
                else if (start.x <= end.x - nodePaddingZoomed * 2f)
                {
                    float midpoint = (start.x + end.x) * 0.5f;
                    pointDatas.Add(new PointData { point = start, gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(midpoint, start.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(midpoint, end.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = end, gridPointIndex = 1});
                }
                else
                {
                    float midpoint = (start.y + end.y) * 0.5f;
                    pointDatas.Add(new PointData { point = start, gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(start.x + nodePaddingZoomed, start.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(start.x + nodePaddingZoomed, midpoint), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(end.x - nodePaddingZoomed, midpoint), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = new Vector2(end.x - nodePaddingZoomed, end.y), gridPointIndex = 0});
                    pointDatas.Add(new PointData { point = end, gridPointIndex = 1});
                }

                for (var i = 1; i < pointDatas.Count; i++)
                {
                    totalLength += (pointDatas[i].point - pointDatas[i - 1].point).magnitude;
                    pointDatas[i].distance = totalLength;
                }
            }
            else
            {
                List<PointDirectionalData> pointDirectionalDatas = new List<PointDirectionalData>();

                pointDirectionalDatas.Add(new PointDirectionalData
                {
                    point = gridPoints[0] + Vector2.right * nodePaddingZoomed,
                    inDirection = NoodleDirection.Left,
                    gridPointIndex = 0
                });

                for (var i = 1; i < gridPoints.Count-1; i++)
                {
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[i], gridPointIndex = i});
                }
                if(inputPort == null)
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[^1], gridPointIndex = gridPoints.Count-1});
                else
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[^1] + Vector2.left * nodePaddingZoomed, gridPointIndex = gridPoints.Count-1, outDirection = NoodleDirection.Right});

                pointDatas.Add(new PointData{point = gridPoints[0], gridPointIndex = 0});

                for (var i = 0; i < pointDirectionalDatas.Count-1; i++)
                {
                    PointDirectionalData p1 = pointDirectionalDatas[i];
                    PointDirectionalData p2 = pointDirectionalDatas[i+1];

                    CalcConnection(p1, p2, pointDatas);
                }

                pointDatas.Add(new PointData{point = gridPoints[^1], gridPointIndex = gridPoints.Count-1});

                for (var i = 1; i < pointDatas.Count; i++)
                {
                    totalLength += (pointDatas[i].point - pointDatas[i - 1].point).magnitude;
                    pointDatas[i].distance = totalLength;
                }
            }

            if (pointDatas.Count < 2)
                return true;

            return false;
        }

        public bool TryFindPointWithinDistance(NodePort outputPort, NodePort inputPort, Vector2 mousePosition, float zoom, Gradient gradient, List<Vector2> gridPoints, out Vector2 point, out int gridPointIndex)
        {
            point = Vector2.zero;
            gridPointIndex = -1;

            if (TryGeneratePointData(outputPort, inputPort, zoom, gridPoints, out var pointDatas, out var totalLength))
                return false;

            for (var i = 0; i < pointDatas.Count - 1; i++)
            {
                if (IsPointWithinDistanceOfLine(pointDatas[i].point, pointDatas[i + 1].point, mousePosition, 10 / zoom, out point))
                {
                    gridPointIndex = pointDatas[i].gridPointIndex;
                    return true;
                }
            }

            return false;
        }

        public static bool IsPointWithinDistanceOfLine(
            Vector2 linePointA,
            Vector2 linePointB,
            Vector2 targetPoint,
            float thresholdDistance,
            out Vector2 closestPoint)
        {
            // 1. Define the vectors needed for the calculation.
            Vector2 lineDirection = linePointB - linePointA;
            Vector2 targetDirection = targetPoint - linePointA;

            // 2. Calculate the projection parameter (t).
            // t = (targetDirection . lineDirection) / ||lineDirection||^2

            float lineLengthSq = lineDirection.sqrMagnitude; // Use sqrMagnitude to avoid sqrt

            // Handle the case where A and B are the same point (line has zero length)
            if (lineLengthSq == 0f)
            {
                closestPoint = linePointA;
                return Vector2.Distance(targetPoint, linePointA) <= thresholdDistance;
            }

            float dotProduct = Vector2.Dot(targetDirection, lineDirection);
            float t = dotProduct / lineLengthSq;

            // 3. Calculate the closest point on the infinite line.
            // Q = A + t * lineDirection
            closestPoint = linePointA + t * lineDirection;

            // 4. Check the distance between the closest point and the target point.
            float actualDistance = Vector2.Distance(targetPoint, closestPoint);

            return actualDistance <= thresholdDistance;
        }

        private void CalcConnection(PointDirectionalData p1, PointDirectionalData p2, List<PointData> pointDatas)
        {
            if(IsOpposite(p1, p2, out NoodleDirection outOpposite, out NoodleDirection inOpposite))
            {
                pointDatas.Add(new PointData{point = p1.point, gridPointIndex = p1.gridPointIndex});
                pointDatas.Add(new PointData{point = p2.point, gridPointIndex = p2.gridPointIndex});

                p1.outDirection = outOpposite;
                p2.inDirection = inOpposite;

                return;
            }

            if (IsCrossing(p1, p2, out Vector2 crossingPoint, out NoodleDirection outCrossing, out NoodleDirection inCrossing))
            {
                pointDatas.Add(new PointData{point = p1.point, gridPointIndex = p1.gridPointIndex});
                pointDatas.Add(new PointData{point = crossingPoint, gridPointIndex = p1.gridPointIndex});
                pointDatas.Add(new PointData{point = p2.point, gridPointIndex = p2.gridPointIndex});

                p1.outDirection = outCrossing;
                p2.inDirection = inCrossing;

                return;
            }

            if (FindBestDirections(p1, p2))
                GenerateConnection(p1, p2, pointDatas);
        }

        private bool IsOpposite(PointDirectionalData current, PointDirectionalData other, out NoodleDirection outDir, out NoodleDirection inDir)
        {
            if ((current.validOutDirections & NoodleDirection.Left) != 0 && (other.validOutDirections & NoodleDirection.Right) != 0 && current.point.x >= other.point.x && Mathf.Approximately(current.point.y, other.point.y))
            {
                outDir = NoodleDirection.Left;
                inDir = NoodleDirection.Right;
                return true;
            }

            if ((current.validOutDirections & NoodleDirection.Right) != 0 && (other.validOutDirections & NoodleDirection.Left) != 0 && current.point.x <= other.point.x && Mathf.Approximately(current.point.y, other.point.y))
            {
                outDir = NoodleDirection.Right;
                inDir = NoodleDirection.Left;
                return true;
            }

            if ((current.validOutDirections & NoodleDirection.Up) != 0 && (other.validOutDirections & NoodleDirection.Down) != 0 && current.point.y >= other.point.y && Mathf.Approximately(current.point.x, other.point.x))
            {
                outDir = NoodleDirection.Up;
                inDir = NoodleDirection.Down;
                return true;
            }

            if ((current.validOutDirections & NoodleDirection.Down) != 0 && (other.validOutDirections & NoodleDirection.Up) != 0 && current.point.y <= other.point.y && Mathf.Approximately(current.point.x, other.point.x))
            {
                outDir = NoodleDirection.Down;
                inDir = NoodleDirection.Up;
                return true;
            }

            outDir = NoodleDirection.None;
            inDir = NoodleDirection.None;

            return false;
        }

        private bool IsCrossing(PointDirectionalData current, PointDirectionalData other, out Vector2 crossingPoint, out NoodleDirection outDir, out NoodleDirection inDir)
        {
            NoodleDirection opposite = current.inDirection.Opposite();

            if (IsIntersecting(opposite, other.validOutDirections, current.point, other.point, out crossingPoint, out outDir, out inDir))
                return true;

            NoodleDirection valid = current.validOutDirections & ~opposite;
            if (IsIntersecting(valid, other.validOutDirections, current.point, other.point, out crossingPoint, out outDir, out inDir))
                return true;

            crossingPoint = Vector2.zero;
            outDir = NoodleDirection.None;
            inDir = NoodleDirection.None;

            return false;
        }

        private bool IsIntersecting(NoodleDirection directions, NoodleDirection otherDirections, Vector2 point, Vector2 otherPoint, out Vector2 intersectionPoint, out NoodleDirection outDir, out NoodleDirection inDir)
        {
            intersectionPoint = Vector2.zero;
            outDir = NoodleDirection.None;
            inDir = NoodleDirection.None;

            if (directions == NoodleDirection.None)
                return false;

            if ((directions & NoodleDirection.Left) != 0 && point.x >= otherPoint.x)
            {
                if((otherDirections & NoodleDirection.Up) != 0 && point.y <= otherPoint.y)
                {
                    intersectionPoint = new Vector2(otherPoint.x, point.y);
                    outDir = NoodleDirection.Left;
                    inDir = NoodleDirection.Up;
                    return true;
                }
                if((otherDirections & NoodleDirection.Down) != 0 && point.y >= otherPoint.y)
                {
                    intersectionPoint = new Vector2(otherPoint.x, point.y);
                    outDir = NoodleDirection.Left;
                    inDir = NoodleDirection.Down;
                    return true;
                }
            }

            if ((directions & NoodleDirection.Right) != 0 && point.x <= otherPoint.x)
            {
                if((otherDirections & NoodleDirection.Up) != 0 && point.y <= otherPoint.y)
                {
                    intersectionPoint = new Vector2(otherPoint.x, point.y);
                    outDir = NoodleDirection.Right;
                    inDir = NoodleDirection.Up;
                    return true;
                }
                if((otherDirections & NoodleDirection.Down) != 0 && point.y >= otherPoint.y)
                {
                    intersectionPoint = new Vector2(otherPoint.x, point.y);
                    outDir = NoodleDirection.Right;
                    inDir = NoodleDirection.Down;
                    return true;
                }
            }

            if ( (directions & NoodleDirection.Up) != 0 && point.y >= otherPoint.y)
            {
                if((otherDirections & NoodleDirection.Left) != 0 && point.x <= otherPoint.x)
                {
                    intersectionPoint = new Vector2(point.x, otherPoint.y);
                    outDir = NoodleDirection.Up;
                    inDir = NoodleDirection.Left;
                    return true;
                }
                if((otherDirections & NoodleDirection.Right) != 0 && point.x >= otherPoint.x)
                {
                    intersectionPoint = new Vector2(point.x, otherPoint.y);
                    outDir = NoodleDirection.Up;
                    inDir = NoodleDirection.Right;
                    return true;
                }
            }

            if ((directions & NoodleDirection.Down) != 0 && point.y <= otherPoint.y)
            {
                if((otherDirections & NoodleDirection.Left) != 0 && point.x <= otherPoint.x)
                {
                    intersectionPoint = new Vector2(point.x, otherPoint.y);
                    outDir = NoodleDirection.Down;
                    inDir = NoodleDirection.Left;
                    return true;
                }
                if((otherDirections & NoodleDirection.Right) != 0 && point.x >= otherPoint.x)
                {
                    intersectionPoint = new Vector2(point.x, otherPoint.y);
                    outDir = NoodleDirection.Down;
                    inDir = NoodleDirection.Right;
                    return true;
                }
            }

            return false;
        }

        private bool FindBestDirections(PointDirectionalData current, PointDirectionalData other)
        {
            if ((current.validOutDirections & NoodleDirection.Left) != 0 && current.point.x >= other.point.x)
            {
                if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Right) != 0 && current.point.x <= other.point.x)
            {
                if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Up) != 0 && current.point.y <= other.point.y)
            {
                if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Down) != 0 && current.point.y >= other.point.y)
            {
                if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    return true;
            }

            return false;
        }

        private void GenerateConnection(PointDirectionalData current, PointDirectionalData other, List<PointData> pointDatas)
        {
            var p1 = current.point;
            var p2 = other.point;
            var xDiff = other.point.x - current.point.x;
            var yDiff = other.point.y - current.point.y;
            if(xDiff > yDiff)
            {
                var midPoint = (p1.x + p2.x) * 0.5f;
                p1.x = midPoint;
                p2.x = midPoint;
            }
            else
            {
                var midPoint = (p1.y + p2.y) * 0.5f;
                p1.y = midPoint;
                p2.y = midPoint;
            }

            pointDatas.Add(new PointData{point = p1, gridPointIndex = current.gridPointIndex});
            pointDatas.Add(new PointData{point = p2, gridPointIndex = current.gridPointIndex});
            pointDatas.Add(new PointData{point = other.point, gridPointIndex = other.gridPointIndex});
        }
    }
}