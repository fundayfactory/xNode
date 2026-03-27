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
    public sealed class AngledNoodleDrawer : INoodleDrawer
    {
        public string Name => "Angled";

        private class PointData
        {
            public Vector2 Point;
            public int GridPointIndex;
            public float NormalizedDistance;
            public float Distance;
        }

        private static Vector2 DirectionToVector(NoodleDirection direction)
        {
            return new Vector2((direction & NoodleDirection.Right) != 0 ? 1 : (direction & NoodleDirection.Left) != 0 ? -1 : 0, (direction & NoodleDirection.Up) != 0 ? 1 : (direction & NoodleDirection.Down) != 0 ? -1 : 0);
        }

        private class PointDirectionalData
        {
            public Vector2 Point;
            public int GridPointIndex;
            public NoodleDirection OutDirection;
            public NoodleDirection InDirection;
            public NoodleDirection ValidOutDirections => NoodleDirection.All ^ OutDirection ^ InDirection;
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

                if (pd1.Distance <= halfLength && pd2.Distance >= halfLength)
                     center = pd1.Point + (pd2.Point - pd1.Point) * ((halfLength - pd1.Distance) / (pd2.Distance - pd1.Distance));

                pd1.NormalizedDistance = pd2.Distance / totalLength;
                Handles.color = gradient.Evaluate(pd1.NormalizedDistance);
                NoodleDrawerUtility.DrawAAPolyLineNonAlloc(thickness, pd1.Point, pd2.Point);
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

                    pointDatas.Add(new PointData{Point = start, GridPointIndex = 0});
                    pointDatas.Add(new PointData{Point = new Vector2(start.x + nodePaddingZoomed, start.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData{Point = new Vector2(start.x + nodePaddingZoomed, nodeTop), GridPointIndex = 0});
                    pointDatas.Add(new PointData{Point = new Vector2(end.x - nodePaddingZoomed, nodeTop), GridPointIndex = 0});
                    pointDatas.Add(new PointData{Point = new Vector2(end.x - nodePaddingZoomed, end.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData{Point = end, GridPointIndex = 1});
                }
                else if (start.x <= end.x - nodePaddingZoomed * 2f)
                {
                    float midpoint = (start.x + end.x) * 0.5f;
                    pointDatas.Add(new PointData { Point = start, GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(midpoint, start.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(midpoint, end.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = end, GridPointIndex = 1});
                }
                else
                {
                    float midpoint = (start.y + end.y) * 0.5f;
                    pointDatas.Add(new PointData { Point = start, GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(start.x + nodePaddingZoomed, start.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(start.x + nodePaddingZoomed, midpoint), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(end.x - nodePaddingZoomed, midpoint), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = new Vector2(end.x - nodePaddingZoomed, end.y), GridPointIndex = 0});
                    pointDatas.Add(new PointData { Point = end, GridPointIndex = 1});
                }

                for (var i = 1; i < pointDatas.Count; i++)
                {
                    totalLength += (pointDatas[i].Point - pointDatas[i - 1].Point).magnitude;
                    pointDatas[i].Distance = totalLength;
                }
            }
            else
            {
                List<PointDirectionalData> pointDirectionalDatas = new List<PointDirectionalData>();

                pointDirectionalDatas.Add(new PointDirectionalData
                {
                    Point = gridPoints[0] + Vector2.right * nodePaddingZoomed,
                    InDirection = NoodleDirection.Left,
                    GridPointIndex = 0
                });

                for (var i = 1; i < gridPoints.Count-1; i++)
                {
                    pointDirectionalDatas.Add(new PointDirectionalData{Point = gridPoints[i], GridPointIndex = i});
                }
                if(inputPort == null)
                    pointDirectionalDatas.Add(new PointDirectionalData{Point = gridPoints[^1], GridPointIndex = gridPoints.Count-1});
                else
                    pointDirectionalDatas.Add(new PointDirectionalData{Point = gridPoints[^1] + Vector2.left * nodePaddingZoomed, GridPointIndex = gridPoints.Count-1, OutDirection = NoodleDirection.Right});

                pointDatas.Add(new PointData{Point = gridPoints[0], GridPointIndex = 0});

                for (var i = 0; i < pointDirectionalDatas.Count-1; i++)
                {
                    PointDirectionalData p1 = pointDirectionalDatas[i];
                    PointDirectionalData p2 = pointDirectionalDatas[i+1];

                    CalcConnection(p1, p2, pointDatas);
                }

                pointDatas.Add(new PointData{Point = gridPoints[^1], GridPointIndex = gridPoints.Count-1});

                for (var i = 1; i < pointDatas.Count; i++)
                {
                    totalLength += (pointDatas[i].Point - pointDatas[i - 1].Point).magnitude;
                    pointDatas[i].Distance = totalLength;
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

            if (TryGeneratePointData(outputPort, inputPort, zoom, gridPoints, out var pointDatas, out _))
                return false;

            for (var i = 0; i < pointDatas.Count - 1; i++)
            {
                if (IsPointWithinDistanceOfLineSegment(pointDatas[i].Point, pointDatas[i + 1].Point, mousePosition, 10 / zoom, out point))
                {
                    gridPointIndex = pointDatas[i].GridPointIndex;
                    return true;
                }
            }

            return false;
        }

        public static bool IsPointWithinDistanceOfLineSegment(
            Vector2 linePointA,
            Vector2 linePointB,
            Vector2 targetPoint,
            float distanceThreshold,
            out Vector2 closestPoint)
        {
            Vector2 lineSegment = linePointB - linePointA;
            Vector2 targetDirection = targetPoint - linePointA;

            float lineSegmentLength = lineSegment.sqrMagnitude;

            if (lineSegmentLength == 0f)
            {
                closestPoint = linePointA;
                return Vector2.Distance(targetPoint, linePointA) <= distanceThreshold;
            }

            float dotProduct = Vector2.Dot(targetDirection, lineSegment);
            float t = dotProduct / lineSegmentLength;

            t = Mathf.Clamp01(t);

            closestPoint = linePointA + t * lineSegment;

            float actualDistance = Vector2.Distance(targetPoint, closestPoint);

            return actualDistance <= distanceThreshold;
        }

        private void CalcConnection(PointDirectionalData p1, PointDirectionalData p2, List<PointData> pointDatas)
        {
            if(IsOpposite(p1, p2, out NoodleDirection outOpposite, out NoodleDirection inOpposite))
            {
                pointDatas.Add(new PointData{Point = p1.Point, GridPointIndex = p1.GridPointIndex});
                pointDatas.Add(new PointData{Point = p2.Point, GridPointIndex = p2.GridPointIndex});

                p1.OutDirection = outOpposite;
                p2.InDirection = inOpposite;

                return;
            }

            if (IsCrossing(p1, p2, out Vector2 crossingPoint, out NoodleDirection outCrossing, out NoodleDirection inCrossing))
            {
                pointDatas.Add(new PointData{Point = p1.Point, GridPointIndex = p1.GridPointIndex});
                pointDatas.Add(new PointData{Point = crossingPoint, GridPointIndex = p1.GridPointIndex});
                pointDatas.Add(new PointData{Point = p2.Point, GridPointIndex = p2.GridPointIndex});

                p1.OutDirection = outCrossing;
                p2.InDirection = inCrossing;

                return;
            }

            if (FindBestDirections(p1, p2))
                GenerateConnection(p1, p2, pointDatas);
        }

        private bool IsOpposite(PointDirectionalData current, PointDirectionalData other, out NoodleDirection outDir, out NoodleDirection inDir)
        {
            if ((current.ValidOutDirections & NoodleDirection.Left) != 0 && (other.ValidOutDirections & NoodleDirection.Right) != 0 && current.Point.x >= other.Point.x && Mathf.Approximately(current.Point.y, other.Point.y))
            {
                outDir = NoodleDirection.Left;
                inDir = NoodleDirection.Right;
                return true;
            }

            if ((current.ValidOutDirections & NoodleDirection.Right) != 0 && (other.ValidOutDirections & NoodleDirection.Left) != 0 && current.Point.x <= other.Point.x && Mathf.Approximately(current.Point.y, other.Point.y))
            {
                outDir = NoodleDirection.Right;
                inDir = NoodleDirection.Left;
                return true;
            }

            if ((current.ValidOutDirections & NoodleDirection.Up) != 0 && (other.ValidOutDirections & NoodleDirection.Down) != 0 && current.Point.y >= other.Point.y && Mathf.Approximately(current.Point.x, other.Point.x))
            {
                outDir = NoodleDirection.Up;
                inDir = NoodleDirection.Down;
                return true;
            }

            if ((current.ValidOutDirections & NoodleDirection.Down) != 0 && (other.ValidOutDirections & NoodleDirection.Up) != 0 && current.Point.y <= other.Point.y && Mathf.Approximately(current.Point.x, other.Point.x))
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
            NoodleDirection opposite = current.InDirection.Opposite();

            if (IsIntersecting(opposite, other.ValidOutDirections, current.Point, other.Point, out crossingPoint, out outDir, out inDir))
                return true;

            NoodleDirection valid = current.ValidOutDirections & ~opposite;
            if (IsIntersecting(valid, other.ValidOutDirections, current.Point, other.Point, out crossingPoint, out outDir, out inDir))
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
            if ((current.ValidOutDirections & NoodleDirection.Left) != 0 && current.Point.x >= other.Point.x)
            {
                if ((other.ValidOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Left) != 0)
                    return true;
            }
            if ((current.ValidOutDirections & NoodleDirection.Right) != 0 && current.Point.x <= other.Point.x)
            {
                if ((other.ValidOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Right) != 0)
                    return true;
            }
            if ((current.ValidOutDirections & NoodleDirection.Up) != 0 && current.Point.y <= other.Point.y)
            {
                if ((other.ValidOutDirections & NoodleDirection.Down) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Up) != 0)
                    return true;
            }
            if ((current.ValidOutDirections & NoodleDirection.Down) != 0 && current.Point.y >= other.Point.y)
            {
                if ((other.ValidOutDirections & NoodleDirection.Up) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Left) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Right) != 0)
                    return true;
                if ((other.ValidOutDirections & NoodleDirection.Down) != 0)
                    return true;
            }

            return false;
        }

        private void GenerateConnection(PointDirectionalData current, PointDirectionalData other, List<PointData> pointDatas)
        {
            var p1 = current.Point;
            var p2 = other.Point;
            var xDiff = other.Point.x - current.Point.x;
            var yDiff = other.Point.y - current.Point.y;
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

            pointDatas.Add(new PointData{Point = p1, GridPointIndex = current.GridPointIndex});
            pointDatas.Add(new PointData{Point = p2, GridPointIndex = current.GridPointIndex});
            pointDatas.Add(new PointData{Point = other.Point, GridPointIndex = other.GridPointIndex});
        }
    }
}