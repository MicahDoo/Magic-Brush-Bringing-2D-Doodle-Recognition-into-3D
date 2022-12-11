// #define UMBRELLA_MODE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using System.Threading;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Providers.LinearAlgebra;
using System.Linq;
using GK;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine.ProBuilder;

public struct ProjectionPlane {
    public string               protocol;
    public Vector3              normal;
    public Vector3              basis1;
    public Vector3              basis2;
    public Matrix4x4            matTo2D;
    public Matrix4x4            matTo3D;
    public Matrix4x4            matOrthoProj;

    public Vector3 MatTo3D(Vector2 p) { return matTo3D.MultiplyPoint3x4(p); }
    public Vector2 MatTo2D(Vector3 p) { return matTo2D.MultiplyPoint3x4(p); }
    public Vector3 MatOrthoProj(Vector3 p) { return matOrthoProj.MultiplyPoint3x4(p); }

    // TODO: Store the strokes in 2D and 3D
    public List<List<Vector2>>  strokesOn2DCoords;
    public List<List<Vector3>>  strokesProjected;
    public List<Vector2>        pointsOnCoord;
    public List<Vector2>        hullVerts2D;

    public Vector3 pa;
    public Vector3 pb;
    public Vector3 px;
    public Vector3 pz;
    
    // directions
    public Vector3 dirSvd;
    public Vector3 dirBl;
    public Vector3 dirPd;
    public Vector3 dirMo; // mass offset
}

public struct PredrawnSketch {
    public bool flat;
    public string result;
    public List<List<Vector3>> strokes;
    public Vector3 avgLookDirection;
    public Vector3 avgTopDirection;
}

public class SketchProcessor : MonoBehaviour
{
    public struct ConvexHull {
        public List<Vector3>        verts;
        public List<int>            tris;
        public List<Vector3>        normals;
    }
    public  string                              projectedPlane3DName = "PairedDistance";
    public  string                              displayedProjection1Name = "AvgTorque";
    public  string                              displayedProjection2Name = "BoundingBoxAnalysis";
    public  string                              protocolUsedForRecognition = "PairedDistance";
    public  bool                                displayBestFitLineOn2DCoord = true;
    public  bool                                displayBestBoundingLinesOn2DCoord = true;
    public  bool                                displayLongestP2PLineOn2DCoord = false;
    public  string                              bestPlaneName;
    public  Dictionary<string, ProjectionPlane> planes              = new Dictionary<string, ProjectionPlane>();
    public  List<List<Vector3>>                 strokes;
    public  List<LineRenderer>                  strokesLR;
    public  List<Vector3>                       points;
    public  bool                                flat;
    public  ConvexHull                          hull3D;
    public  List<Vector2>                       hullVerts2D;
    public  int                                 bestTrisIndex;
    public  int                                 bestVertIndex;
    public  float                               BPDistance3D;
    public  Vector3                             BPRefPoint;

    public  bool                                planesCalculated = false;

    private Vector3                             avgLookDirection;
    private Vector3                             avgTopDirection;
    private DrawManager                         drawManager;

    public  Vector3                             center;
    public  Vector3                             centroid;

    /*TENTATIVE*/
    private List<LineRenderer> strokesOnCoordLR1;
    private List<LineRenderer> strokesOnCoordLR2;
    private Recognizer                          recognizer;
    private ObjectGenerator                     objectGenerator;

    private GameObject                          coordinateSpaceSVD;
    private GameObject                          coordinateSpaceBoundingPlanes;

    private bool                                isExecuting;
    public bool IsExecuting {
        set {isExecuting = value;}
    }

    private bool                                saveAsPredrawnSketch;
    private string                              result;

    public struct ReferencePoints
    {
        public Vector2 boxCenter2D;
        public Vector3 boxCenter;
        public Vector3 outlineCenter;
        public Vector3 centroidHull;
        public Vector3 centroid3DHull;
    }

    public ReferencePoints refPoints;

    private float height;
    private float width;
    private float depth;

    private float slope2DBP;


    public void Init(List<List<Vector3>> strokes, List<LineRenderer> strokesLR, int i, Vector3 avgLookDirection, Vector3 avgTopDirection, DrawManager drawManager, ref DrawManager.DrawStates drawState)  {
        
        Debug.Log("SketchProcessor initialized.");
        // transfer variables
        this.strokes = TurnStrokesLrToStrokes(strokesLR);
        this.strokesLR                  = strokesLR;
        this.drawManager                = drawManager;
        #if UMBRELLA_MODE
        this.avgLookDirection           = avgTopDirection;
        this.avgTopDirection            = avgLookDirection;
        #else
        this.avgLookDirection           = avgLookDirection;
        this.avgTopDirection            = avgTopDirection;
        #endif
        coordinateSpaceSVD              = drawManager.coordinateSpaceSVD;
        coordinateSpaceBoundingPlanes   = drawManager.coordinateSpaceBoundingPlanes;
        recognizer                      = drawManager.recognizer;

        hull3D                          = new ConvexHull
        {
            verts = new List<Vector3>(),
            tris = new List<int>(),
            normals = new List<Vector3>()
        };
        
        objectGenerator                 = gameObject.AddComponent<ObjectGenerator>();
        hullVerts2D = new List<Vector2>();

        bestPlaneName                   = "PairedDistance";

        Thread t                        = new Thread(FindPlanes);
        t.Start();
    }

    List<List<Vector3>> TurnStrokesLrToStrokes(List<LineRenderer> strokesLr)
    {
        var s = new List<List<Vector3>>();
        foreach (var lr in strokesLr)
        {
            Vector3[] pos = new Vector3[lr.positionCount];
            lr.GetPositions(pos);
            s.Add(pos.ToList());
        }

        return s;
    }

    void SavePredrawnSketch() {

        // TODO: manually input the result
        PredrawnSketch predrawn = new PredrawnSketch {
            flat = flat,
            result = result,
            strokes = strokes,
            avgLookDirection = avgLookDirection,
            avgTopDirection = avgTopDirection,
        };
        // https://docs.unity3d.com/Manual/JSONSerialization.html
        var json = JsonConvert.SerializeObject(predrawn, drawManager.jsonSettings);
        string filename = Application.dataPath + "/" + "Predrawns.json";
        // tip on @"hkuyfjyt": https://stackoverflow.com/questions/66690918/deserialzation-from-json-file-with-multiple-objects-in-c-sharp
        // json = File.ReadAllText(filename);
        // outputSketch = JsonConvert.DeserializeObject<PredrawnSketch>(json);
        drawManager.ExportText(filename, json, true);
    }

    /*  1.
            "Average Look Direction"    Easy
            "SVD"                       Singular Value Decomposition
            "Shortest Depth"            Bounding Planes.
                                        Good for finding object direction.
            "Two Linear Regressions"    Doesn't work
            "Paired Distance"           1. Greatest distance between any pair of two points. That's one basis
                                        2. Compress along the line that the pair forms. 
                                        3. Repeat 1. That's a new basis.
                                        4. Find normal with the two basis.
                                        5. Adjust the basis based on the top direction.
                                        ~ Feels like this one is good even for 3D sketches.
            "Avg Torque"                1. Average of torques
            "BoundingBoxAnalysis"       1. Using the Adjusted Bounding Planes
                                        2. Identify 6 faces:
                                            I.  normal
                                            II. -normal
                                            III.base1
                                            IV. -base1
                                            V.  base2
                                            VI. -base2
                                        3. Find closest face for all stroke points:
                                            1. Angle formed with the center and the point, expressed as linear vector.
                                            2. Normalize by dividing by the l, w, h. (Because it's not a cube but a cuboid)
                                            2. See which face normal is closest.
                                        4. Tally them.
                                        5. Analyze distribution.

        */

    void Update() {
        if (planesCalculated) {
            planesCalculated = false; // Do first so errors won't repeat in next Update()
            drawManager.PlanesGenerated();
            Debug.Log("TIMESTAMP: drawManager.drawState = DrawManager.DrawStates.planesGenerated");
            Debug.Log("Generating plane object: " + displayedProjection1Name);
            drawManager.bestPlaneSVDGameObject = 
                Instantiate(drawManager.bestPlaneSVDPrefab, 
                    center, 
                    Quaternion.LookRotation(planes[displayedProjection1Name].normal, Vector3.up), 
                    null);
            drawManager.bestPlaneSVDGameObject.name = "Plane " + displayedProjection1Name;
            drawManager.bestPlaneBPGameObject = 
                Instantiate(drawManager.bestPlaneBoundingPrefab, 
                    BPRefPoint, 
                    Quaternion.LookRotation(planes[displayedProjection2Name].normal, Vector3.up), 
                    null);
            drawManager.bestPlaneBPGameObject.name = "Plane " + displayedProjection2Name;
            if (!flat) {
                Instantiate(drawManager.bestPlaneBoundingPrefab, center, Quaternion.LookRotation(planes["AdjustedBP"].normal, Vector3.up), null).name = "Plane Adjusted BP";
            }

            StartCoroutine(ProcessSketch());
        }
    }

    public void DetermineBestProjectionPlane() {
        
    }

    void FindPlanes() {
        points = strokes.SelectMany(i => i).ToList(); // Flatten
        
        Thread threadAvgLookDir          = new Thread(GetBestPlaneByAvgLookDirection);
        Thread threadSVD                 = new Thread(_GetBestPlaneBySvd);
        Thread threadBoundingPlanes      = new Thread(_GetBestPlaneByBoundingPlanes);
        Thread threadAdjustedBP          = new Thread(GetBestPlaneByAdjustedBp);         // HARD! Use original easy method for now
        Thread threadPairedDistance      = new Thread(_GetBestPlaneByPairedDistance);
        Thread threadBoundingBoxAnalysis = new Thread(_GetBestPlaneByBoundingBoxAnalysis);
        Thread threadAvgTorque = new Thread(_GetBestPlaneByAvgTorque);

        threadBoundingPlanes.Start();
        threadAvgLookDir    .Start();
        threadSVD           .Start();
        threadPairedDistance.Start();
        threadAvgTorque.Start();

        threadBoundingPlanes.Join ();
        threadAdjustedBP    .Start(); // Dependency: threadBoundingPlanes
        threadAdjustedBP    .Join ();
        threadBoundingBoxAnalysis.Start(); // Dependency: threadAdjustedBP
        threadAvgLookDir    .Join ();
        threadSVD           .Join ();
        threadPairedDistance.Join ();
        threadAvgTorque.Join();
        threadBoundingBoxAnalysis.Join();
        
        Debug.Log("All planes generated.");

        var keys = planes.Keys;
        foreach (var key in keys)
        {
            Debug.Log("Recorded plane: " + key);
        }

        planesCalculated = true;
    }

    IEnumerator ProcessSketch() {

        yield return null;
        hullVerts2D = new List<Vector2> ();
        
        // The first thing to do is to put stroke data into local variable so we can start drawing the next thing as soon as possible.
        /*  0. 
            Prepare a local copy of the strokes so the player 
            can start a new sketch in the next frame.
        */
        drawManager.drawState = DrawManager.DrawStates.idle;

        var stopwatch = new System.Diagnostics.Stopwatch();

        yield return null;

        stopwatch.Start();
        Debug.Log("Start Recognition + Generatioin");

        // FIXME:
        foreach (string key in planes.Keys.ToList())
        {
            ProjectStrokesLr(key, strokesOnCoordLR1, false);
        }

        if (strokesOnCoordLR1 != null) {
            Debug.Log("Destroying strokes");
            for (int i = 0; i < strokesOnCoordLR1.Count; ++i) {
                Destroy(strokesOnCoordLR1[i].gameObject);
            }
        }

        if (strokesOnCoordLR2 != null)
        {
            Debug.Log("Destroying strokes");
            for (int i = 0; i < strokesOnCoordLR2.Count; ++i)
            {
                Destroy(strokesOnCoordLR2[i].gameObject);
            }
        }

        strokesOnCoordLR1 = new List<LineRenderer>(new LineRenderer[strokes.Count]);
        strokesOnCoordLR2 = new List<LineRenderer>(new LineRenderer[strokes.Count]);
        
        foreach (string key in planes.Keys.ToList()) {
            if (key == displayedProjection1Name) {
                for (int i = 0; i < strokes.Count; ++i) {
                    strokesOnCoordLR1[i] = Instantiate(drawManager.strokeOnCoordPrefab, 
                                            coordinateSpaceSVD.transform.position, 
                                            coordinateSpaceSVD.transform.rotation, 
                                            coordinateSpaceSVD.transform);
                    strokesOnCoordLR1[i].name = "Sketch Stroke on 2D";
                }
                ProjectStrokesLr(key, strokesOnCoordLR1, true);
            } else if (key == displayedProjection2Name) {
                for (int i = 0; i < strokes.Count; ++i) {
                    strokesOnCoordLR2[i] = Instantiate(drawManager.strokeOnCoordPrefab, 
                                            coordinateSpaceBoundingPlanes.transform.position, 
                                            coordinateSpaceBoundingPlanes.transform.rotation, 
                                            coordinateSpaceBoundingPlanes.transform);
                    strokesOnCoordLR2[i].name = "Sketch Stroke on 2D";
                } 
                ProjectStrokesLr(key, strokesOnCoordLR2, true);
            }

            yield return null;
        }

        /* POTENTIAL ERROR:
        ** WHEN: planes.Keys.ToList() without ToList()
        ** MSEG: Collection was modified; enumeration operation may not execute
        ** WHY?: because `threads[key].Join()` might modify the Dictionary and hence changing Keys
        */

        Debug.Log("Did projection" + stopwatch.Elapsed);
        yield return null;
        stopwatch.Reset();
        stopwatch.Start();

        // TODO: 11/09
        // 5. Post processing On 2D
        GetAllReferencePoints();

        Debug.Log("Did 2D Analysis: Reference Points" + stopwatch.Elapsed);
        yield return null;
        Debug.Log(stopwatch.Elapsed);
        stopwatch.Reset();
        stopwatch.Start();

        // 5. Feed To Recognizer (TODO: move it ahead so it finishes earlier);
        Debug.Log("Feed Model");

        string[] resultSVD = new string[100];
        string[] resultBoundingPlanes = new string[100];

        /* TODO:
        *  Since we are using bitmap images of dimension 28 * 28 only, once the drawing gets really long it is shrunk
        *  to the degree that some of the detail is lost. Hence, in case of a really oblong drawing, I trim out the
        *  less important end (the end with less mass). This issue should be obviated if the images are of a dimension
        *  that's higher, preferably 50+. I can do it by converting the vector dataset from QuickDraw to higher-
        *  resolution bitmaps or use another dataset like the one from TU-Berlin.
        */

        var strokesOnCoord1 = planes[displayedProjection1Name].strokesOn2DCoords;
        var strokesOnCoord2 = planes[displayedProjection2Name].strokesOn2DCoords;

        if (height/width > 4f) {
            Debug.Log("Sketch too thin. Trying trimming.");
            List<List<Vector2>> trimmedstrokesOnCoord1 = TrimStrokes(strokesOnCoord1, slope2DBP, refPoints.boxCenter2D, height, width);
            List<List<Vector2>> trimmedstrokesOnCoord2 = TrimStrokes(strokesOnCoord2, slope2DBP, refPoints.boxCenter2D, height, width);
            // CalculationHelper.DrawDebugLine(trimmedstrokesOnCoord1, coordinateSpaceBoundingPlanes, debugStrokePrefabBlack, true, "TrimmedStrokesSVD");
            // CalculationHelper.DrawDebugLine(trimmedstrokesOnCoord2, coordinateSpaceBoundingPlanes, debugStrokePrefabBlack, true, "TrimmedStrokesBP");
            isExecuting = true;
            yield return StartCoroutine(recognizer.DoodleRecognition(trimmedstrokesOnCoord1, resultSVD, this, 0));
            while(isExecuting) {
                yield return null;
            }
            isExecuting = true;
            yield return StartCoroutine(recognizer.DoodleRecognition(trimmedstrokesOnCoord2, resultBoundingPlanes, this, 1));
            while(isExecuting) {
                yield return null;
            }
        } else {
            isExecuting = true;
            yield return StartCoroutine(recognizer.DoodleRecognition(strokesOnCoord1, resultSVD, this, 0));
            while(isExecuting) {
                yield return null;
            }
            isExecuting = true;
            yield return StartCoroutine(recognizer.DoodleRecognition(strokesOnCoord2, resultBoundingPlanes, this, 1));
            while(isExecuting) {
                yield return null;
            }
        }
        Debug.Log("Results: ");
        Debug.Log(string.Join("\n", resultSVD));
        Debug.Log(string.Join("\n", resultBoundingPlanes));
        result = GetFinalResult(resultSVD, resultBoundingPlanes);
        Debug.Log("Final result: " + result);

        yield return null;
        stopwatch.Reset();
        stopwatch.Start();

        // 7. Export result
        DrawManager.Sketch outputSketch = new DrawManager.Sketch {
            finalDecision = result,
            resultSVD = resultSVD,
            resultBoundingPlanes = resultBoundingPlanes,
            bitmap = recognizer.resultedBitmap256,
            strokes = strokes,
            flat = flat,
            normalSVD = planes["SVD"].normal,
            normalBoundingPlanes = planes["BoundingPlanes"].normal,
            normalPairedDistance = planes["PairedDistance"].normal,
            normalBBAnalysis = planes["BoundingBoxAnalysis"].normal,
        };
        // https://docs.unity3d.com/Manual/JSONSerialization.html
        // var json = JsonUtility.ToJson(outputSketch, true); //prettyPrint = true
        var json = JsonConvert.SerializeObject(outputSketch, drawManager.jsonSettings);
        // outputSketch = JsonConvert.DeserializeObject<Sketch>(json);
        json = recognizer.bitmapsInString[0] + "\n" + recognizer.bitmapsInString[1] + "\n" + json + "\n";
        string filename = Application.dataPath + "/" + "sketches.json";
        drawManager.ExportText(filename, json, true);

        if (drawManager.saveAsPredrawnSketch) {
            Debug.Log("Save Predrawn");
            SavePredrawnSketch();
        }

        Debug.Log("Saved file " + stopwatch.Elapsed);
        yield return null;
        stopwatch.Reset();
        stopwatch.Start();
        
        yield return null;

        yield return StartCoroutine(drawManager.objectGenerator.GenerateObject(result, this));

        Debug.Log("Generated Object: " + stopwatch.Elapsed);
        yield return null;

        Debug.Log("End Recognition + Generation");
        Debug.Log(stopwatch.Elapsed);
        drawManager.isRecognizing = false;
        stopwatch.Stop();

        yield return null;
    }

    private void GetAllReferencePoints()
    {
        // TODO: Move these to each plane
        CalculationHelper.GetBestFitLineByLinearRegression(planes["AdjustedBP"].pointsOnCoord, out drawManager.yIntercept, out drawManager.slope2DSVD);
        Debug.Log("planes[bestPlaneName].pointsOnCoord.Count = " + planes[bestPlaneName].pointsOnCoord.Count);
        drawManager.slope = drawManager.slope2DSVD;
        drawManager.lengthDirection = planes["AdjustedBP"].matTo3D.MultiplyVector(new Vector2(1.0f, drawManager.slope));
        
        CalculationHelper.GetBestFitLineByBoundingLines(planes["AdjustedBP"].pointsOnCoord, ref hullVerts2D, out drawManager.point1Index, 
                                                        out drawManager.point2Index, out slope2DBP, out drawManager.BoundingLinesDistance2D);
        // GetBestFitLineByP2PDistance(pointsOnCoordBoundingPlanes, out point1Index, out point2Index, out slope);

        // Get hullVerts2D
        CalculationHelper.FindBoundingBox2D(hullVerts2D, slope2DBP, out refPoints.boxCenter2D, out height, out width);
        float centerOffset = 0f;
        if (!flat)
        {
            CalculationHelper.GetDepthAlongNormal(points, planes["AdjustedBP"].normal, out _, out _, out centerOffset);
        }
        refPoints.boxCenter = planes["AdjustedBP"].MatTo3D(refPoints.boxCenter2D) + planes["AdjustedBP"].normal; // NOTE: no need to + center
        refPoints.outlineCenter = planes["AdjustedBP"].MatTo3D(CalculationHelper.Vector2OutlineCenter(hullVerts2D));
        refPoints.centroidHull = planes["AdjustedBP"].MatTo3D(CalculationHelper.Vector2Centroid(hullVerts2D));
        refPoints.centroid3DHull = CalculationHelper.Vector3SurfaceAreaCentroid(hull3D.verts, hull3D.tris, center);
        Debug.Log(hullVerts2D.Count);
        Debug.Log("centroidHull = " + refPoints.centroidHull);
        drawManager.bestPlaneBPGameObject.transform.rotation = Quaternion.LookRotation(planes["AdjustedBP"].normal, planes["AdjustedBP"].matTo3D.MultiplyVector(new Vector2(1f, slope2DBP)));
        drawManager.bestPlaneBPGameObject.transform.localScale =
            new Vector3 (
                width
                / Mathf.Abs(drawManager.bestPlaneBPGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.x)
                / 2.0f,
                height
                / Mathf.Abs(drawManager.bestPlaneBPGameObject.GetComponentInChildren<MeshFilter>().mesh.bounds.extents.x)
                / 2.0f,
                1f
            );
        drawManager.bestPlaneBPGameObject.transform.position = planes[displayedProjection2Name].MatTo3D(refPoints.boxCenter2D);

        Instantiate(drawManager.referencePoint, center, Quaternion.identity, null).name = "Stroke Center";
        Instantiate(drawManager.referencePointRed, refPoints.boxCenter, Quaternion.identity, null).name = "3D Center";
        Instantiate(drawManager.referencePointBlue, refPoints.outlineCenter, Quaternion.identity, null).name = "Outline Center";
        Instantiate(drawManager.referencePointYellow, refPoints.centroidHull, Quaternion.identity, null).name = "Centroid Hull";
        Instantiate(drawManager.referencePointYellow, refPoints.centroid3DHull, Quaternion.identity, null).name = "Centroid 3DHull";
    }

    // FIXME: calculate pointsOnCoords immediately after getting matrices in each thread
    private void ProjectStrokesLr(string protocol, List<LineRenderer> strokesOnCoords, bool toBeDisplayed) {
        var plane = planes[protocol];

        for (int i = 0; i < strokes.Count; ++i) {
            List<Vector3> points = strokes[i];
            // LineRenderers
            if (toBeDisplayed) {
                strokesOnCoords[i].positionCount = points.Count;
            }
            for (int j = 0; j < points.Count; ++j) {
                if (toBeDisplayed) {
                    strokesOnCoords[i].SetPosition(j, (Vector3)plane.strokesOn2DCoords[i][j]); // Vector2 can be implicitly converted to Vector3
                }
            }

            // NOTE: comment out if you don't want to project
            // TODO: Specify which plane to project on
            // TODO: move out of thread
            /*
            if (drawManager.projectSketch && projectedPlane3DName == protocol) {
                // FIXME: do I have to reference strokesLR in DrawManager?
                strokesLR[i].SetPositions(plane.strokesProjected[i].ToArray()); // decision
            }
            */
        }
    }
    
    private void ProjectStrokesOnCoord(ref ProjectionPlane plane) {
        plane.strokesOn2DCoords = new List<List<Vector2>> (new List<Vector2>[strokes.Count]);
        plane.strokesProjected = new List<List<Vector3>> (new List<Vector3>[strokes.Count]);
        for (int i = 0; i < strokes.Count; ++i) {
            List<Vector3> points = strokes[i];
            plane.strokesProjected[i] = new List<Vector3> (new Vector3[points.Count]);
            plane.strokesOn2DCoords[i] = new List<Vector2> (new Vector2[points.Count]);
            
            for (int j = 0; j < points.Count; ++j) {
                // Project to 2D Coord
                plane.strokesOn2DCoords[i][j] = (Vector2)plane.MatTo2D(points[j]);
                // Project to plane in 3D
                plane.strokesProjected[i][j] = plane.MatOrthoProj(points[j]); // strokesSVD[i][j] = matTo3DSVD.MultiplyPoint3x4((Vector3)strokesOnCoord1[i][j]);
            }
        }

        plane.pointsOnCoord = 
            plane.strokesOn2DCoords.Aggregate(
                new List<Vector2> (),
                (p, n) => p.Concat(n).ToList() );

        plane.hullVerts2D = CalculationHelper.GetConvexHull2D(plane.pointsOnCoord);
    }

    List<List<Vector2>> TrimStrokes(List<List<Vector2>> strokes, float slope, Vector2 newCenter2D, float height, float width) { // for better recognition
        Vector2 massDir = (Vector2.zero - newCenter2D).normalized;
        Vector2 leftDir = Vector3.Cross(new Vector3(0f,0f,1f), massDir).normalized;
        Vector2 lowerBound = newCenter2D + massDir * (height/2f - width * 3f); // 1 : 3
        List<(Vector2 start, Vector2 end)> lineSegments = CalculationHelper.GetLineSegments(strokes);
        var trimmedLineSegments =
            lineSegments.
            Where(
                seg => 
                Vector3.Dot(Vector3.Cross(seg.start-lowerBound, leftDir), new Vector3(0f,0f,1f)) > 0
                &&
                Vector3.Dot(Vector3.Cross(seg.end-lowerBound, leftDir), new Vector3(0f,0f,1f)) > 0
            ).ToList();

        List<List<Vector2>> trimmedStrokes = new List<List<Vector2>>();
        if (trimmedLineSegments.Count != 0) {
            trimmedStrokes.Add(new List<Vector2>{trimmedLineSegments[0].start, trimmedLineSegments[0].end});
            for (int i = 1; i < trimmedLineSegments.Count; ++i) {
                if (trimmedLineSegments[i].start == trimmedStrokes.Last().Last()) {
                    trimmedStrokes.Last().Add(trimmedLineSegments[i].end);
                } else {
                    trimmedStrokes.Add(new List<Vector2>{trimmedLineSegments[i].start, trimmedLineSegments[i].end});
                }
            }
        }

        return trimmedStrokes;
    }

    string GetFinalResult(string[] resultSVD, string[] resultBoundingPlanes) {
        for (int i = 0; i < resultSVD[0].Length; ++i)
            if (resultSVD[0][i] == ' ')
                return resultSVD[0].Substring(0, i);
        return resultSVD[0];
    }

    private void GetBestPlaneByAvgLookDirection() {
        center = CalculationHelper.GetCenter(points);
        ProjectionPlane plane = new ProjectionPlane();
        plane.protocol       = "AvgLookDir";
        plane.normal         = -avgLookDirection;
        plane.basis2         = Vector3.ProjectOnPlane(avgTopDirection, plane.normal).normalized; // y: pointing to the top
        plane.basis1         = Vector3.Cross(plane.normal, plane.basis2).normalized; // x
        _GetTransformMatrices(center, ref plane);
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol ] = plane;
        }
    }

    private void _GetBestPlaneByBoundingBoxAnalysis()
    {
        ProjectionPlane plane = new ProjectionPlane();
        
        if (flat)
        {
            lock (planes)
            {
                plane = planes["AdjustedBP"];
                plane.protocol   = "BoundingBoxAnalysis";
                planes[plane.protocol] = plane;
            }
            return;
        }
        plane.protocol   = "BoundingBoxAnalysis";
        GetBestPlaneByBoundingBoxAnalysis(points, avgLookDirection, avgTopDirection, out center, out Vector3 normal, out Vector3 basis2);
        plane.normal = normal;
        plane.basis2 = basis2;
        plane.basis1 = Vector3.Cross(plane.normal, plane.basis2).normalized; // x
        _GetTransformMatrices(center, ref plane);
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
    }
    
    
    // FIXME: Big prob: View angle slanted down even if the plane looks upright for adjusted bp and bb analysis, only (surprisingly) avgtorque produces the desired outcome

    private void GetBestPlaneByBoundingBoxAnalysis(List<Vector3> points, Vector3 avgLookDirection,
        Vector3 avgTopDirection, out Vector3 center, out Vector3 normal, out Vector3 top)
    {
        center = CalculationHelper.GetCenter(points);
        float slope2DBP;
        List<Vector2> pointsOnCoord;
        Vector3 ogNormal;
        lock (planes) ;
        {
            pointsOnCoord = planes["AdjustedBP"].pointsOnCoord;
            ogNormal = planes["AdjustedBP"].normal;
        }
        CalculationHelper.GetBestFitLineByBoundingLines(pointsOnCoord, ref hullVerts2D, out drawManager.point1Index, 
            out drawManager.point2Index, out slope2DBP, out drawManager.BoundingLinesDistance2D);
        Vector3 basis1 = new Vector3(1f, slope2DBP).normalized;
        Vector3 basis2 = new Vector3(slope2DBP, -1f).normalized;
        Vector2 newCenter2D;
        float height, width;
        CalculationHelper.FindBoundingBox2D(pointsOnCoord, 
                        slope2DBP, out newCenter2D, out height, out width);
        float centerOffset;
        float depth = CalculationHelper.GetDepthAlongNormal(points, ogNormal, out _, out _, out centerOffset);
        
        Vector3[] faceNormals =
        {
            ogNormal,
            -ogNormal,
            basis1 * height,
            -basis1 * height,
            basis2 * width,
            -basis2 * width,
        };

        int[] counts = { 0, 0, 0, 0, 0, 0 };

        Matrix4x4 mat = CalculationHelper.GetTriMappingMatrix3D (
            ogNormal * depth / 2f,
            basis1 * height / 2f,
            basis2 * width / 2f,
            ogNormal,
            basis1 * height,
            basis2 * width
        );

        Vector3 o;
        lock (planes)
        {
            o = planes["AdjustedBP"].MatTo3D(newCenter2D) - center + centerOffset * ogNormal;
        }
        List<Vector3> newPoints = (from point in points select (point + o)).ToList();
        newPoints = (from p in newPoints select mat.MultiplyPoint3x4(p)).ToList();

        foreach (var point in newPoints)
        {
            float max = -Mathf.Infinity;
            int maxIdx = 0;
            for (int i = 0; i < 6; ++i)
            {
                float dot = Vector3.Dot(point, faceNormals[i]);
                if (dot > max)
                {
                    max = dot;
                    maxIdx = i;
                }
            }

            counts[maxIdx]++;
        }
        
        // ANALYZE COUNTS:

        var sorted = counts
            .Select((x, i) => new KeyValuePair<int, int>(x, i))
            .OrderBy(x => x.Key)
            .ToList();
        List<int> sortedCounts = sorted.Select(x => x.Key).ToList();
        List<int> sortedIdx = sorted.Select(x => x.Value).ToList();
        // max would be sortedIdx[5]
        int bar = sortedCounts[5] * 2 / 3;
        int lowSurfaceCount = 0; // also when high surfaces start
        
        for (int i = 0; i < 6; ++i)
        {
            lowSurfaceCount = i;
            if (sortedCounts[i] > bar) break;
        }
        
        // DECIDE NORMAL:
        // TODO: top direction, etc. has to be determined too
        int maxCountIdx = sortedIdx[5];
        int maxCountFaceOppositeIdx = (maxCountIdx % 2 == 0) ? (maxCountIdx + 1 ): (maxCountIdx - 1);
        switch (lowSurfaceCount)
        {
            // 1. Only one face high: that one faces up
            // normal: "the highest count one among the four sideways faces?"
            // top: "
            case 5:

                Debug.Log("Only One High Count Face");
                top = (Vector3.Dot(avgTopDirection, faceNormals[maxCountIdx]) > 0)
                    ? faceNormals[maxCountIdx]
                    : faceNormals[maxCountFaceOppositeIdx];
                if (sortedIdx[4] == maxCountFaceOppositeIdx)
                {
                    normal = faceNormals[sortedIdx[3]];
                }
                else
                {
                    normal = faceNormals[sortedIdx[4]];
                }
                
                Debug.Log("normal = " + normal);

                break;

            // 2. Two faces high...
            case 4:

                Debug.Log("Two High Count Faces");
                // opposite: "choose the highest count one among the four sideways faces?"
                if (sortedIdx[4] == maxCountFaceOppositeIdx)
                {
                    Debug.Log("Faces Opposite");
                    top = (Vector3.Dot(avgTopDirection, faceNormals[maxCountIdx]) > 0)
                        ? faceNormals[maxCountIdx]
                        : faceNormals[maxCountFaceOppositeIdx];
                    normal = faceNormals[sortedIdx[3]];
                }
                // not opposite: "average out the two?"
                // e.g. book opened to 
                else
                {
                    Debug.Log("Faces Not Opposite");
                    normal = (faceNormals[sortedIdx[4]] + faceNormals[sortedIdx[5]]).normalized;
                    top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                }

                break;

            // 3. Three faces high
            // e.g. closed book
            case 3:

                Debug.Log("Three high count surfaces");
                // 3-1. They form a U (two of them are opposites
                if (sortedIdx[4] == maxCountFaceOppositeIdx || sortedIdx[3] == maxCountFaceOppositeIdx ||
                    _AreOppositeFaces(sortedIdx[4], sortedIdx[3]))
                {
                    int side1Idx, side2Idx, midIdx;
                    if (sortedIdx[4] == maxCountFaceOppositeIdx)
                    {
                        side1Idx = maxCountIdx;
                        side2Idx = sortedIdx[4];
                        midIdx = sortedIdx[3];
                    }
                    else if (sortedIdx[3] == maxCountFaceOppositeIdx)
                    {
                        side1Idx = maxCountIdx;
                        side2Idx = sortedIdx[3];
                        midIdx = sortedIdx[4];
                    }
                    else
                    {
                        side1Idx = sortedIdx[4];
                        side2Idx = sortedIdx[3];
                        midIdx = maxCountIdx;
                    }

                    normal = (faceNormals[side1Idx] - faceNormals[midIdx] +
                              Vector3.Cross(faceNormals[side1Idx], faceNormals[midIdx]).normalized).normalized;
                    top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                }
                // 3-2. They are in one corner
                else
                {
                    normal = (faceNormals[sortedIdx[5]] - faceNormals[sortedIdx[4]] + faceNormals[sortedIdx[3]])
                        .normalized;
                    top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                }

                break;
            // 4. Four faces high
            case 2:
                // 4-1. Hollow faces are opposites:
                // up: either one of the hollow faces
                // normal: avergae out two of the high faces who are neighboring plus up
                // TODO: delete strokes near the away faces?
                // e.g. a stack of paper
                if (_AreOppositeFaces(sortedIdx[0], sortedIdx[1]))
                {
                    int side1Idx, side2Idx, up;
                    int upIdx = (Vector3.Dot(faceNormals[sortedIdx[0]], avgTopDirection) > 0) ? sortedIdx[0] : sortedIdx[1];
                    if (sortedIdx[4] == maxCountFaceOppositeIdx)
                    {
                        side1Idx = maxCountIdx;
                        side2Idx = sortedIdx[3];
                    }
                    else
                    {
                        side1Idx = maxCountIdx;
                        side2Idx = sortedIdx[4];
                    }

                    normal = (faceNormals[sortedIdx[0]] + faceNormals[sortedIdx[1]] + faceNormals[upIdx]).normalized;
                    top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                }
                // 4-2. Hollow faces are adjacent:
                // normal: average of: 1. & 2. the hollow faces, 3. their cross product
                else
                {
                    normal = (faceNormals[sortedIdx[0]] + faceNormals[sortedIdx[1]] +
                             Vector3.Cross(faceNormals[sortedIdx[0]], faceNormals[sortedIdx[1]])).normalized;
                    top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                }
                break;
            // 5. Five faces high
            // up: either the hollow face or its opposite
            // normal: avergae out two of the side faces who are neighboring plus the up face
            case 1:
                normal = ogNormal;
                top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                
                break;

            // 6. All equally high: do nothing
            case 0:
                normal = ogNormal;
                top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                break;

            default:
                normal = ogNormal;
                top = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized;
                break;
        }
    }

    private bool _AreOppositeFaces(int a, int b)
    {
        return (a - b == 1 && b % 2 == 0) || (a - b == -1 && a % 2 == 0);
    }

    private void _GetBestPlaneByAvgTorque()
    {
        var plane = new ProjectionPlane();
        plane.protocol = "AvgTorque";
        GetBestPlaneByAvgTorque(strokes, avgLookDirection, avgTopDirection, out center, out var normal);
        plane.normal = normal;
        plane.basis2     = Vector3.ProjectOnPlane(avgTopDirection, plane.normal).normalized; // y: pointing to the top
        plane.basis1     = Vector3.Cross(plane.normal, plane.basis2).normalized; // x
        _GetTransformMatrices(center, ref plane);
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
    }

    private void GetBestPlaneByAvgTorque(List<List<Vector3>> strokes, Vector3 avgLookDirection, Vector3 avgTopDirection,
        out Vector3 center, out Vector3 normal)
    {
        points = strokes.SelectMany(i => i).ToList();
        center = CalculationHelper.GetCenter(points);
        
        Vector3 avgTorque = Vector3.zero;
        int nCorners = 0;
        foreach (var stroke in strokes)
        {
            for (int i = 1; i < stroke.Count() - 1; ++i)
            {
                nCorners++;
                Vector3 torque = Vector3.Cross(stroke[1] - stroke[0], stroke[1] - stroke[2]);
                avgTorque =
                    (avgTorque * (float)(nCorners - 1) + Mathf.Sign(Vector3.Dot(avgTorque, torque)) * avgTorque) /
                    (float)nCorners;
            }
        }
        normal = avgTorque.normalized;
    }

    private void _GetBestPlaneByPairedDistance() {
        var plane = new ProjectionPlane();
        plane.protocol   = "PairedDistance";
        GetBestPlaneByPairedDistance(points, avgLookDirection, avgTopDirection, out center, out var normal);
        plane.normal     = normal;
        plane.basis2     = Vector3.ProjectOnPlane(avgTopDirection, plane.normal).normalized; // y: pointing to the top
        plane.basis1     = Vector3.Cross(plane.normal, plane.basis2).normalized; // x
        _GetTransformMatrices(center, ref plane);
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
        Debug.Log("PairedDistance plane generated.");
    }

    private void GetBestPlaneByPairedDistance(List<Vector3> points, Vector3 avgLookDirection, Vector3 avgTopDirection, 
        out Vector3 center, out Vector3 normal) {
        /* TODO: Less than two points? */
        center = CalculationHelper.GetCenter(points);
        Vector3 center_temp = center;
        int p1, p2;
        GetTwoFarthestApartPoints3D(points, out p1, out p2);
        Vector3 dir1 = points[p2] - points[p1];
        /* FIXME: Does this work? */
        List<Vector3> flattenedPoints = (from point in points select (Vector3.ProjectOnPlane(dir1, point - center_temp) + center_temp)).ToList();
        int q1, q2;
        GetTwoFarthestApartPoints3D(flattenedPoints, out q1, out q2);
        Vector3 dir2 = points[q2] - points[q1];
        normal  = Vector3.Cross(dir1, dir2).normalized;
        normal *= -Mathf.Sign(Vector3.Dot(normal, avgLookDirection));
    }

    // base: https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/LinearAlgebra/Factorization/Svd.cs
    // user end: https://github.com/mathnet/mathnet-numerics/blob/master/src/Numerics/LinearAlgebra/Complex32/DenseMatrix.cs
    private void _GetBestPlaneBySvd() {

        ProjectionPlane plane = new ProjectionPlane();
        plane.protocol              = "SVD";
        Vector3 normal;
        GetBestPlaneBySVD(points, avgLookDirection, avgTopDirection, out center, out normal);
        plane.normal                = normal;
        plane.basis2                = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized; // y: pointing to the top
        plane.basis1                = Vector3.Cross(normal, plane.basis2).normalized; // x
        _GetTransformMatrices(center, ref plane);
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
    }

    static public void GetBestPlaneBySVD(List<Vector3> points, Vector3 avgLookDirection, Vector3 avgTopDirection, out Vector3 center, out Vector3 normal){
        
        int count = points.Count;
        center = CalculationHelper.GetCenter(points);

        Vector3 basis1, basis2;
        
        if (points.Count <= 1) {
            normal = -Vector3.forward.normalized;    // doesn't matter if called as a static
            basis1 = Vector3.Cross(Vector3.Cross(normal, Vector3.up), normal);
            basis2 = Vector3.Cross(normal, basis1);
        }
        else if (points.Count == 2) {
            var v   = points[1]-points[0];
            normal  = Vector3.Cross(v, Vector3.Cross(-Vector3.forward, v)).normalized;
            basis1  = points[1]-points[0];
            basis1 *= Mathf.Sign(Vector3.Dot(basis1, normal));
            basis2  = Vector3.Cross(normal, basis1);
        } else {
            // points.Count >= 3
            var matrix = new DenseMatrix(3, count);
            for (var i = 0; i < count; i++) {
                matrix[0, i] = points[i].x - center.x;
                matrix[1, i] = points[i].y - center.y;
                matrix[2, i] = points[i].z - center.z;
            }
            var f = matrix.Svd(); // factorization
            basis1 = new Vector3((float)f.U[0, 0], (float)f.U[1, 0], (float)f.U[2, 0]);
            basis2 = new Vector3((float)f.U[0, 1], (float)f.U[1, 1], (float)f.U[2, 1]);
            normal = Vector3.Cross(basis1, basis2).normalized;
        }
        
        normal *= -Mathf.Sign(Vector3.Dot(normal, avgLookDirection));
    }

    // Originally used alglib: https://www.alglib.net/
    /*
    void GetBestPlaneBySVD_alglib_version(List<Vector3> points, out Vector3 centroid, out Vector3 basis1, out Vector3 basis2, out Vector3 normal) {
        int count = points.Count;
        centroid = GetCentroid(points);
        var matrix = new DenseMatrix(3, count);
        // var dataMat = new double[3, count];
        for (var i = 0; i < count; i++) {
            dataMat[0, i] = points[i].x - centroid.x;
            dataMat[1, i] = points[i].y - centroid.y;
            dataMat[2, i] = points[i].z - centroid.z;
        }
        double[] w = new double[3];
        double[,] u = new double[3,3];
        double[,] vt = new double[(int)count, (int)count];

        bool a = alglib.rmatrixsvd(dataMat, 3, (int)count, 1, 0, 2, out w, out u, out vt);

        basis1 = new Vector3((float)u[0, 0], (float)u[1, 0], (float)u[2, 0]);
        basis2 = new Vector3((float)u[0, 1], (float)u[1, 1], (float)u[2, 1]);
        normal = Vector3.Cross(basis1, basis2);
    }
    */

    private void _GetBestPlaneByBoundingPlanes() {
        ProjectionPlane plane = new ProjectionPlane();
        plane.protocol = "BoundingPlanes";
        GetBestPlaneByBoundingPlanes(points, out center, out Vector3 normal, out BPDistance3D, out BPRefPoint, out bestTrisIndex, out bestVertIndex);
        Debug.Log("GetBestPlaneByBoundingPlanes returned.");
        plane.normal = normal;
        plane.basis2 = Vector3.ProjectOnPlane(avgTopDirection, normal).normalized; // y: pointing to the top
        plane.basis1 = Vector3.Cross(plane.normal, plane.basis2).normalized; // x
        Debug.Log("_GetTransformMatrices called in _GetBestPlaneByBoundingPlanes.");
        _GetTransformMatrices(center, ref plane);
        Debug.Log("ProjectStrokesOnCoord called in _GetBestPlaneByBoundingPlanes.");
        ProjectStrokesOnCoord(ref plane);
        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
        Debug.Log("BoundingPlanesFinished");
    }

    public void GetBestPlaneByBoundingPlanes(
                    List<Vector3> points, 
                    out Vector3 center, 
                    out Vector3 normal, 
                    out float depth, 
                    out Vector3 BPRefPoint, 
                    out int bestTrisIndex, 
                    out int bestVertIndex) {
        
        float count = points.Count;
        center = CalculationHelper.GetCenter(points);

        var pointsNoDup = points.Distinct().ToList();
        if (pointsNoDup.Count <= 1) {
            normal = -avgLookDirection;
            BPRefPoint = pointsNoDup[0];
            bestTrisIndex = 0;
            bestVertIndex = 0;
            depth = 0;
            return;
        } else if (pointsNoDup.Count == 2) {
            var v = pointsNoDup[1]-pointsNoDup[0];
            normal = Vector3.Cross(v, Vector3.Cross(-avgLookDirection, v));
            BPRefPoint = pointsNoDup[0];
            bestTrisIndex = 0;
            bestVertIndex = 0;
            depth = 0;
            return;
        }
        
        // 1. Make a Convex Hull

        var calc = new ConvexHullCalculator(); // Generate calculator as object
        try {
            calc.GenerateHull(pointsNoDup, false, ref hull3D.verts, ref hull3D.tris, ref hull3D.normals);
        }
        catch (Exception e)
        {
            // FIXME: coplanar
            Debug.Log(e);
            var v1 = pointsNoDup[1]-pointsNoDup[0];
            var v2 = pointsNoDup[2]-pointsNoDup[0];
            BPRefPoint = pointsNoDup[0];
            normal = Vector3.Cross(v1, v2);
            normal *= -Mathf.Sign(Vector3.Dot(normal, avgLookDirection)); //FIXME:
            for (int i = 1; i < pointsNoDup.Count - 2 && normal == Vector3.zero; ++i) {
                v1 = pointsNoDup[i+1]-pointsNoDup[i];
                v2 = pointsNoDup[i+2]-pointsNoDup[i];
                BPRefPoint = pointsNoDup[i];
                normal = Vector3.Cross(v1, v2);
                normal *= -Mathf.Sign(Vector3.Dot(normal, avgLookDirection));  //FIXME:
            }
            if (normal == Vector3.zero) {
                Debug.Log("Drawing is colinear");
                normal = -avgLookDirection;
            }
            bestTrisIndex = 0;
            bestVertIndex = 0;
            depth = 0;
            return;
        }
        Debug.Log("hull3D.verts.Count = " + hull3D.verts.Count );

        // 2. Find Bounding Planes Using Hull Vertices
        // One of the tris's normal must be the answer because given a point and a plane the line of shortest distance must be orthogonal to the plane
        // Hence one of the tris must be inside one of the bounding planes.
        // Other optimizations?
        // In 2-D we can sort of sort the normals and only deal with tris whose normals are collinear (but opposite direction), what about 3-D?
        float minDistance = float.PositiveInfinity;
        bestTrisIndex = 0;
        bestVertIndex = 0;
        normal = Vector3.zero;
        // NOTE: We are looking for the minimum distance of the maximum distances for a tris
        for (int i = 0; i < hull3D.tris.Count; i = i + 3) {
            // TODO: Are the vertices in tris arranged clock-wise / counter-clockwise???
            Vector3 cross = Vector3.Cross(hull3D.verts[hull3D.tris[i+1]] - hull3D.verts[hull3D.tris[i]], hull3D.verts[hull3D.tris[i+2]] - hull3D.verts[hull3D.tris[i]]);
            if (cross == Vector3.zero) {
                Debug.Log("bad tris");
                continue;
            }
            Plane trisPlane = new Plane (Vector3.Cross(hull3D.verts[hull3D.tris[i+1]] - hull3D.verts[hull3D.tris[i]], hull3D.verts[hull3D.tris[i+2]] - hull3D.verts[hull3D.tris[i]]), hull3D.verts[hull3D.tris[i]]);
            float maxDistanceFromTris = 0;
            int farthestVert = 0;
            Vector3 farthestNormal = Vector3.zero;
            for (int j = 0; j < hull3D.verts.Count; ++j) {
                if (j == hull3D.tris[i] || j == hull3D.tris[i+1] || j == hull3D.tris[i+2]) continue;
                float distance = Mathf.Abs(trisPlane.GetDistanceToPoint(hull3D.verts[j])); // ABS!
                if (maxDistanceFromTris <= distance) {
                    maxDistanceFromTris = distance;
                    farthestVert = j;
                }
            }
            if (maxDistanceFromTris <= minDistance) {
                minDistance = maxDistanceFromTris;
                normal = trisPlane.normal;
                bestTrisIndex = i;
                bestVertIndex = farthestVert;
            }
        }
        BPRefPoint = hull3D.verts[hull3D.tris[bestTrisIndex]];
        depth = minDistance;
        
        normal *= -Mathf.Sign(Vector3.Dot(normal, avgLookDirection));
        Debug.Log("Bounding planes found.");
    }

    private void GetBestPlaneByAdjustedBp() {
        ProjectionPlane plane = new ProjectionPlane();
        lock (planes)
        {
            plane = planes["BoundingPlanes"];
        }
        plane.protocol = "AdjustedBP";

        int p1, p2;
        flat = BPDistance3D / GetTwoFarthestApartPoints3D(points, out p1, out p2) <= 0.25;
        
        // TODO: use other metrics: if there are stroke segments whose directions are near perpendicular then not flat
        
        Debug.Log("Flat? "+ flat);
        if (!flat) {
            Vector3 normal;
            GetAdjustedBoundingPlanes(points, planes["BoundingPlanes"].normal, out normal);
            plane.normal = normal;
        }

        lock (planes)
        {
            planes[plane.protocol] = plane;
        }
    }

    private void GetAdjustedBoundingPlanes(List<Vector3> points, Vector3 normalBP, out Vector3 normal) {

        Vector3 centroid3DHull = CalculationHelper.Vector3SurfaceAreaCentroid(hull3D.verts, hull3D.tris, center);
        Vector3 centroidOffset = center - centroid3DHull;
        normal = Vector3.Cross(centroidOffset, Vector3.Cross(normalBP, centroidOffset));
    }

    static public float GetTwoFarthestApartPoints3D(List<Vector3> points, out int point1, out int point2) {
        float farthestDistance = 0f;
        point1 = 0;
        point2 = 0;
        for (int i = 0; i < points.Count; ++i) {
            for (int j = i + 1; j < points.Count; ++j) {
                if (points[i] != points[j] && Vector3.Distance(points[i], points[j]) >= farthestDistance) {
                    farthestDistance = Vector3.Distance(points[i], points[j]);
                    point1 = i;
                    point2 = j;
                }
            }
        }
        return farthestDistance;
    }


    private void _GetTransformMatrices(Vector3 center, ref ProjectionPlane plane){
        Matrix4x4 matTo2D, matTo3D, matOrthoProj;
        GetTransformMatrices(center, plane.normal, plane.basis1, plane.basis2, out matTo2D, out matTo3D, out matOrthoProj);
        plane.matTo2D       = matTo2D;
        plane.matTo3D       = matTo3D;
        plane.matOrthoProj  = matOrthoProj;
    }

    private void GetTransformMatrices(Vector3 center, Vector3 normal, Vector3 basis1, Vector3 basis2, out Matrix4x4 matTo2D, out Matrix4x4 matTo3D, out Matrix4x4 matOrthoProj){
        // A matrix [basis1 basis2 normal] would be used to go from the bt plane space to standard space, we need the opposite
        // Invert the matix [basis1 basis2 normal]
        // Sol.1
        var translation = center;
        var rotation = Quaternion.LookRotation(-normal, basis2); // negative so that x faces left (z direction doesn't matter), i.e. faces right in the observer's direction
        var scale = Vector3.one;
        matTo3D = Matrix4x4.TRS(translation, rotation, scale);
        matTo2D = matTo3D.inverse;
        matTo2D.SetRow(2, Vector4.zero);
        matOrthoProj = matTo3D * matTo2D;
        
    }
    
}
