using UnityEditor;
using UnityEngine;

namespace XNodeEditor
{
    public static class NoodleDrawerUtility
    {
        private static readonly Vector3[] polyLineTempArray = new Vector3[2];

        public static Vector2 CalculateBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float u = 1 - t;
            float tt = t * t, uu = u * u;
            float uuu = uu * u, ttt = tt * t;
            return new Vector2(
                (uuu * p0.x) + (3 * uu * t * p1.x) + (3 * u * tt * p2.x) + (ttt * p3.x),
                (uuu * p0.y) + (3 * uu * t * p1.y) + (3 * u * tt * p2.y) + (ttt * p3.y)
            );
        }

        /// <summary> Draws a line segment without allocating temporary arrays </summary>
        public static void DrawAAPolyLineNonAlloc(float thickness, Vector2 p0, Vector2 p1) {
            polyLineTempArray[0].x = p0.x;
            polyLineTempArray[0].y = p0.y;
            polyLineTempArray[1].x = p1.x;
            polyLineTempArray[1].y = p1.y;
            Handles.DrawAAPolyLine(thickness, polyLineTempArray);
        }
    }
}