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
            public NoodleDirection outDirection;
            public NoodleDirection inDirection;
            public NoodleDirection validOutDirections => NoodleDirection.All ^ outDirection ^ inDirection;
        }


        public void DrawNoodle(NodeGraph graph, NodePort outputPort, NodePort inputPort, float zoom, Gradient gradient,
            NoodleStroke stroke, float thickness, List<Vector2> gridPoints)
        {
            int length = gridPoints.Count;

            if (length < 2)
                return;

            float nodePadding = 25;
            float nodePaddingZoomed = nodePadding / zoom;

            var pointDatas = new List<PointData>();
            var totalLength = 0f;

            if(length == 2)
            {
                // Direct connetions between two nodes, or dragged connection, without reroute points
                Vector2 start = gridPoints[0];
                Vector2 end = gridPoints[1];

                if (outputPort != null && inputPort != null && outputPort.node == inputPort.node)
                {
                    Vector2 nodePos = NodeEditorWindow.current.GridToWindowPosition(outputPort.node.position);
                    float nodeTop = nodePos.y - (nodePadding - 16) / zoom;

                    pointDatas.Add(new PointData{point = start});
                    pointDatas.Add(new PointData{point = new Vector2(start.x + nodePaddingZoomed, start.y)});
                    pointDatas.Add(new PointData{point = new Vector2(start.x + nodePaddingZoomed, nodeTop)});
                    pointDatas.Add(new PointData{point = new Vector2(end.x - nodePaddingZoomed, nodeTop)});
                    pointDatas.Add(new PointData{point = new Vector2(end.x - nodePaddingZoomed, end.y)});
                    pointDatas.Add(new PointData{point = end});
                }
                else if (start.x <= end.x - nodePaddingZoomed * 2f)
                {
                    float midpoint = (start.x + end.x) * 0.5f;
                    pointDatas.Add(new PointData { point = start});
                    pointDatas.Add(new PointData { point = new Vector2(midpoint, start.y)});
                    pointDatas.Add(new PointData { point = new Vector2(midpoint, end.y)});
                    pointDatas.Add(new PointData { point = end});
                }
                else
                {
                    float midpoint = (start.y + end.y) * 0.5f;
                    pointDatas.Add(new PointData { point = start});
                    pointDatas.Add(new PointData { point = new Vector2(start.x + nodePaddingZoomed, start.y)});
                    pointDatas.Add(new PointData { point = new Vector2(start.x + nodePaddingZoomed, midpoint)});
                    pointDatas.Add(new PointData { point = new Vector2(end.x - nodePaddingZoomed, midpoint)});
                    pointDatas.Add(new PointData { point = new Vector2(end.x - nodePaddingZoomed, end.y)});
                    pointDatas.Add(new PointData { point = end});
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

                pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[0] + Vector2.right * nodePaddingZoomed, inDirection = NoodleDirection.Left});
                for (var i = 1; i < gridPoints.Count-1; i++)
                {
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[i]});
                }
                if(inputPort == null)
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[^1]});
                else
                    pointDirectionalDatas.Add(new PointDirectionalData{point = gridPoints[^1] + Vector2.left * nodePaddingZoomed, outDirection = NoodleDirection.Right});

                pointDatas.Add(new PointData{point = gridPoints[0]});
                for (var i = 0; i < pointDirectionalDatas.Count-1; i++)
                {
                    PointDirectionalData p1 = pointDirectionalDatas[i];
                    PointDirectionalData p2 = pointDirectionalDatas[i+1];

                    CalcConnection(p1, p2, pointDatas);
                }

                pointDatas.Add(new PointData{point = gridPoints[^1]});

                for (var i = 1; i < pointDatas.Count; i++)
                {
                    totalLength += (pointDatas[i].point - pointDatas[i - 1].point).magnitude;
                    pointDatas[i].distance = totalLength;
                }
            }

            if (pointDatas.Count < 2)
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

        private void CalcConnection(PointDirectionalData p1, PointDirectionalData p2, List<PointData> pointDatas)
        {
            if(IsOpposite(p1, p2, out NoodleDirection outOpposite, out NoodleDirection inOpposite))
            {
                pointDatas.Add(new PointData{point = p1.point});
                pointDatas.Add(new PointData{point = p2.point});

                p1.outDirection = outOpposite;
                p2.inDirection = inOpposite;

                return;
            }

            if (IsCrossing(p1, p2, out Vector2 crossingPoint, out NoodleDirection outCrossing, out NoodleDirection inCrossing))
            {
                pointDatas.Add(new PointData{point = p1.point});
                pointDatas.Add(new PointData{point = crossingPoint});
                pointDatas.Add(new PointData{point = p2.point});

                p1.outDirection = outCrossing;
                p2.inDirection = inCrossing;

                return;
            }

            if (FindBestDirections(p1, p2, out NoodleDirection outDir, out NoodleDirection inDir))
                GenerateConnection(p1, p2, outDir, inDir, pointDatas);
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

        private bool FindBestDirections(PointDirectionalData current, PointDirectionalData other, out NoodleDirection outDir, out NoodleDirection inDir)
        {
            outDir = NoodleDirection.None;
            inDir = NoodleDirection.None;

            if ((current.validOutDirections & NoodleDirection.Left) != 0 && current.point.x >= other.point.x)
            {
                outDir = NoodleDirection.Left;

                if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    inDir = NoodleDirection.Right;
                else if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    inDir = NoodleDirection.Up;
                else if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    inDir = NoodleDirection.Down;
                else if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    inDir = NoodleDirection.Left;

                if (inDir != NoodleDirection.None)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Right) != 0 && current.point.x <= other.point.x)
            {
                outDir = NoodleDirection.Right;

                if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    inDir = NoodleDirection.Left;
                else if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    inDir = NoodleDirection.Up;
                else if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    inDir = NoodleDirection.Down;
                else if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    inDir = NoodleDirection.Right;

                if (inDir != NoodleDirection.None)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Up) != 0 && current.point.y <= other.point.y)
            {
                outDir = NoodleDirection.Up;

                if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    inDir = NoodleDirection.Down;
                else if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    inDir = NoodleDirection.Left;
                else if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    inDir = NoodleDirection.Right;
                else if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    inDir = NoodleDirection.Up;

                if (inDir != NoodleDirection.None)
                    return true;
            }
            if ((current.validOutDirections & NoodleDirection.Down) != 0 && current.point.y >= other.point.y)
            {
                outDir = NoodleDirection.Down;

                if ((other.validOutDirections & NoodleDirection.Up) != 0)
                    inDir = NoodleDirection.Up;
                else if ((other.validOutDirections & NoodleDirection.Left) != 0)
                    inDir = NoodleDirection.Left;
                else if ((other.validOutDirections & NoodleDirection.Right) != 0)
                    inDir = NoodleDirection.Right;
                else if ((other.validOutDirections & NoodleDirection.Down) != 0)
                    inDir = NoodleDirection.Down;

                if (inDir != NoodleDirection.None)
                    return true;
            }

            return false;
        }

        private void GenerateConnection(PointDirectionalData current, PointDirectionalData other, NoodleDirection outDir, NoodleDirection inDir, List<PointData> pointDatas)
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

            pointDatas.Add(new PointData{point = p1});
            pointDatas.Add(new PointData{point = p2});
            pointDatas.Add(new PointData{point = other.point});
        }
    }
}