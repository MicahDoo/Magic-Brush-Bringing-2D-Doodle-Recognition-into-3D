using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using GK;
using Unity.Barracuda;
using Newtonsoft.Json;
using System.Threading;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Providers.LinearAlgebra;

public class ObjectGenerator : MonoBehaviour
{
    public DrawManager drawManager;

    [Serializable]
    public struct Object
    {
        public string name;
        public GameObject[] gameObjects;
    }

    public Object[] objectList;
    public GameObject ladderPrefab;

    private SketchProcessor sketchProcessor;


    void Awake()
    {
    }

    void Start()
    {

    }

    void Update()
    {
        // Debug.Log(drawManager);
        // Debug.Log(helper);
    }

    public IEnumerator GenerateObject(string objectLabel, SketchProcessor sketchProcessor)
    {
        yield return null;
        this.sketchProcessor = sketchProcessor;
        print("GenerateObject");
        Dictionary<string, ProjectionPlane> planes = sketchProcessor.planes;
        switch (objectLabel)
        {
            case "star":
                StartCoroutine(GenerateStar(planes["BoundingPlanes"].strokesOn2DCoords,
                    planes["BoundingPlanes"].pointsOnCoord, planes["BoundingPlanes"].matTo3D,
                    planes["BoundingPlanes"].normal));
                break;
            case "umbrella":
                GenerateUmbrella(planes["BoundingPlanes"].pointsOnCoord, planes["BoundingPlanes"].matTo3D,
                    planes["BoundingPlanes"].normal, sketchProcessor.flat);
                break;
            case "traffic_light":
                break;
            case "baseball_bat":
                GenerateBaseballBat(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            case "basketball":
                // bool flat = true;
                // GenerateBasketball(strokesOnCoordBP, pointsOnCoordBoundingPlanes, planes["BoundingPlanes"].matTo3D, normalBoundingPlanes);
                break;
            case "apple":
                GenerateApple(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            case "sword":
                GenerateSword(planes["BoundingPlanes"].pointsOnCoord, planes["BoundingPlanes"].matTo3D,
                    planes["BoundingPlanes"].normal);
                break;
            case "baseball":
                // bool flat = true;
                GenerateBaseball(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            case "triangle":
                GenerateTriangle(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            case "square":
                break;
            case "pizza":
                break;
            case "table":
                break;
            case "ladder":
                GenerateLadder(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            case "chair":
                GenerateChair(sketchProcessor.points, planes["BoundingPlanes"].strokesOn2DCoords,
                    planes["BoundingPlanes"].pointsOnCoord, planes["BoundingPlanes"].matTo3D,
                    planes["BoundingPlanes"].normal, sketchProcessor.flat);
                break;
            case "laptop":
                GenerateLaptop(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal, sketchProcessor.flat);
                break;
            case "eyeglasses":
                break;
            case "car":
                break;
            case "axe":
                break;
            case "spoon":
                break;
            case "circle":
                break;
            case "hook":
                GenerateHook(planes["BoundingPlanes"].strokesOn2DCoords, planes["BoundingPlanes"].pointsOnCoord,
                    planes["BoundingPlanes"].matTo3D, planes["BoundingPlanes"].normal);
                break;
            default:
                Debug.Log("default");
                break;
        }

        Debug.Log("Object Generatoin Complete, return method.");
    }


    // FIXME:
    private void GenerateLadder(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D,
            Vector3 normalBoundingPlanes)
        {
            CalculationHelper.GetBestFitLineByBoundingLinesHullReady(sketchProcessor.hullVerts2D,
                out drawManager.point1Index, out drawManager.point2Index, out drawManager.slope,
                out drawManager.BoundingLinesDistance2D);
            Debug.Log("BoundingLinesDistance2D " + drawManager.BoundingLinesDistance2D);
            Vector2 uprightDir = new Vector2(1f, drawManager.slope).normalized;
            Vector2 newCenter2D;
            float height, width;
            CalculationHelper.FindBoundingBox2D(sketchProcessor.hullVerts2D, drawManager.slope, out newCenter2D,
                out height, out width);
            Debug.Log("Width " + width);

            Vector2 lowerBoundMid = newCenter2D - uprightDir * height / 2f;
            Vector2 upperBoundMid = newCenter2D + uprightDir * height / 2f;

            List<(Vector2 start, Vector2 end)> lineSegments = CalculationHelper.GetLineSegments(strokes);
            List<Vector2> intersections =
            (
                from seg in lineSegments
                where CalculationHelper.Intersects(seg.start, seg.end, lowerBoundMid, upperBoundMid)
                select CalculationHelper.FindIntersection(seg.start, seg.end, lowerBoundMid, upperBoundMid)
            ).ToList();
            intersections = intersections.Distinct().ToList();
            List<float> offsets = (from intersection in intersections
                    select Vector2.Distance(newCenter2D, intersection) *
                           Mathf.Sign(Vector2.Dot(newCenter2D, intersection)))
                .ToList();
            // Assume rail first then rung
            Debug.Log("objectList[10].gameObjects.Length " + objectList[10].gameObjects.Length);
            // GameObject prefab = Instantiate(objectList[10].gameObjects[UnityEngine.Random.Range(0,objectList[10].gameObjects.Length)]);
            GameObject prefab = Instantiate(ladderPrefab);
            GameObject rail = prefab.transform.GetChild(0).gameObject; // x length, z width, y depth
            GameObject rung = prefab.transform.GetChild(1).gameObject; // z length, x y thickness
            float heightRatio = height
                                / rail.GetComponentsInChildren<MeshFilter>()[0].mesh.bounds.extents.x
                                / 2f;
            float widthRatio = width
                               / rung.GetComponentsInChildren<MeshFilter>()[0].mesh.bounds.extents.x
                               / 2f;
            rail.transform.localScale = new Vector3(
                heightRatio,
                rail.transform.localScale.y * heightRatio,
                rail.transform.localScale.z * heightRatio
            );
            rail.transform.localPosition = Vector3.up * width / 2f;
            GameObject secondRail = Instantiate(rail, prefab.transform);
            secondRail.transform.localPosition = -Vector3.up * width / 2f;
            rung.transform.localScale = new Vector3(
                widthRatio,
                rung.transform.localScale.y * widthRatio,
                rung.transform.localScale.z * widthRatio
            );
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            rung.transform.localPosition = -Vector3.left * offsets[0];
            for (int i = 1; i < offsets.Count; ++i)
            {
                GameObject newRung = Instantiate(rung, prefab.transform);
                newRung.transform.localPosition = -Vector3.left * offsets[i];
            }

            Vector3 uprightDir3D = matTo3D.MultiplyVector(uprightDir);
            Vector3 sideDir3D = matTo3D.MultiplyVector(new Vector3(drawManager.slope, -1f));
            prefab.transform.rotation =
                Quaternion.LookRotation(
                    Vector3.Cross(uprightDir3D, sideDir3D),
                    sideDir3D
                );
            prefab.transform.position = matTo3D.MultiplyPoint3x4(newCenter2D);
        }

        private void GenerateBaseballBat(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D,
            Vector3 normal)
        {
        }

        private void GenerateBaseball(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D,
            Vector3 normal)
        {

            // 1. Find perpendicular direction by closest point.
            Vector2 closestPointToCenter = points.Aggregate((p, n) => p.magnitude > n.magnitude ? n : p);
            Vector2 perpendicularDir = Vector2.zero - closestPointToCenter;

            CalculationHelper.DrawDebugLine(
                new List<List<Vector2>> { new List<Vector2> { Vector2.zero, closestPointToCenter } },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true, "ClosestPoint");

            // 2. Find four points along that line. (Two intersecting the outline, two intersection the inner lines)
            List<Vector2> pointsAlongPerp = new List<Vector2>();
            foreach (List<Vector2> stroke in strokes)
            {
                pointsAlongPerp.AddRange(
                    stroke.Take(stroke.Count - 1).Where(
                        (point, index)
                            => CalculationHelper.Straddle(point, stroke[index + 1], Vector2.zero, perpendicularDir)
                    ).ToList()
                );
            }

            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { pointsAlongPerp },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlue, true, "AllPoints");

            // 3. Trim to 4 points if more than 4 points
            CalculationHelper.TrimClosestPoints(4, pointsAlongPerp);
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { pointsAlongPerp },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlue, true, "FourPoints");

            // 4. Take out the outer points (leave the inner points)
            CalculationHelper.TakeOutEdgePoints(pointsAlongPerp);
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { pointsAlongPerp },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true, "TwoPoints");

            // 5. New center, rotation (calculate new center with the inner points)
            Vector2 innerCenter = (pointsAlongPerp[0] + pointsAlongPerp[1]) / 2f;
            Vector2 uprightDir2D = new Vector2(perpendicularDir.y, -perpendicularDir.x);
            uprightDir2D *= Mathf.Sign(Vector3.Dot(uprightDir2D, new Vector3(0f, 0f, 1f)));
            Vector2 newCenter2D;
            float height, width;
            CalculationHelper.FindBoundingBox2D(points, uprightDir2D.y / uprightDir2D.x, out newCenter2D, out height,
                out width);
            float rotationAngle = Mathf.Rad2Deg *
                                  Mathf.Atan2(Vector3.Project((innerCenter - newCenter2D), innerCenter).magnitude,
                                      width);
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            GameObject newGameObject =
                Instantiate(
                    objectList[4].gameObjects[UnityEngine.Random.Range(0, objectList[4].gameObjects.Length)],
                    newCenter3D,
                    Quaternion.LookRotation(normal, matTo3D.MultiplyVector(uprightDir2D)),
                    null // TODO: decide parent
                );
            newGameObject.transform.RotateAround(newGameObject.transform.position, matTo3D.MultiplyVector(uprightDir2D),
                rotationAngle);
            newGameObject.transform.localScale =
                new Vector3(
                    width
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.x)
                    / 2.0f,
                    height
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.y)
                    / 2.0f,
                    width
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.z)
                    / 2.0f
                );


            /* 
            List<List<Vector2>> innerStrokes = CalculationHelper.GetInnerStrokes(strokes);
            Debug.Log("There are " + innerStrokes.Count + " inner strokes");
            Debug.Assert(strokes.Count == 2);
            CalculationHelper.DrawDebugLine(innerStrokes, coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true, "InnerStrokes");
            Vector2 direction = GetAverageDirection2D(innerStrokes);
            direction *=  Mathf.Sign(direction.y);
            // It feels like it would be inaccurate because surely if the strokes are leaning right, the left stroke would be far longer
            // Vector2 innerStrokeCenter = GetCentroid(strokes.Aggregate(new List<Vector2>(), (p, n) => p.AddRange(n)));
            // Vector2 centroidHull = Vector2Centroid(sketchProcessor.hullVerts2D);
            Vector2 perp = Vector2.Perpendicular(direction);
            perp *= Mathf.Sign(perp.x);
            Vector2 newCenter2D;
            float height, width;
            CalculationHelper.FindBoundingBox(points, direction.y / direction.x, out newCenter2D, out height, out width);
            List<Vector2> midpoints = new List<Vector2>();
            foreach(List<Vector2> stroke in innerStrokes) {
                for (int i = 0; i < stroke.Count - 1; ++i) {
                    if (CalculationHelper.Straddle(stroke[i], stroke[i+1], newCenter2D, perp)) {
                        midpoints.Add(CalculationHelper.FindIntersection(stroke[i], stroke[i+1], newCenter2D, perp));
                        break;
                    }
                }
            }
            Debug.Log("There are " + midpoints.Count + " midpoints");
            Debug.Assert(midpoints.Count == 2);
            CalculationHelper.DrawDebugLine(new List<List<Vector2>>{midpoints}, coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true, "Midpoints");
            CalculationHelper.Trim2DConvexHull(2, midpoints);
    
            Vector2 innerCenter = midpoints.Count > 0 ? (midpoints.Aggregate((p, n)=>p+n) / (float) midpoints.Count) : newCenter2D;
            float rotationAngle = Mathf.Rad2Deg * Mathf.Atan2((innerCenter - newCenter2D).magnitude, width);
    
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            GameObject newGameObject = 
                Instantiate(
                    objectList[4].gameObjects[UnityEngine.Random.Range(0,objectList[4].gameObjects.Length)], 
                    newCenter3D, 
                    Quaternion.LookRotation(normal, matTo3D.MultiplyVector(direction)),
                    null // TODO: decide parent
                );
            newGameObject.transform.RotateAround(newGameObject.transform.position, matTo3D.MultiplyVector(direction), rotationAngle);
            newGameObject.transform.localScale = 
                new Vector3(
                    width
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.x)
                    / 2.0f,
                    height
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.y)
                    / 2.0f,
                    width
                    / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.z)
                    / 2.0f
                );*/




        }

        private void GenerateLaptop(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D,
            Vector3 normalBoundingPlanes, bool flat)
        {

        }

        private void GenerateTriangle(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D,
            Vector3 normalBoundingPlanes)
        {
        }

        private void GenerateHook(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D, Vector3 normal)
        {
            Vector2 outlineCenter = CalculationHelper.Vector2OutlineCenter(sketchProcessor.hullVerts2D);
            Vector2 centroid = CalculationHelper.Vector2Centroid(sketchProcessor.hullVerts2D);
            Vector2 uprightDir2D = (centroid - outlineCenter).normalized;
            // TODO: Unreliable way to find hook direction
            float hookDir = -Mathf.Sign(Vector3.Dot(Vector3.Cross(uprightDir2D, Vector2.zero - centroid),
                new Vector3(0f, 0f, 1f)));
            Vector2 newCenter2D;
            float height, width;
            CalculationHelper.FindBoundingBox2D(points, uprightDir2D.y / uprightDir2D.x, out newCenter2D, out height,
                out width);
            // newCenter2D = newCenter2D + uprightDir2D * (height/2f - width*1f);
            newCenter2D = newCenter2D - uprightDir2D * height / 2f;
            float handleLength = height - width;
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            Vector3 uprightDir3D = matTo3D.MultiplyVector(uprightDir2D);
            GameObject newGameObject =
                Instantiate(objectList[8].gameObjects[UnityEngine.Random.Range(0, objectList[8].gameObjects.Length)],
                    newCenter3D,
                    Quaternion.LookRotation(normal * hookDir, uprightDir3D),
                    null); // TODO: decide parent
            Mesh hookMesh = newGameObject.GetComponentsInChildren<MeshFilter>()[0].mesh;
            Transform hookTransform = newGameObject.transform.GetChild(0).GetChild(0);
            Mesh handleMesh = newGameObject.GetComponentsInChildren<MeshFilter>()[1].mesh;
            Transform handleTransform = newGameObject.transform.GetChild(0).GetChild(1);
            hookTransform.localScale =
                new Vector3(
                    width
                    / hookMesh.bounds.extents.x
                    / 2.0f,
                    width
                    / hookMesh.bounds.extents.z
                    / 2.0f,
                    width
                    / hookMesh.bounds.extents.z
                    / 2.0f
                );
            handleTransform.localScale =
                new Vector3(
                    handleLength
                    / handleMesh.bounds.extents.x
                    / 2.0f,
                    width
                    / hookMesh.bounds.extents.z
                    / 2.0f,
                    width
                    / hookMesh.bounds.extents.z
                    / 2.0f
                );
            newGameObject.transform.GetChild(0).localPosition = new Vector3(0f, 0.26f, 0f) * handleLength
                                                                / handleMesh.bounds.extents.x
                                                                / 2.0f;
            Debug.Log("Hook: " + newGameObject.transform.GetChild(0).localPosition);
        }

        // Object Generation Methods

        private void GenerateChair(List<Vector3> pointsIn3D, List<List<Vector2>> strokes, List<Vector2> points,
            Matrix4x4 matTo3D, Vector3 normal, bool flat)
        {
            // NOTE: chair: 
            // if (!flat) {
            //     Debug.Log("Non-flat Chair");
            //     return;
            // }   
            CalculationHelper.Trim2DConvexHullByAngle(6, sketchProcessor.hullVerts2D);
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { sketchProcessor.hullVerts2D },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true, "ChairOutline");
            CalculationHelper.GetBestFitLineByBoundingLinesHullReady(sketchProcessor.hullVerts2D,
                out drawManager.point1Index, out drawManager.point2Index, out drawManager.slope,
                out drawManager.BoundingLinesDistance2D);
            if (sketchProcessor.hullVerts2D.Count == 6)
            {
                bool frontView = true;
                var hollowSides = CalculationHelper.FindHollowSides(3, sketchProcessor.hullVerts2D, points)
                    .OrderBy(p => p).ToList();
                CalculationHelper.DrawDebugLine(
                    new List<List<Vector2>>
                    {
                        new List<Vector2>
                        {
                            sketchProcessor.hullVerts2D[hollowSides[0]],
                            sketchProcessor.hullVerts2D[(hollowSides[0] + 1) % 6]
                        },
                        new List<Vector2>
                        {
                            sketchProcessor.hullVerts2D[hollowSides[1]],
                            sketchProcessor.hullVerts2D[(hollowSides[1] + 1) % 6]
                        },
                        new List<Vector2>
                        {
                            sketchProcessor.hullVerts2D[hollowSides[2]],
                            sketchProcessor.hullVerts2D[(hollowSides[2] + 1) % 6]
                        }
                    },
                    drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true, "Hollow Sides"
                );
                // var parallelPairs = FindParallelPairs(2, sketchProcessor.hullVerts2D);
                int bottom = 0;
                bool counterClockwise = false;
                for (int i = 0; i < hollowSides.Count; ++i)
                {
                    if ((hollowSides[i] + 1) % 6 == hollowSides[(i + 1) % 3])
                    {
                        frontView = false;
                        bottom = hollowSides[i];
                        if ((hollowSides[(i + 2) % 3] + 2) % 6 == bottom)
                        {
                            counterClockwise = true;
                        }

                        continue;
                    }
                }

                var o = new int[6]; // order
                Vector2 uprightDir = Vector2.one;
                if (!frontView)
                {
                    Debug.Log("Not Front View");
                    if (counterClockwise)
                    {
                        for (int i = 0; i < 6; ++i)
                        {
                            o[i] = bottom;
                            bottom = (bottom + 1) % 6;
                        }
                    }
                    else
                    {
                        bottom = (bottom + 2) % 6;
                        for (int i = 0; i < 6; ++i)
                        {
                            o[i] = bottom;
                            bottom = (bottom + 5) % 6;
                        }
                    }

                    CalculationHelper.DrawDebugLine(
                        new List<List<Vector2>>
                        {
                            (from idx in o select sketchProcessor.hullVerts2D[idx]).ToList()
                        },
                        drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true,
                        "ChairOutlineOrdered"
                    );
                    uprightDir =
                        (((sketchProcessor.hullVerts2D[o[3]] - sketchProcessor.hullVerts2D[o[2]]) +
                          (sketchProcessor.hullVerts2D[o[5]] - sketchProcessor.hullVerts2D[o[0]])) / 2f).normalized;
                    // float height = ?
                    // FIXME: What if the angle is already < 90 deg
                    Vector2 LegPair1 = sketchProcessor.hullVerts2D[o[0]] - sketchProcessor.hullVerts2D[o[1]];
                    Vector2 LegPair2 = sketchProcessor.hullVerts2D[o[2]] - sketchProcessor.hullVerts2D[o[1]];
                    float angle1 = Mathf.Acos(Vector2.Dot(uprightDir, LegPair1) /
                                              (uprightDir.magnitude * LegPair1.magnitude));
                    float angle2 = Mathf.Acos(Vector2.Dot(uprightDir, LegPair2) /
                                              (uprightDir.magnitude * LegPair2.magnitude));
                    float x = LegPair1.magnitude * Mathf.Sin(angle1);
                    float y = LegPair2.magnitude * Mathf.Sin(angle2);
                    float a = LegPair1.magnitude * Mathf.Cos(angle1);
                    float b = LegPair2.magnitude * Mathf.Cos(angle2);
                    // xy = na * nb
                    float n = Mathf.Sqrt(x * y / (a * b));
                    float horizontalRot =
                        Mathf.Rad2Deg * (counterClockwise ? Mathf.Atan2(n * a, x) : -Mathf.Atan2(n * a, x));
                    float verticalRot = Mathf.Rad2Deg * -Mathf.Asin(1f / n);
                    Vector2 newCenter2D = sketchProcessor.hullVerts2D[o[1]] +
                                          Vector2.Dot(
                                              (sketchProcessor.hullVerts2D[o[5]] - sketchProcessor.hullVerts2D[o[0]]),
                                              uprightDir) * uprightDir +
                                          (sketchProcessor.hullVerts2D[o[0]] - sketchProcessor.hullVerts2D[o[1]]) / 2f +
                                          (sketchProcessor.hullVerts2D[o[2]] - sketchProcessor.hullVerts2D[o[1]]) / 2f;
                    CalculationHelper.DrawDebugLine(
                        new List<List<Vector2>>
                        {
                            new List<Vector2> { newCenter2D, newCenter2D + uprightDir },
                        },
                        drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true,
                        "Upright Direction"
                    );
                    // float width = counterClockwise ? Mathf.Sqrt(n * n * a * a + x * x) : Mathf.Sqrt(n * n * b * b + y * y);
                    float width = Mathf.Sqrt(n * n * a * a + x * x);
                    // float depth = counterClockwise ? Mathf.Sqrt(n * n * b * b + y * y) : Mathf.Sqrt(n * n * a * a + x * x);
                    float depth = Mathf.Sqrt(n * n * b * b + y * y);
                    float height =
                        Vector2.Dot((sketchProcessor.hullVerts2D[o[3]] - sketchProcessor.hullVerts2D[o[2]]),
                            uprightDir) / Mathf.Cos(Mathf.Deg2Rad * verticalRot);
                    Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
                    Vector3 uprightDir3D = matTo3D.MultiplyVector(uprightDir);
                    GameObject newGameObject = Instantiate(
                        objectList[7].gameObjects[UnityEngine.Random.Range(0, objectList[7].gameObjects.Length)],
                        newCenter3D,
                        Quaternion.LookRotation(normal, uprightDir3D),
                        null); // TODO: decide parent
                    Mesh seatMesh = newGameObject.GetComponentsInChildren<MeshFilter>()[1].mesh;
                    Transform seatTransform = newGameObject.transform.GetChild(1);
                    Mesh legMesh = newGameObject.GetComponentsInChildren<MeshFilter>()[0].mesh;
                    Transform legTransform = newGameObject.transform.GetChild(0);
                    newGameObject.transform.localScale =
                        new Vector3(
                            width
                            / (seatMesh.bounds.extents.x * seatTransform.localScale.x)
                            / 2.0f
                            ,
                            height
                            / (
                                seatMesh.bounds.extents.z * seatTransform.localScale.z
                                + legMesh.bounds.extents.z * legTransform.localScale.z
                            )
                            / 2.0f,
                            depth
                            / (seatMesh.bounds.extents.y * seatTransform.localScale.y)
                            / 2.0f
                        );
                    newGameObject.transform.RotateAround(newGameObject.transform.position, uprightDir3D, horizontalRot);
                    newGameObject.transform.RotateAround(newGameObject.transform.position,
                        Vector3.Cross(normal, uprightDir3D), verticalRot);
                }
                // for (int i = 0; i < parallelPairs.Count; ++i) {
                //     if ((hollowSides[i]+1)%6 + 1 == hollowSides[(i+1)%3] && hollowSides[(i+1)%3] + 1 == hollowSides[(i+2)%3]) {
                //         frontView = false;
                //         bottom = hollowSides[i];
                //     }
                // }
            }
            else
            {
                CalculationHelper.Trim2DConvexHullByAngle(4, sketchProcessor.hullVerts2D);
                // var parallelPairs = FindParallelPairs(1, sketchProcessor.hullVerts2D);
                // Determine upright direction and back direction
                // draw a line in the middle to find two intersecting points
            }
        }

        private void GenerateSword(List<Vector2> points, Matrix4x4 matTo3D, Vector3 normal)
        {
            CalculationHelper.GetBestFitLineByLinearRegression(points, out drawManager.yIntercept,
                out drawManager.slope2DSVD);
            Vector2 outlineCenter =
                CalculationHelper.Vector2OutlineCenter(sketchProcessor.hullVerts2D); // TODO: maybe use outline center?
            Vector2 uprightDir2D = outlineCenter - Vector2.zero;
            float height, width;
            Vector2 newCenter2D;
            CalculationHelper.FindBoundingBox2D(sketchProcessor.hullVerts2D, uprightDir2D.y / uprightDir2D.x,
                out newCenter2D, out height, out width);
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            GameObject newGameObject =
                Instantiate(
                    objectList[6].gameObjects[UnityEngine.Random.Range(0, objectList[6].gameObjects.Length)],
                    newCenter3D,
                    Quaternion.LookRotation(normal, matTo3D.MultiplyVector(uprightDir2D)),
                    null // TODO: decide parent
                );
            newGameObject.transform.localScale =
                new Vector3(
                    width / 0.122f / 2f,
                    height / 0.73f / 2f,
                    width / 0.122f / 2f
                );
        }

        IEnumerator GenerateStar(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D, Vector3 normal)
        {
            // TODO: enable editing meshes and trimming hull verts (if > 5) to achieve a finer tuned
            print("Generate Star");
            CalculationHelper.Trim2DConvexHull(5, sketchProcessor.hullVerts2D);
            // DEBUG
            CalculationHelper.GetBestFitLineByBoundingLinesHullReady(sketchProcessor.hullVerts2D,
                out drawManager.point1Index, out drawManager.point2Index, out drawManager.slope,
                out drawManager.BoundingLinesDistance2D);

            // Find Center
            Vector2 newCenter2D = CalculationHelper.Vector2Average(sketchProcessor.hullVerts2D);

            float minDistance = float.PositiveInfinity;
            int leftSpikeIndex = 0;
            for (int i = 0; i < sketchProcessor.hullVerts2D.Count; ++i)
            {
                if (Vector2.Distance(sketchProcessor.hullVerts2D[i],
                        sketchProcessor.hullVerts2D[(i + 2) % sketchProcessor.hullVerts2D.Count]) < minDistance)
                {
                    leftSpikeIndex = i;
                    minDistance = Vector2.Distance(sketchProcessor.hullVerts2D[i],
                        sketchProcessor.hullVerts2D[(i + 2) % sketchProcessor.hullVerts2D.Count]);
                }
            }

            int topSpikeIndex = (leftSpikeIndex + 1) % sketchProcessor.hullVerts2D.Count;
            Vector2 topDirection = sketchProcessor.hullVerts2D[topSpikeIndex] - newCenter2D;
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            // Debug.Log(matTo3D.MultiplyVector(topDirection));
            // Debug.Log(matTo3D.MultiplyPoint3x4(topDirection + newCenter2D) - matTo3D.MultiplyPoint3x4(newCenter2D));
            // Instantiate(drawManager.referencePoint, matTo3D.MultiplyPoint3x4(newCenter2D), Quaternion.identity, null);
            // Instantiate(drawManager.referencePoint, matTo3D.MultiplyPoint3x4(newCenter2D + topDirection), Quaternion.identity, null);

            GameObject starGameObject = Instantiate(
                objectList[0].gameObjects[UnityEngine.Random.Range(0, objectList[0].gameObjects.Length)],
                newCenter3D,
                Quaternion.LookRotation(matTo3D.MultiplyVector(topDirection), normal),
                null); // TODO: decide parent
            starGameObject.transform.localScale =
                new Vector3(
                    minDistance
                    / Mathf.Abs(starGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.x)
                    / 2.0f,
                    minDistance
                    / Mathf.Abs(starGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.x)
                    / 2.0f,
                    topDirection.magnitude
                    / Mathf.Abs(starGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.z)
                );
            starGameObject.SetActive(false);
            yield return null;

            yield return StartCoroutine(
                ManipulateStarMesh(
                    strokes, points, sketchProcessor.hullVerts2D,
                    starGameObject.GetComponent<MeshFilter>().mesh,
                    newCenter2D,
                    (newMeshCenter)
                        =>
                    {
                        newCenter2D = newMeshCenter;
                        Debug.Log("Updated center");
                    }
                    ,
                    (meshManipulationSuccessful)
                        =>
                    {
                        if (meshManipulationSuccessful)
                        {
                            starGameObject.transform.rotation =
                                Quaternion.LookRotation(matTo3D.MultiplyVector(new Vector2(0f, 1f)), normal);
                            starGameObject.transform.localScale =
                                new Vector3(1f, starGameObject.transform.localScale.y, 1f);
                            starGameObject.transform.position = matTo3D.MultiplyPoint3x4(newCenter2D);
                            starGameObject.SetActive(true);
                            Debug.Log("Updated gameObject");
                        }
                        else
                        {
                            starGameObject.SetActive(true);
                            Debug.Log("Failed to change mesh");
                        }
                    }
                )
            );
        }

        IEnumerator ManipulateStarMesh(List<List<Vector2>> strokes, List<Vector2> points, List<Vector2> hullVerts2D,
            Mesh mesh, Vector2 offset, System.Action<Vector2> newMeshCenter, System.Action<bool> success)
        {

            // 1. Cartesian: find concave corners on sketch
            var concaveCorners = new List<Vector2>();
#if DEBUG
            concaveCorners = FindConcaveCornersByScanning(strokes);
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { concaveCorners },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true,
                "InnerCornersScanned");
            yield return null;
            concaveCorners.Clear();
            for (int i = 0; i < sketchProcessor.hullVerts2D.Count; ++i)
            {
                concaveCorners.Add(CalculationHelper.FindIntersection(
                    sketchProcessor.hullVerts2D[(i + 4) % sketchProcessor.hullVerts2D.Count],
                    sketchProcessor.hullVerts2D[(i + 1) % sketchProcessor.hullVerts2D.Count],
                    sketchProcessor.hullVerts2D[i],
                    sketchProcessor.hullVerts2D[(i + 2) % sketchProcessor.hullVerts2D.Count]));
            }

            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { concaveCorners },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true,
                "AutoIntersection");
            concaveCorners.Clear();
#endif
            for (int i = 0; i < strokes.Count; ++i)
            {
                for (int j = 0; j < strokes[i].Count - 1; ++j)
                {
                    for (int l = j + 2; l < strokes[i].Count - 1; ++l)
                    {
                        if (CalculationHelper.Intersects(strokes[i][j], strokes[i][j + 1], strokes[i][l],
                                strokes[i][l + 1]))
                        {
                            concaveCorners.Add((strokes[i][j] + strokes[i][j + 1] + strokes[i][l] + strokes[i][l + 1]) /
                                               4f);
                        }
                    }

                    for (int k = i + 1; k < strokes.Count; ++k)
                    {
                        for (int l = 0; l < strokes[k].Count - 1; ++l)
                        {
                            if (CalculationHelper.Intersects(strokes[i][j], strokes[i][j + 1], strokes[k][l],
                                    strokes[k][l + 1]))
                            {
                                concaveCorners.Add((strokes[i][j] + strokes[i][j + 1] + strokes[k][l] +
                                                    strokes[k][l + 1]) / 4f);
                            }
                        }
                    }
                }
            }

            Debug.Log("Found " + concaveCorners.Count + " by intersecting strokes");
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { concaveCorners },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true,
                "IntersectingStrokes");

            yield return null;

            // 2. Trim concave corners or use scanning method
            if (concaveCorners.Count > 5)
            {
                // Take away the furthest ones?
                // TODO: Check so far correct.
                while (concaveCorners.Count > 5)
                {
                    Vector2 furthestPoint = concaveCorners.Aggregate((s, a) => s.magnitude > a.magnitude ? s : a);
                    concaveCorners.RemoveAt(concaveCorners.IndexOf(furthestPoint));
                }

                CalculationHelper.DrawDebugLine(new List<List<Vector2>> { concaveCorners },
                    drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true,
                    "IntersectingStrokesTrimmed");
            }
            else if (concaveCorners.Count < 5)
            {
                // a. Use a circular scan or b. Make intersections from the spikes
                // TODO: Check
                concaveCorners = FindConcaveCornersByScanning(strokes);
                Debug.Log("Found " + concaveCorners.Count + " intersections by scanning outline");
                if (concaveCorners.Count != 5)
                {
                    // TODO: Use automatic intersection:
                    concaveCorners.Clear();
                    for (int i = 0; i < sketchProcessor.hullVerts2D.Count; ++i)
                    {
                        concaveCorners.Add(CalculationHelper.FindIntersection(
                            sketchProcessor.hullVerts2D[(i + 4) % sketchProcessor.hullVerts2D.Count],
                            sketchProcessor.hullVerts2D[(i + 1) % sketchProcessor.hullVerts2D.Count],
                            sketchProcessor.hullVerts2D[i],
                            sketchProcessor.hullVerts2D[(i + 2) % sketchProcessor.hullVerts2D.Count]));
                    }
                }
            }

            yield return null;

            // 3. Add spike anchors
            // new center has to be enclosed by the five inner corners
            Vector2 newCenter = concaveCorners.Aggregate(Vector2.zero, (s, v) => s + v) / (float)concaveCorners.Count;
            offset = newCenter;
            var centeredhullVerts2D =
                (from hullVert in sketchProcessor.hullVerts2D select (hullVert - newCenter)).ToList();
            var spikesPolar =
                ((from spikeTip in centeredhullVerts2D select CalculationHelper.CartesianToPolar(spikeTip)).OrderBy(
                    spikeTip => spikeTip.y)).ToList();
            var anchorPointsSketchPolar = new List<Vector2>(new Vector2[10]); // after centered
            for (int i = 0; i < 5; ++i)
            {
                Debug.Log(i.ToString() + ": spikesPolar[i]: " + spikesPolar[i]);
                anchorPointsSketchPolar[i * 2] = spikesPolar[i];
            }

            yield return null;

            // 4. Add inner anchors
            var centeredConcaveCorners = (from corner in concaveCorners select (corner - newCenter)).ToList();
            var concaveCornersPolar =
                ((from corner in centeredConcaveCorners select CalculationHelper.CartesianToPolar(corner)).OrderBy(
                    corner => corner.y)).ToList();

            if (concaveCornersPolar[0].y >= anchorPointsSketchPolar[0].y)
            {
                for (int i = 0; i < 8; i += 2)
                {
                    anchorPointsSketchPolar[i + 1] = concaveCornersPolar[i / 2];
                    if (concaveCornersPolar[i / 2].y < anchorPointsSketchPolar[i].y ||
                        concaveCornersPolar[i / 2].y >= anchorPointsSketchPolar[i + 2].y)
                    {
                        Debug.Log(i.ToString() + " Point not between: " + anchorPointsSketchPolar[i] +
                                  concaveCornersPolar[i / 2] + anchorPointsSketchPolar[i + 2]);
                    }
                }

                anchorPointsSketchPolar[9] = concaveCornersPolar[4];
            }
            else
            {
                anchorPointsSketchPolar[9] = concaveCornersPolar[0];
                for (int i = 0; i < 8; i += 2)
                {
                    anchorPointsSketchPolar[i + 1] = concaveCornersPolar[i / 2 + 1];
                    if (concaveCornersPolar[i / 2 + 1].y < anchorPointsSketchPolar[i].y ||
                        concaveCornersPolar[i / 2 + 1].y >= anchorPointsSketchPolar[i + 2].y)
                    {
                        Debug.Log(i.ToString() + " Point not between: " + anchorPointsSketchPolar[i] +
                                  concaveCornersPolar[i / 2 + 1] + anchorPointsSketchPolar[i + 2]);
                    }
                }
            }

            var anchorPointsSketch =
                (from point in anchorPointsSketchPolar select CalculationHelper.PolarToCartesian(point)).ToList();
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { anchorPointsSketch },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabYellow, true, "Model");

            yield return null;

            // 5. Find anchor points on model
            var vertices2D = (from vertex in mesh.vertices select new Vector2(vertex.x, vertex.z)).ToList();
            /* var hullVertsStarModel = GetConvexHull2D(vertices);
            /* CalculationHelper.Trim2DConvexHull(5, hullVertsStarModel);
            /* float spikeDistance = sum(from corner in hullVertsStarModel select CalculationHelper.CartesianToPolar(corner).x) / 5f;
            /* ... Accurate but too complex */
            var vertices2DPolar = (from vertex in vertices2D select CalculationHelper.CartesianToPolar(vertex))
                .OrderBy(v => v.x).ToList();
            var spikeDistance = vertices2DPolar[vertices2DPolar.Count - 1].x;
            vertices2DPolar = vertices2DPolar.OrderBy(v => v.y).ToList();
            // foreach (Vector2 vertex in vertices2DPolar) {
            //     Debug.Log("Vertex in polar: "vertex);
            // }
            yield return null;
            Debug.Log("spikeDistance: " + spikeDistance);
            float startAngle = 0f;
            for (int i = 0; i < vertices2DPolar.Count; ++i)
            {
                if (vertices2DPolar[i].x == spikeDistance)
                {
                    startAngle = vertices2DPolar[i].y;
                    Debug.Log("First spike: " + vertices2DPolar[i]);
                    break;
                }
            }

            yield return null;
            float angleUnit = Mathf.PI * 2f / 10f;
            startAngle = Mathf.Repeat(startAngle, 2 * angleUnit); // the smallest spike angle
            float nextAngle = startAngle + angleUnit;
            Vector2 closest =
                vertices2DPolar.Aggregate((x, y) => Mathf.Abs(x.y - nextAngle) < Mathf.Abs(y.y - nextAngle) ? x : y);
            int closestIdx = vertices2DPolar.IndexOf(closest);
            float max = 0f;
            for (int i = Mathf.Max(closestIdx - 10, 0), j = 0; j < 21; i = (i + 1) % vertices2DPolar.Count, j++)
            {
                Debug.Log(Mathf.DeltaAngle(Mathf.Rad2Deg * vertices2DPolar[i].y, Mathf.Rad2Deg * nextAngle));
                if (Mathf.Abs(Mathf.DeltaAngle(Mathf.Rad2Deg * vertices2DPolar[i].y, Mathf.Rad2Deg * nextAngle)) <=
                    18f && vertices2DPolar[i].x > max)
                {
                    max = vertices2DPolar[i].x;
                }
            }

            yield return null;
            // Debug.Log("Start from: " + startAngle + " with step " + angleUnit);
            // Debug.Log("closest: " + closest);
            // FIXME: It often comes back zero.
            var concaveCornerDistance = max;
            Debug.Log("concaveCornerDistance: " + concaveCornerDistance);
            var anchorPointsModelPolar = new List<Vector2>(new Vector2[10]);
            float angle = startAngle;
            for (int i = 0; i < 10; i += 2)
            {
                anchorPointsModelPolar[i] = new Vector2(spikeDistance, angle);
                angle += 2f * angleUnit;
                if (angle >= 2 * Mathf.PI)
                {
                    angle -= 2 * Mathf.PI;
                }
            }

            angle = startAngle + angleUnit;
            for (int i = 1; i < 10; i += 2)
            {
                anchorPointsModelPolar[i] = new Vector2(concaveCornerDistance, angle);
                angle += 2f * angleUnit;
                if (angle >= 2 * Mathf.PI)
                {
                    angle -= 2 * Mathf.PI;
                }
            }

            var anchorPointsModel =
                (from anchor in anchorPointsModelPolar select CalculationHelper.PolarToCartesian(anchor)).ToList();
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { anchorPointsModel },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true, "FullModel");
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { vertices2D },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlue, true, "ModelOutline");

            yield return null;

            // 6. Matrix
            List<Matrix4x4> matrices = new List<Matrix4x4>(new Matrix4x4[10]);
            for (int i = 0; i < 10; ++i)
            {
                // Check done!
                matrices[i] = CalculationHelper.GetTriMappingMatrix2D(Vector2.zero, anchorPointsModel[i],
                    anchorPointsModel[(i + 1) % 10], Vector2.zero, anchorPointsSketch[i],
                    anchorPointsSketch[(i + 1) % 10]);
                Debug.Log("matrices[i].MultiplyPoint3x4(anchorPointsModel[i]) vs anchorPointsSketchPolar[i]: " +
                          (matrices[i].MultiplyPoint3x4(anchorPointsModel[i])) + anchorPointsSketch[i]);
                Debug.Log("Centroid change actual: " + ((anchorPointsModel[i] + anchorPointsModel[(i + 1) % 10]) / 3f) +
                          " -> " + ((anchorPointsSketch[i] + anchorPointsSketch[(i + 1) % 10]) / 3f));
                Debug.Log("Centroid change Matrix: " + ((anchorPointsModel[i] + anchorPointsModel[(i + 1) % 10]) / 3f) +
                          " -> " + matrices[i]
                              .MultiplyPoint3x4((anchorPointsModel[i] + anchorPointsModel[(i + 1) % 10]) / 3f));
            }

            yield return null;

            // 7. Change vertices
            var vertices3D = mesh.vertices;
            vertices2D = (from vertex in vertices3D select new Vector2(vertex.x, vertex.z)).ToList();
            vertices2DPolar = (from vertex in vertices2D select CalculationHelper.CartesianToPolar(vertex)).ToList();
            for (int i = 0; i < vertices3D.Length; ++i)
            {
                float angleDifference = Mathf.Repeat(vertices2DPolar[i].y - startAngle, 2 * Mathf.PI);
                Debug.Log(angleDifference);
                int idx = (int)Mathf.Floor(angleDifference / angleUnit);
                // Debug.Log(vertices2DPolar[i].y);
                Debug.Log(idx);
                idx = Mathf.Min(idx, 9);
                Debug.Log("vertices2DPolar[i]: " + vertices2DPolar[i]);
                Debug.Log("anchorPointsModelPolar[idx]: " + anchorPointsModelPolar[idx]);
                Debug.Log("anchorPointsModelPolar[(idx+1)%10]: " + anchorPointsModelPolar[(idx + 1) % 10]);
                Vector2 v = matrices[idx].MultiplyPoint3x4(new Vector2(vertices3D[i].x, vertices3D[i].z));
                vertices3D[i] = new Vector3(v.x, vertices3D[i].y, v.y);
                if (i % 10 == 0)
                {
                    yield return null;
                }
            }

            // #if DEBUG
            // var temp = new List<Vector2>(new Vector2[10]);
            // for (int i = 0; i < anchorPointsModelPolar.Count; ++i) {
            //     float angleDifference = Mathf.Repeat(anchorPointsModelPolar[i].y - startAngle, 2 * Mathf.PI);
            //     Debug.Log(angleDifference);
            //     int idx = (int) Mathf.Floor(angleDifference / angleUnit);
            //     // Debug.Log(vertices2DPolar[i].y);
            //     Debug.Log(idx);
            //     idx = Mathf.Min(idx, 9);
            //     temp[i] = matrices[idx].MultiplyPoint3x4(anchorPointsModel[i]);
            // }
            // CalculationHelper.DrawDebugLine(new List<List<Vector2>> {temp}, drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlack, true);
            // #endif
            var newVertices2D = (from vertex in vertices3D select new Vector2(vertex.x, vertex.z)).ToList();
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { newVertices2D },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlue, true, "ModelProjected");
            mesh.vertices = vertices3D;

            newMeshCenter(newCenter);
            success(true);

            yield return null;
        }

        private List<Vector2> FindConcaveCornersByScanning(List<List<Vector2>> strokes)
        {
            // FIXME: Sometimes incorrect (jagged)
            // 2. Get outline
            List<Vector2> outlinePolar = CalculationHelper.GetOutlinePolar(strokes);
#if DEBUG
            var outline = (from point in outlinePolar select CalculationHelper.PolarToCartesian(point)).ToList();
            CalculationHelper.DrawDebugLine(new List<List<Vector2>> { outline },
                drawManager.coordinateSpaceBoundingPlanes, drawManager.debugStrokePrefabBlue, true, "OutlineScanned");
#endif

            // 3. Trim it
            var concaveCorners = new List<Vector2>();
            var concaveCornersIdx = new List<int>();

            for (int i = 0; i < outlinePolar.Count; ++i)
            {
                if (outlinePolar[i].x >= outlinePolar[(i + 1) % outlinePolar.Count].x &&
                    outlinePolar[(i + 1) % outlinePolar.Count].x <= outlinePolar[(i + 2) % outlinePolar.Count].x)
                {
                    concaveCorners.Add(CalculationHelper.PolarToCartesian(outlinePolar[(i + 1) % outlinePolar.Count]));
                    concaveCornersIdx.Add((i + 1) % outlinePolar.Count);
                }
            }

            // FIXME: Frequently gets 4
            Debug.Log("First Scan yields " + concaveCorners.Count + " inner corners");
            if (concaveCorners.Count > 5)
            {
                int range = 2;
                var recentlyRemovedIdx = new List<int>();
                while (concaveCorners.Count > 5 && range < outlinePolar.Count)
                {
                    recentlyRemovedIdx.Clear();
                    for (int i = 0; i < concaveCorners.Count; ++i)
                    {
                        bool passed = false;
                        int idx = concaveCornersIdx[i];
                        for (int j = 2; j <= range & concaveCorners.Count > 5; ++j)
                        {
                            if (outlinePolar[(idx + outlinePolar.Count - j) % outlinePolar.Count].x <=
                                outlinePolar[(idx + outlinePolar.Count - j + 1) % outlinePolar.Count].x ||
                                outlinePolar[(idx + j) % outlinePolar.Count].x <=
                                outlinePolar[(idx + j - 1) % outlinePolar.Count].x)
                            {
                                concaveCorners.RemoveAt(i);
                                concaveCornersIdx.RemoveAt(i);
                                recentlyRemovedIdx.Add(idx);
                                passed = true;
                                break;
                            }
                        }

                        if (passed)
                        {
                            i--;
                        }
                    }

                    range++;
                }

                if (concaveCorners.Count < 5)
                {
                    // Do something using recentlyRemovedIdx
                    // shortestDistanceToAnotherAngle = List<float>(new float[recentlyRemovedIdx.Count]);
                    // foreach (int idx in recentlyRemovedIdx) {
                    //     shortestDistanceToAnotherAngle = 
                    //         concaveCorners.
                    //             Aggregate (
                    //                 (p, n) => 
                    //                     Mathf.Abs(Mathf.DeltaAngle(Mathf.Rad2Deg * p.y, Mathf.Rad2Deg * outlinePolar[idx].y))
                    //                     < Mathf.Abs(Mathf.DeltaAngle(Mathf.Rad2Deg * n.y, Mathf.Rad2Deg * outlinePolar[idx].y)) 
                    //                     ? p 
                    //                     : n
                    //             );
                    // }
                }
            }

            return concaveCorners;
        }

        void GenerateUmbrella(List<Vector2> points, Matrix4x4 matTo3D, Vector3 normal, bool flat = true)
        {
            Debug.Log("Generating Umbrella");
            if (flat || !flat)
            {
                // looks like there is not difference
                /*
                Strategy:
                Get the two longest sides, average them out. That's roughly the direction of the handle.
                TODO: Find the stroke that is closest to that direction and that would be the handle.
                */
                // 1. Find the two longest sides (the V at the bottom)
                // sketchProcessor.hullVerts2D = GetConvexHull2D(points);
                int longest = 0, secondLongest = 0;
                float longestValue = 0f, secondLongestValue = 0f;
                for (int i = 0; i < sketchProcessor.hullVerts2D.Count; ++i)
                {
                    float length = Vector2.Distance(sketchProcessor.hullVerts2D[i],
                        sketchProcessor.hullVerts2D[(i + 1) % sketchProcessor.hullVerts2D.Count]);
                    if (length > longestValue)
                    {
                        secondLongestValue = longestValue;
                        secondLongest = longest;
                        longestValue = length;
                        longest = i;
                    }
                    else if (length > secondLongestValue)
                    {
                        secondLongestValue = length;
                        secondLongest = i;
                    }
                }

                // 2. Identify the direction of the V
                (int l, int s) closerPair = (longest, secondLongest);
                (int l, int s) fartherPair = (longest, secondLongest);
                float shortestDistance = float.PositiveInfinity;
                float longestDistance = 0f;
                foreach (int i in new List<int>() { longest, (longest + 1) % sketchProcessor.hullVerts2D.Count })
                {
                    foreach (int j in new List<int>()
                                 { secondLongest, (secondLongest + 1) % sketchProcessor.hullVerts2D.Count })
                    {
                        Debug.Log("i = " + i + ", j = " + j);
                        if (Vector2.Distance(sketchProcessor.hullVerts2D[i], sketchProcessor.hullVerts2D[j]) <
                            shortestDistance)
                        {
                            shortestDistance = Vector2.Distance(sketchProcessor.hullVerts2D[i],
                                sketchProcessor.hullVerts2D[j]);
                            closerPair = (i, j);
                        }

                        if (Vector2.Distance(sketchProcessor.hullVerts2D[i], sketchProcessor.hullVerts2D[j]) >
                            longestDistance)
                        {
                            longestDistance = Vector2.Distance(sketchProcessor.hullVerts2D[i],
                                sketchProcessor.hullVerts2D[j]);
                            fartherPair = (i, j);
                        }
                    }
                }

                // Debug.Log("Long side: " + longest + ": " + sketchProcessor.hullVerts2D[longest] + ", " + longest + ": " + sketchProcessor.hullVerts2D[(longest+1)%sketchProcessor.hullVerts2D.Count]);
                // Debug.Log("Short side: " + secondLongest + ": " + sketchProcessor.hullVerts2D[secondLongest] + ", " + secondLongest + ": " + sketchProcessor.hullVerts2D[(secondLongest+1)%sketchProcessor.hullVerts2D.Count]);
                // Debug.Log("closer: " + closerPair);
                // Debug.Log("farther: " + fartherPair);
                var longestVector =
                    (sketchProcessor.hullVerts2D[fartherPair.l] - sketchProcessor.hullVerts2D[closerPair.s]).normalized;
                // var longestVector = (sketchProcessor.hullVerts2D[longest] - sketchProcessor.hullVerts2D[(longest+1)%sketchProcessor.hullVerts2D.Count]).normalized;
                // longestVector = Mathf.Sign(longestVector.x) * longestVector;
                var secondLongestVector =
                    (sketchProcessor.hullVerts2D[fartherPair.s] - sketchProcessor.hullVerts2D[closerPair.l])
                    .normalized; // merge on the narrower side
                // var secondLongestVector = (sketchProcessor.hullVerts2D[(secondLongest+1)%sketchProcessor.hullVerts2D.Count] - sketchProcessor.hullVerts2D[secondLongest]).normalized;
                // secondLongestVector = - Mathf.Sign(secondLongestVector.x) * secondLongestVector;
                // Debug.Log("Longest: " + longest + " " + longestVector + ", Second: " + secondLongest + " " + secondLongestVector);
                var uprightDir = ((longestVector + secondLongestVector) / 2f).normalized;
                var uprightSlope = uprightDir.y / uprightDir.x;
                Debug.Log("uprightDir: " + uprightDir);
                drawManager.slope2DSVD = uprightSlope;
                // Debug.Log("averageSlope: " + averageSlope);
                // 3. Find bounding box (center and the extents)
                Vector2 newCenter2D;
                float height, width;
                CalculationHelper.FindBoundingBox2D(sketchProcessor.hullVerts2D, uprightSlope, out newCenter2D,
                    out height, out width);
                Debug.Log("newCenter2D: " + newCenter2D);
                // NOTE: Instantiate uses the pivot point not the center
                // In this case the umbrella's pivot point is at the bottom of its handle
                // TODO: Find out how long the top part is.
                // TODO: Find handle direction. Right now I'm using a not-so-accurate trick where the shorter side is the direction
                Vector3 pivot = matTo3D.MultiplyPoint3x4(newCenter2D - height * 0.5f * uprightDir);
                Vector3 upperEnd = matTo3D.MultiplyPoint3x4(newCenter2D + height * 0.5f * uprightDir);
                Vector3 rightwardDir = new Vector2(uprightDir.y, -uprightDir.x);
                Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
                float sign = Mathf.Sign(Vector2.Dot(rightwardDir, secondLongestVector));
                GameObject newGameObject =
                    Instantiate(
                        objectList[1].gameObjects[UnityEngine.Random.Range(0, objectList[1].gameObjects.Length)],
                        pivot,
                        Quaternion.LookRotation(sign * Vector3.Cross(normal, matTo3D.MultiplyVector(uprightDir)),
                            matTo3D.MultiplyVector(uprightDir)), // (z, upward) y = 
                        null); // TODO: decide parent
                // FIXED: The boudning box seems to be wrong. -> changed center before setting pivot
                // Instantiate(drawManager.referencePoint, pivot, Quaternion.identity, null);
                // Instantiate(drawManager.referencePointYellow, upperEnd, Quaternion.identity, null);
                // Instantiate(drawManager.referencePointBlue, center, Quaternion.identity, null);
                // Instantiate(drawManager.referencePointRed, newCenter3D, Quaternion.identity, null);
                Transform child = newGameObject.GetComponentInChildren<MeshFilter>().transform;
                newGameObject.transform.localScale =
                    new Vector3(
                        newGameObject.transform.localScale.x
                        * width
                        / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.x)
                        / 2f
                        / child.localScale.x,
                        newGameObject.transform.localScale.y
                        * height
                        / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.y)
                        / 1.8f
                        / child.localScale.y,
                        newGameObject.transform.localScale.z
                        * width
                        / Mathf.Abs(newGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.z)
                        / 2f
                        / child.localScale.z
                    );
            }
        }

        private void GenerateApple(List<List<Vector2>> strokes, List<Vector2> points, Matrix4x4 matTo3D, Vector3 normal)
        {
            // The bounindg box center would be below the mass center because the upper part is denser (maybe we can do that with umbrella too)
            // Vector2 tempNewCenter2D;
            // float tempHeight, tempWidth;
            // CalculationHelper.FindBoundingBox(sketchProcessor.hullVerts2D, slope2DBoundingPlanes, out tempNewCenter2D, out tempHeight, out tempWidth);
            // Vector3 tempCenter = matTo3D.MultiplyPoint3x4(tempNewCenter2D);
            Vector2 centroidHull = CalculationHelper.Vector2Centroid(sketchProcessor.hullVerts2D);
            // Vector2 uprightDir2D = Vector2.zero - tempNewCenter2D;
            Vector2 uprightDir2D = Vector2.zero - centroidHull;
            float bestSlope = uprightDir2D.y / uprightDir2D.x;

            // Find highest lowest leftmost rightmost points and use average as reference for center and localScale
            Vector2 newCenter2D;
            float height, width;
            CalculationHelper.FindBoundingBox2D(sketchProcessor.hullVerts2D, bestSlope, out newCenter2D, out height,
                out width);
            Vector3 newCenter3D = matTo3D.MultiplyPoint3x4(newCenter2D);
            Vector3 pivot =
                matTo3D.MultiplyPoint3x4(newCenter2D -
                                         height * 0.5f * new Vector2(1f, bestSlope) * Mathf.Sign(bestSlope));
            Vector3 upperEnd =
                matTo3D.MultiplyPoint3x4(newCenter2D +
                                         height * 0.5f * new Vector2(1f, bestSlope) * Mathf.Sign(bestSlope));
            GameObject newGameObject =
                Instantiate(
                    objectList[5].gameObjects[UnityEngine.Random.Range(0, objectList[5].gameObjects.Length)],
                    newCenter3D,
                    Quaternion.LookRotation(matTo3D.MultiplyVector(new Vector2(1f, bestSlope) * Mathf.Sign(bestSlope)),
                        normal),
                    null // TODO: decide parent
                );
#if DEBUG
            Instantiate(drawManager.referencePoint, pivot, Quaternion.identity, null);
            Instantiate(drawManager.referencePoint, upperEnd, Quaternion.identity, null);
# endif
            newGameObject.transform.localScale =
                new Vector3(
                    width
                    / Mathf.Abs(newGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.x)
                    / 2f,
                    width
                    / Mathf.Abs(newGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.y)
                    / 2f,
                    height
                    / Mathf.Abs(newGameObject.GetComponent<MeshFilter>().mesh.bounds.extents.z)
                    / 2f
                );
            /*
            TODO:
            Different strategies to choose from:
            1. Symmetry (hard)
            2. Find the stroke that forms the most consistent slope with its points and the center (medium), that stroke will be the leaf or stalk
                If it's the leaf (stroke starts and ends near each other) (what if two strokes?)
                If it's the stalk (stroke starts and ends far away from each other)
                * Fails if there are small, little strokes.
            3. Find the stroke whose "line segments"'s ray is closest to the origin. (easy)
            4. (deadlock) Since the upper part of the apple is always denser, just take the direction from center of bounding box to the stroke center.
            5. From 4., just use bounding line as slope first.
            */
        }
}
