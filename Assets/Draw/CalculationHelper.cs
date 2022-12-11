using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using GK;
using Unity.Barracuda;
using Newtonsoft.Json;
using System.Threading;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Providers.LinearAlgebra;
using UnityEditor;

public class CalculationHelper : MonoBehaviour
{

    public static Vector2 FindNearestPointOnLineSegment(Vector2 origin, Vector2 end, Vector2 point) {
        Vector2 heading = end - origin;
        float magnitudeMax = heading.magnitude;
        heading.Normalize(); // in place v.s. .normalized

        Vector2 lhs = point - origin;
        float dotP = Vector2.Dot(lhs, heading);
        dotP = Mathf.Clamp(dotP, 0f, magnitudeMax);
        return origin + heading * dotP;
    }

    public static Vector2 FindNearestPointOnLine(Vector2 pointOnLine, float slope, Vector2 point) {
        Vector2 direction = (new Vector2(1f, slope)).normalized;
        var v = point - pointOnLine;
        var d = Vector2.Dot(v, direction);
        return pointOnLine + direction * d;
    }

    // centerOffsetMultiplier: center + normal * centerOffsetMultiplier = newCenter
    public static float GetDepthAlongNormal(List<Vector3> points, Vector3 normal, out Vector3 front, out Vector3 back, out float centerOffsetMultiplier)
    {
        Vector3 center = GetCenter(points);
        Debug.Log("normal.magnitude = " + normal.magnitude);
        // Edge cases:
        front = Vector3.zero;
        back = Vector3.zero;
        centerOffsetMultiplier = 0f;
        if (points.Count == 0)
        {
            return -1;
        }
        if (points.Count == 1)
        {
            return 0;
        }
        
        float max = 0, min = 0;
        foreach (var point in points)
        {
            Vector3 v = point - center;
            float signedDistance = Vector3.Dot(normal, v) / normal.magnitude;
            if (signedDistance > max)
            {
                max = signedDistance;
                front = point;
            } else if (signedDistance < min)
            {
                min = signedDistance;
                back = point;
            }
        }

        centerOffsetMultiplier = (max + min) / 2f;
        return max - min;
    }

    /*
     * In: a direction in 2D (the slope), points
     * Out: A bounding box aligned with that direction (center, height, width)
     */
    public static void FindBoundingBox2D(List<Vector2> points, float slope, out Vector2 newCenter, out float height, out float width){
        
        float leftmost = 0f, 
              rightmost = 0f, 
              highest = 0f, 
              lowest = 0f;

        for (int i = 0; i < points.Count; ++i) {
            Vector2 closestPoint = FindNearestPointOnLine(Vector2.zero, slope, points[i]);
            Vector2 horizontalOffset = points[i] - closestPoint;
            float horizontalDistance = Mathf.Sign(horizontalOffset.x) * horizontalOffset.magnitude;
            float verticalDistance = Mathf.Sign(closestPoint.y) * closestPoint.magnitude;
            if (horizontalDistance > rightmost) {
                rightmost = horizontalDistance;
            } else if (horizontalDistance < leftmost) {
                leftmost = horizontalDistance;
            }
            if (verticalDistance > highest) {
                highest = verticalDistance;
            } else if (verticalDistance < lowest) {
                lowest = verticalDistance;
            }
        }
        Debug.Log("High: " + highest + " Low: " + lowest + " Left: " + leftmost + " Right: " + rightmost);
        newCenter = (highest + lowest) * 0.5f * new Vector2(1f, slope).normalized * Mathf.Sign(slope) // y has to be positive
                //    + 10f * new Vector2(1f, slope).normalized * Mathf.Sign(slope)
                  + (rightmost + leftmost) * 0.5f * new Vector2(slope, -1f).normalized * Mathf.Sign(slope) // x has to be positive
                //   + 10f * new Vector2(slope, -1f).normalized * Mathf.Sign(slope)
                ;
        height = highest - lowest;
        width = rightmost - leftmost;
    }

    public static List<(Vector2 start, Vector2 end)> GetLineSegments(List<List<Vector2>> strokes) {
        var lineSegments = new List<(Vector2 start, Vector2 end)>();
        foreach (List<Vector2> stroke in strokes) {
            for (int i = 0; i < stroke.Count - 1; ++i) {
                (Vector2 start, Vector2 end) lineSegment = (stroke[i], stroke[i+1]);
                lineSegments.Add(lineSegment);
            }
        }
        return lineSegments;
    }

    public static void GetBestFitLineByLinearRegression(List<Vector2> points, out float yIntercept, out float slope) {
        float sumOfX = 0;
        float sumOfY = 0;
        float sumOfXSq = 0;
        float sumOfYSq = 0;
        float sumCodeviates = 0;
        float count = points.Count;

        for (var i = 0; i < count; i++)
        {
            var x = points[i].x;
            var y = points[i].y;
            sumCodeviates += x * y;
            sumOfX += x;
            sumOfY += y;
            sumOfXSq += x * x;
            sumOfYSq += y * y;
        }

        var ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
        var ssY = sumOfYSq - ((sumOfY * sumOfY) / count);

        var meanX = sumOfX / count;
        var meanY = sumOfY / count;

        // var rNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
        var rDenom = (count * sumOfXSq - (sumOfX * sumOfX)) * (count * sumOfYSq - (sumOfY * sumOfY));
        var sCo = sumCodeviates - ((sumOfX * sumOfY) / count);
        // var dblR = rNumerator / Math.Sqrt(rDenom);
        // var rSquared = dblR * dblR;
        yIntercept = meanY - ((sCo / ssX) * meanX);
        slope = sCo / ssX;

    }

    public static void GetBestFitLineByBoundingLines(List<Vector2> points, ref List<Vector2> hullVerts2D, out int point1Index, out int point2Index, out float slope, out float depth) {
        print("GetBestFitLineByBoundingLines");
        print(points.Count);
        if (hullVerts2D.Count == 0)
            hullVerts2D = GetConvexHull2D(points);
        print(hullVerts2D.Count);
        GetBestFitLineByBoundingLinesHullReady(hullVerts2D, out point1Index, out point2Index, out slope, out depth);
    }

    public static void GetBestFitLineByBoundingLinesHullReady(List<Vector2> hullVerts2D, out int point1Index, out int point2Index, out float slope, out float depth) {
        float minDistance = float.PositiveInfinity;
        point1Index = 0;
        point2Index = 0;
        slope = 0;
        // NOTE: We are looking for the minimum distance of the maximum distances for an edge
        for (int i = 0; i < hullVerts2D.Count; ++i) { // FATAL: not i = i + 3!
            // TODO: Sometimes unreliable -> Find out why?
            Vector2 direction = (hullVerts2D[(i+1)%hullVerts2D.Count] - hullVerts2D[i]).normalized; // normalized!
            float maxDistanceFromEdge = 0;
            int farthestVert = 0;
            for (int j = 0; j < hullVerts2D.Count; ++j) {
                if (j == i || j == (i+1)%hullVerts2D.Count) continue;
                Vector2 p = hullVerts2D[j] - hullVerts2D[i];
                float distance = (p - Mathf.Abs(Vector2.Dot(p, direction)) * direction).magnitude; // sqrMagnitude would be faster than magnitude
                // Debug.Log(i + "->" + j + " distance: " + distance);
                if (maxDistanceFromEdge <= distance) {
                    maxDistanceFromEdge = distance;
                    farthestVert = j;
                }
            }
            // Debug.Log("max distance from edge " + i + ": " + maxDistanceFromEdge);
            if (maxDistanceFromEdge < minDistance) {
                minDistance = maxDistanceFromEdge;
                slope = direction.y / direction.x;
                point1Index = i;
                point2Index = farthestVert;
            }
        }
        depth = minDistance;
        // Debug.Log("min distance: " + minDistance);
    }

    public static List<Vector2> GetConvexHull2D(List<Vector2> points) {
        if(points.Count <= 1) {
            return points;
        }
        int count = points.Count;
        int k = 0;
        List<Vector2> hullVerts = new List<Vector2>(new Vector2[2 * count]);
        points.Sort((a, b) =>
            a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        // Build lower hull
        for (int i = 0; i < count; ++i)
        {
            while (k >= 2 && cross(hullVerts[k - 2], hullVerts[k - 1], points[i]) <= 0f)
                k--;
            hullVerts[k++] = points[i];
        }

        // Build upper hull
        for (int i = count - 2, t = k + 1; i >= 0; i--)
        {
            while (k >= t && cross(hullVerts[k - 2], hullVerts[k - 1], points[i]) <= 0f)
                k--;
            hullVerts[k++] = points[i];
        }

        return hullVerts.Take(k - 1).ToList();

    }

    public static double cross(Vector2 O, Vector2 A, Vector2 B)
    {
        return (A.x - O.x) * (B.y - O.y) - (A.y - O.y) * (B.x - O.x);
    }

    static private List<List<Vector2>> GetInnerStrokes(List<List<Vector2>> strokes) {
        
        // 1. trim line segments
        List<(Vector2 start, Vector2 end)> lineSegments = GetLineSegments(strokes);
        var lineSegmentsPolar = new List<(Vector2 start, Vector2 end)>();
        lineSegmentsPolar = (from seg in lineSegments select (CartesianToPolar(seg.start), CartesianToPolar(seg.end))).ToList();
        var hasOverlaps = Enumerable.Repeat(false, lineSegmentsPolar.Count).ToList();
        var toDelete = Enumerable.Repeat(false, lineSegmentsPolar.Count).ToList();
        // 2. Trim out inner segments
        for (int i = 0; i < lineSegmentsPolar.Count; ++i) {
            for (int j = i + 1; j < lineSegmentsPolar.Count; ++j) {
                if (lineSegmentsPolar[i].start != lineSegmentsPolar[j].end 
                    && lineSegmentsPolar[i].start != lineSegmentsPolar[j].start
                    && lineSegmentsPolar[i].end != lineSegmentsPolar[j].start
                    && lineSegmentsPolar[i].end != lineSegmentsPolar[j].end
                    && Intersects(PolarToCartesian(lineSegmentsPolar[i].start), PolarToCartesian(lineSegmentsPolar[i].end), PolarToCartesian(lineSegmentsPolar[j].start), PolarToCartesian(lineSegmentsPolar[j].end))){
                    // TODO: FIXME: Get intersection point
                    // Take the farthest point from each segment
                    hasOverlaps[i] = true;
                    hasOverlaps[j] = true;
                    Debug.Log("Intersect: " + i + " and " + j);
                    lineSegmentsPolar[i] = 
                    (
                        lineSegmentsPolar[i].start.x > lineSegmentsPolar[i].end.x ?
                        lineSegmentsPolar[i].start :
                        lineSegmentsPolar[i].end
                        ,
                        FindIntersection(
                            lineSegmentsPolar[i].start, 
                            lineSegmentsPolar[i].end, 
                            lineSegmentsPolar[j].start, 
                            lineSegmentsPolar[j].end
                        )
                    );
                    toDelete[j] = true;
                } else {
                    // Delete the outer segment
                    // FIXME: Adjust values?
                    Vector2 a = lineSegmentsPolar[i].start;
                    Vector2 b = lineSegmentsPolar[i].end;
                    Vector2 x = lineSegmentsPolar[j].start;
                    Vector2 y = lineSegmentsPolar[j].end;
                    if (OverlapsRadially(a, b, x, y)) {
                        // FIXME:
                        Debug.Log("Overlaps: " + i + " and " + j);
                        hasOverlaps[i] = true;
                        hasOverlaps[j] = true;
                        float d1 = FindNearestPointOnLineSegment(PolarToCartesian(a), PolarToCartesian(b), Vector2.zero).magnitude;
                        float d2 = FindNearestPointOnLineSegment(PolarToCartesian(x), PolarToCartesian(y), Vector2.zero).magnitude;
                        if (d1 >= d2) { // special case for == ?
                            toDelete[j] = true;
                        } else {
                            toDelete[i] = true;
                        }
                    }
                }
            }
        }

        for (int i = 0; i < hasOverlaps.Count; ++i) {
            if (!hasOverlaps[i]) {
                lineSegmentsPolar.RemoveAt(i);
                hasOverlaps.RemoveAt(i);
                toDelete.RemoveAt(i);
                i--;
            }
        }

        for (int i = 0; i < toDelete.Count; ++i) {
            if (toDelete[i]) {
                lineSegmentsPolar.RemoveAt(i);
                toDelete.RemoveAt(i);
                i--;
            }
        }
        
        // 2. Connect line segments into strokes
        List<List<Vector2>> strokesLeftPolar = new List<List<Vector2>>();
        if (lineSegmentsPolar.Count != 0) {
            strokesLeftPolar.Add(new List<Vector2>{lineSegmentsPolar[0].start, lineSegmentsPolar[0].end});
            for (int i = 1; i < lineSegmentsPolar.Count; ++i) {
                if (lineSegmentsPolar[i].start == strokesLeftPolar.Last().Last()) {
                    strokesLeftPolar.Last().Add(lineSegmentsPolar[i].end);
                } else {
                    strokesLeftPolar.Add(new List<Vector2>{lineSegmentsPolar[i].start, lineSegmentsPolar[i].end});
                }
            }
        }

        return (from stroke in strokesLeftPolar select (from point in stroke select PolarToCartesian(point)).ToList()).ToList();
    }

    public static Vector2 Vector2Average(List<Vector2> v) {
        Vector2 res = Vector2.zero;
        for (int i = 0; i < v.Count; ++i) {
            res += v[i];
        }
        res /= (float)v.Count;
        return res;
    }

    public static Vector3 Vector3Average(List<Vector3> v) {
        Vector3 res = Vector3.zero;
        for (int i = 0; i < v.Count; ++i) {
            res += v[i];
        }
        res /= (float)v.Count;
        return res;
    }

    public static List<Vector2> GetOutlinePolar(List<List<Vector2>> strokes) {
        // 1. Get Line Segments
        List<(Vector2 start, Vector2 end)> lineSegments = GetLineSegments(strokes);
        var lineSegmentsPolar = new List<(Vector2 start, Vector2 end)>();
        lineSegmentsPolar = (from seg in lineSegments select (CartesianToPolar(seg.start), CartesianToPolar(seg.end))).ToList();
        // 2. Trim out inner segments
        for (int i = 0; i < lineSegmentsPolar.Count; ++i) {
            for (int j = i + 1; j < lineSegmentsPolar.Count; ++j) {
                if (lineSegmentsPolar[i].start != lineSegmentsPolar[j].end 
                    && lineSegmentsPolar[i].start != lineSegmentsPolar[j].start
                    && lineSegmentsPolar[i].end != lineSegmentsPolar[j].start
                    && lineSegmentsPolar[i].end != lineSegmentsPolar[j].end
                    && Intersects(PolarToCartesian(lineSegmentsPolar[i].start), PolarToCartesian(lineSegmentsPolar[i].end), PolarToCartesian(lineSegmentsPolar[j].start), PolarToCartesian(lineSegmentsPolar[j].end))){
                    // TODO: FIXME: Get intersection point
                    // Take the farthest point from each segment
                    Debug.Log("Intersect: " + i + " and " + j);
                    lineSegmentsPolar[i] = 
                    (
                        lineSegmentsPolar[i].start.x > lineSegmentsPolar[i].end.x ?
                        lineSegmentsPolar[i].start :
                        lineSegmentsPolar[i].end
                        ,
                        FindIntersection(
                            lineSegmentsPolar[i].start, 
                            lineSegmentsPolar[i].end, 
                            lineSegmentsPolar[j].start, 
                            lineSegmentsPolar[j].end
                        )
                    );
                    // lineSegmentsPolar[i] = 
                    lineSegmentsPolar.RemoveAt(j);
                    j--;
                } else {
                    // Delete the inner segment
                    // FIXME: Adjust values?
                    Vector2 a = lineSegmentsPolar[i].start;
                    Vector2 b = lineSegmentsPolar[i].end;
                    Vector2 x = lineSegmentsPolar[j].start;
                    Vector2 y = lineSegmentsPolar[j].end;
                    // if (a.y < x.y && b.y < x.y && a.y < y.y && b.y < y.y) continue;
                    // if (a.y > x.y && b.y > x.y && a.y > y.y && b.y > y.y) continue;
                    if (OverlapsRadially(a, b, x, y)) {
                        // FIXME:
                        Debug.Log("Overlaps: " + i + " and " + j);
                        float d1 = FindNearestPointOnLineSegment(PolarToCartesian(a), PolarToCartesian(b), Vector2.zero).magnitude;
                        float d2 = FindNearestPointOnLineSegment(PolarToCartesian(x), PolarToCartesian(y), Vector2.zero).magnitude;
                        if (d1 >= d2) { // special case for == ?
                            lineSegmentsPolar.RemoveAt(j);
                            j--;
                        } else {
                            lineSegmentsPolar.RemoveAt(i);
                            j = i + 1;
                        }
                    }
                }
            }
        }
        // 3. Connect them
        List<Vector2> outlinePolar = new List<Vector2> ();
        if (lineSegmentsPolar.Count != 0) {
            outlinePolar = new List<Vector2> {lineSegmentsPolar[0].start, lineSegmentsPolar[0].end};
            foreach ((Vector2 start, Vector2 end) seg in lineSegmentsPolar.GetRange(0, lineSegmentsPolar.Count - 1)) {
                if (seg.start != outlinePolar.Last()) {
                    outlinePolar.Add(seg.start);
                    outlinePolar.Add(seg.end);
                }
                outlinePolar.Add(seg.end);
            }
        }
        // outlinePolar = outlinePolar.Distinct().OrderBy(point => point.y).ToList();
        outlinePolar = outlinePolar.OrderBy(point => point.y).ToList();

        return outlinePolar;
    }

    static bool OverlapsRadially(Vector2 a, Vector2 b, Vector2 x, Vector2 y) {
        // FIXME: check
        float p1Left, p1Right, p2Left, p2Right;
        if ((b.y - a.y + y.y - x.y) > (Mathf.PI * 2)) return true;
        return (x.y > b.y) ^ (y.y > a.y) ^ (b.y < a.y) ^ (y.y < x.y);

    }

    // colinear doesn't count
    public static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
        return ccw(a, c, d) != ccw(b, c, d) && ccw(a, b, c) != ccw(a, b, d);
    }

    public static bool ccw(Vector2 a, Vector2 b, Vector2 c) {
        return (c.y - a.y) * (b.x - a.x) > (b.y - a.y) * (c.x - a.x);
    }

    // Assumes intersects
    public static Vector2 FindIntersection(Vector2 a, Vector2 b, Vector2 x, Vector2 y) {
        var r = b - a;
        var s = y - x;
        var rxs = Vector3.Cross(r, s).magnitude;
        var t = Vector3.Cross((x - a), s).magnitude / rxs;
        var u = Vector3.Cross((x - a), r).magnitude / rxs;
        Debug.Log("a + t * r = " + (a + t * r));
        Debug.Log("x + t * s = " + (x + t * s));
        return a + t * r;
    }

    public static Vector2 CartesianToPolar(Vector2 point) {
        float angle = Mathf.Atan2(point.y, point.x) + Mathf.PI; // Mathf.Atan2(y, x)
        float distance = point.magnitude;
        return new Vector2 (distance, angle);
    }

    public static Vector2 PolarToCartesian(Vector2 point) {
        float x = point.x * Mathf.Cos(point.y - Mathf.PI);
        float y = point.x * Mathf.Sin(point.y - Mathf.PI);
        return new Vector2(x, y);
    }

    public static void DrawDebugLine(List<List<Vector2>> strokes, GameObject gameObject, LineRenderer strokeLR, bool loop = false, string name = null) {
        foreach (List<Vector2> stroke in strokes) {
            LineRenderer strokeOnCoord = 
                Instantiate (
                    strokeLR, 
                    gameObject.transform.position, 
                    gameObject.transform.rotation, 
                    gameObject.transform
                );
            if (name != null) {
                strokeOnCoord.name = name;
            }
            strokeOnCoord.positionCount = stroke.Count;
            strokeOnCoord.loop = loop;
            for (int i = 0; i < stroke.Count; ++i) {
                strokeOnCoord.SetPosition(i, stroke[i]);
            }
        }
    }

    public static Matrix4x4 GetTriMappingMatrix2D(Vector2 a, Vector2 b, Vector2 c, Vector2 x, Vector2 y, Vector2 z)
    {
        var A = new Matrix4x4();
        var B = new Matrix4x4();
        A.SetColumn(0, new Vector4(a.x, a.y, 1, 0));
        A.SetColumn(1, new Vector4(b.x, b.y, 1, 0));
        A.SetColumn(2, new Vector4(c.x, c.y, 1, 0));
        B.SetColumn(0, new Vector4(x.x, x.y, 1, 0));
        B.SetColumn(1, new Vector4(y.x, y.y, 1, 0));
        B.SetColumn(2, new Vector4(z.x, z.y, 1, 0));
        Debug.Log("A: \n" + A);
        var AI = new Matrix4x4();
        Matrix4x4.Inverse3DAffine(A, ref AI);
#if DEBUG
        Debug.Log("AI: \n" + AI);
        Debug.Log("B: \n" + B);
        var M = B * AI;
        Debug.Log("M: \n" + M);
#endif
        return B * AI;
    }

    public static Matrix4x4 GetTriMappingMatrix3D(Vector3 a, Vector3 b, Vector3 c, Vector3 x, Vector3 y, Vector3 z)
    {
        var A = new Matrix4x4();
        var B = new Matrix4x4();
        A.SetColumn(0, new Vector4(a.x, a.y, a.z, 0));
        A.SetColumn(1, new Vector4(b.x, b.y, b.z, 0));
        A.SetColumn(2, new Vector4(c.x, c.y, c.z, 0));
        B.SetColumn(0, new Vector4(x.x, x.y, x.z, 0));
        B.SetColumn(1, new Vector4(y.x, y.y, y.z, 0));
        B.SetColumn(2, new Vector4(z.x, z.y, z.z, 0));
        Debug.Log("A: \n" + A);
        var AI = new Matrix4x4();
        Matrix4x4.Inverse3DAffine(A, ref AI);
#if DEBUG
        Debug.Log("AI: \n" + AI);
        Debug.Log("B: \n" + B);
        var M = B * AI;
        Debug.Log("M: \n" + M);
#endif
        return B * AI;
    }

    public static bool Straddle(Vector2 a, Vector2 b, Vector2 origin, Vector2 direction) {
        return direction.normalized * Mathf.Sign(direction.x) == (a - origin).normalized * Mathf.Sign((a - origin).x)
               || direction.normalized * Mathf.Sign(direction.x) == (b - origin).normalized * Mathf.Sign((b - origin).x)
               || Vector3.Dot(Vector3.Cross(direction, a - origin), Vector3.Cross(direction, b - origin)) < 0f;
    }

    public static void TakeOutEdgePoints(List<Vector2> points) {
        if (points.Count < 2) return;
        float farthestDistance = 0;
        int idx1 = 0, idx2 = 1;
        for (int i = 0; i < points.Count; ++i) {
            for (int j = i+1; j < points.Count; ++j) {
                float distance = (points[i] - points[j]).magnitude;
                if (distance >= farthestDistance) {
                    farthestDistance = distance;
                    idx1 = i;
                    idx2 = j;
                }
            }
        }
        // WARNING: if you are going to remove two or more elements from the same list, remember that the indices might change after deleting one of the elements
        // In this case, idx2 is bigger than idx1, so idx2 should be deleteed first.
        points.RemoveAt(idx2);
        points.RemoveAt(idx1);
    }

    public static void TrimClosestPoints(int n, List<Vector2> points) {
        while (points.Count > n) {
            int idx = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < points.Count; ++i) {
                for (int j = i+1; j < points.Count; ++j) {
                    float distance = (points[i] - points[j]).magnitude;
                    if (distance <= closestDistance) {
                        closestDistance = distance;
                        idx = i;
                    }
                }
            }
            points.RemoveAt(idx);
        }
    }

    public static List<int> FindHollowSides(int n, List<Vector2> hullVerts, List<Vector2> points) {
        var midpoints = hullVerts.Select((point, index) => (point + hullVerts[(index+1)%hullVerts.Count]) / 2f).ToList();
        var distToClosestPoint = 
            midpoints.
            Select(
                point
                =>
                points.
                Aggregate(
                    float.PositiveInfinity,
                    (soFar, next)
                    =>
                    Vector2.Distance(point, next) < soFar ?
                    Vector2.Distance(point, next) :
                    soFar
                )
            ).ToList();
        List<int> hollowSides = new List<int>();
        while(n > 0) {
            float min =
                distToClosestPoint.
                Aggregate (
                    (soFar, next)
                    =>
                    next > soFar ?
                    next :
                    soFar
                );
            int idx = distToClosestPoint.IndexOf(min);
            hollowSides.Add(idx);
            distToClosestPoint[idx] = 0f;
            n--;
        }
        return hollowSides;
    }

    public static void Trim2DConvexHull(int n, List<Vector2> convexHull) {
        // LOG: Change policy from "average" to "furtherst away from center"
        int idx = 0;
        while (convexHull.Count > n) {
            // delete one element
            float smallestDistance = float.PositiveInfinity;
            for (int i = 0; i < convexHull.Count; ++i) {
                if (Vector2.Distance(convexHull[i], convexHull[(i+1)%convexHull.Count]) < smallestDistance) {
                    idx = i;
                    smallestDistance = Vector2.Distance(convexHull[i], convexHull[(i+1)%convexHull.Count]);
                }
            }
            if (convexHull[idx].magnitude > convexHull[(idx+1)%convexHull.Count].magnitude) {
                Debug.Log("Delete One Vertex: " + convexHull[(idx+1)%convexHull.Count]);
                convexHull.RemoveAt((idx+1)%convexHull.Count);
            } else {
                Debug.Log("Delete One Vertex: " + convexHull[idx]);
                convexHull.RemoveAt(idx);
            }
        }
    }

    public static void Trim2DConvexHullByAngle(int n, List<Vector2> points) {
        // FIXME: Not always Accurate
        List<float> angles = new List<float>(new float[points.Count]);
        for (int i = 0; i < points.Count; ++i) {
            Vector2 prev = points[(i-1+points.Count)%points.Count];
            Vector2 next = points[(i+1)%points.Count];
            Vector2 current = points[i];
            Vector2 left = prev - current;
            Vector2 right = next - current;
            angles[i] = Mathf.Acos(Vector2.Dot(left, right) / (left.magnitude * right.magnitude));
        }
        while (points.Count > n) {
            float max = angles.Aggregate((maxAngleSoFar, nextAngle) => nextAngle > maxAngleSoFar ? nextAngle : maxAngleSoFar);
            int idx = angles.IndexOf(max);
            points.RemoveAt(idx);
            angles.RemoveAt(idx);
            Vector2 prev = points[(idx-1+points.Count)%points.Count];
            Vector2 next = points[(idx+1)%points.Count];
            Vector2 current = points[idx%points.Count];
            Vector2 left = prev - current;
            Vector2 right = next - current;
            float oldAngle = angles[idx%points.Count];
            angles[idx%points.Count] = Mathf.Acos(Vector2.Dot(left, right) / (left.magnitude * right.magnitude));
            angles[(idx+1)%points.Count] -= Mathf.PI - oldAngle + angles[idx%points.Count] - max;
        }
    }

    public static Vector2 Vector2OutlineCenter(List<Vector2> v) {
        if (v.Count == 1) {
            return v[0];
        }
        if (v.Count == 2) {
            return (v[0] + v[1]) * 0.5f;
        }
        v.Add(v[0]);
        Vector2 average = Vector2.zero;
        float circumference = 0f;
        for (int i = 0; i < v.Count - 1; ++i) {
            Vector2 midpoint = (v[i] + v[i+1]) / 2f;
            float length = (v[i] - v[i+1]).magnitude;
            average += midpoint * length;
            circumference += length;
        }
        v.RemoveAt(v.Count - 1);
        average /= circumference;
        return average;
    }

    public static Vector3 Vector3SurfaceAreaCentroid(List<Vector3> verts, List<int> tris, Vector3 center) {

        if (tris.Count == 0) {
            Debug.Log("No tris");
            return center;
        }

        List<Vector3> trisCenters = new List<Vector3>(new Vector3[tris.Count/3]);
        List<float> areas = new List<float>(new float[tris.Count/3]);

        for (int i = 0; i < tris.Count; i = i + 3) {
            trisCenters[i/3] = (verts[tris[i]] + verts[tris[i+1]] + verts[tris[i+2]])/3f;
            areas[i/3] = Vector3.Cross(verts[tris[i]] - verts[tris[i+1]], verts[tris[i+2]] - verts[tris[i+1]]).magnitude / 2f;
        }
        Vector3 soFar = Vector3.zero;
        float totalArea = 0;
        for (int i = 0; i < trisCenters.Count; ++i) {
            totalArea += areas[i];
            soFar += trisCenters[i] * areas[i];
        }
        Vector3 centroid = soFar / totalArea;
        
        return centroid;
    }

    public static Vector3 GetCenter(List<Vector3> points) {
        return points.Aggregate(Vector3.zero, (p, n) => p + n) / (float) points.Count;
    }

    // http://paulbourke.net/geometry/polygonmesh/centroid.pdf
    public static Vector2 Vector2Centroid(List<Vector2> v) {
        if (v.Count == 1) {
            return v[0];
        }
        if (v.Count == 2) {
            return (v[0] + v[1]) * 0.5f;
        }
        v.Add(v[0]); // wrap around
        float area = 0f;
        for (int i = 0; i < v.Count - 1; ++i) {
            area += v[i].x * v[i+1].y - v[i+1].x * v[i].y;
        }
        area /= 2f;

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < v.Count - 1; ++i) {
            centroid += 
                new Vector2 (v[i].x + v[i+1].x, v[i].y + v[i+1].y)
                * (v[i].x * v[i+1].y - v[i+1].x * v[i].y);
        }
        centroid /= (6f * area);

        v.RemoveAt(v.Count - 1);
        return centroid;
    }

}
