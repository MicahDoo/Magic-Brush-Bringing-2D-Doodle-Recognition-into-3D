#define DEBUG
#define GAME_MODE_VR
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
// using TensorFlow;

// IMPORTANT: MultiplyPoint vs MultiplyVector

public interface IPrefabs
{
    LineRenderer strokePrefab {get; set;}
    LineRenderer strokeOnCoordPrefab {get; set;}
    LineRenderer debugStrokePrefabBlue {get; set;}
    LineRenderer debugStrokePrefabYellow {get; set;}
    LineRenderer debugStrokePrefabBlack {get; set;}
    GameObject   bestPlaneSVDPrefab {get; set;}
    GameObject   bestPlaneBoundingPrefab {get; set;}
};

public class DrawManager : MonoBehaviour, IPrefabs
{
    [Header("Settings")]
    public bool saveAsPredrawnSketch = true;
    public bool projectSketch = true;
    public string currentTestSketch;
    public bool currentTestSketchIsFlat;
    
    [Header("Other Attributes")]
    public ObjectGenerator objectGenerator;
    public Recognizer recognizer;
    public CalculationHelper helper;
    // TODO: interface
    [SerializeField]
    private LineRenderer _strokePrefab;
    [SerializeField]
    private LineRenderer _strokeOnCoordPrefab;
    [SerializeField]
    private LineRenderer _debugStrokePrefabBlue;
    [SerializeField]
    private LineRenderer _debugStrokePrefabYellow;
    [SerializeField]
    private LineRenderer _debugStrokePrefabBlack;
    [SerializeField]
    private GameObject   _bestPlaneSVDPrefab;
    [SerializeField]
    private GameObject   _bestPlaneBoundingPrefab;
    public LineRenderer strokePrefab {get {return _strokePrefab;} set {_strokePrefab = value;}}
    public LineRenderer strokeOnCoordPrefab {get {return _strokeOnCoordPrefab;} set {_strokeOnCoordPrefab = value;}}
    public LineRenderer debugStrokePrefabBlue {get {return _debugStrokePrefabBlue;} set {_debugStrokePrefabBlue = value;}}
    public LineRenderer debugStrokePrefabYellow {get {return _debugStrokePrefabYellow;} set {_debugStrokePrefabYellow = value;}}
    public LineRenderer debugStrokePrefabBlack  {get {return _debugStrokePrefabBlack;} set {_debugStrokePrefabBlack = value;}}
    public GameObject   bestPlaneSVDPrefab {get {return _bestPlaneSVDPrefab;} set {_bestPlaneSVDPrefab = value;}}
    public GameObject   bestPlaneBoundingPrefab {get {return _bestPlaneBoundingPrefab;} set {_bestPlaneBoundingPrefab = value;}}

    [NonSerialized]
    public LineRenderer strokeOnCoordSVD;
    [NonSerialized]
    public LineRenderer strokeOnCoordBP;
    public GameObject brush;
    public GameObject rightHand;
    public GameObject leftHand;
    private GameObject brushHand;
    public Transform drawingContainer;
    public Transform pastDrawingContainer;
    public GameObject coordinateSpaceSVD;
    public GameObject coordinateSpaceBoundingPlanes;
    public float planeThresholdRatio = 0.1f; // ratio
    public float lineThresholdRatio = 0.1f; // ratio
    public float planeThresholdAbs = 0.3f;
    public float lineThresholdAbs = 0.3f;
    public float strokeConnectionThreshold = 0.2f;
    public bool strokeSmoothing = true;
    public float pointSpacing = 0.05f;
    public bool flat;
    public MorphLinesGeneric morphLinesManager;

    private Vector3 BPRefPoint;

    [Serializable]
    public struct Object {
        public string name;
        public GameObject[] gameObjects;
    }
    public Object[] objectList;
    // public GameObject ladderPrefab;
    public GameObject referencePoint;
    public GameObject referencePointRed;
    public GameObject referencePointBlue;
    public GameObject referencePointYellow;

    [NonSerialized]
    public GameObject bestPlaneSVDGameObject;
    [NonSerialized]
    public GameObject bestPlaneBPGameObject;

    // move to sp
    public struct Sketch {
        public string finalDecision;
        public string[] resultSVD;
        public string[] resultBoundingPlanes;
        public int[] bitmap;
        public bool flat;
        public List<List<Vector3>> strokes;
        public Vector3 normalSVD;
        public Vector3 normalBoundingPlanes;
        public Vector3 normalBBAnalysis;
        public Vector3 normalPairedDistance;
    }

    private float[] resultedBitmap;
    public float[] ResultedBitmap{
        set{resultedBitmap = value;}
    }
    int[] resultedBitmap256;
    public int[] ResultedBitmap256{
        set{resultedBitmap256 = value;}
    }

    // public TextAsset graphModel;
    // TFGraph graph;
    // TFSession session;
    [System.NonSerialized]
    List<LineRenderer> strokesLR; //strokes line renderer
    List<List<Vector3>> strokes;
    public List<Vector3> allPoints;
    List<List<Vector3>> strokesSVD;
    List<List<Vector3>> strokesBoundingPlanes;
    public List<List<Vector2>> strokesOnCoordSVD; // Data to feed
    public List<List<Vector2>> strokesOnCoordBP; // Data to feed
    public List<Vector2> pointsOnCoordBoundingPlanes;
    public List<Vector2> pointsOnCoordSVD; 
    int i; // number of strokes
    int j; // number of vertices in the current stroke
    int nLooks; // number of looks to average out from
    Vector3 avgLookDirection;
    Vector3 avgTopDirection;
    Vector3 brushPos;
    Camera cam;

    List<SketchProcessor> sketchProcessors = new List<SketchProcessor>();

    public bool isRecognizing;
    public bool isExecuting;
    public bool IsExecuting {
        set {isExecuting = value;}
    }

    public Vector3 center;
    public Vector3 basis1SVD;
    public Vector3 basis2SVD;
    public Vector3 basis1BoundingPlanes;
    public Vector3 basis2BoundingPlanes;
    public Vector3 normal;
    public Vector3 normalSVD;
    public Vector3 normalBoundingPlanes;
    public Vector3 normalAdjustedBP;
    public Vector3 lengthDirection;
    public float slope;
    public float slope2DSVD;
    public float slope2DBoundingPlanes;
    public float yIntercept;
    public float BoundingPlanesDistance3D;
    public float BoundingLinesDistance2D;

    public Matrix4x4 matTo2DSVD;
    public Matrix4x4 matTo2DBP;
    public Matrix4x4 matTo3DSVD;
    public Matrix4x4 matTo3DBP;
    public Matrix4x4 matOrthoProjSVD;
    public Matrix4x4 matOrthoProjBP;

    public List<int> tris;
    public List<Vector3> verts;
    public List<Vector3> normals;
    public int bestTrisIndex;
    public int bestVertIndex;
    public int point1Index;
    public int point2Index;
    public List<Vector2> hullVerts2D;
    public JsonSerializerSettings jsonSettings;
    // string[] resultSVD;
    // string[] resultBP;

    // Non-VR variables
    public float handDistance = 0.5f;
    private Vector3 handDirection;

    public enum DrawStates {
        drawing,
        drawn,
        generatingPlanes,
        planesGenerated,
        idle
    }
    public DrawStates drawState;

    private List<PredrawnSketch> predrawnSketches;

    void Awake () {

    }

    // Start is called before the first frame update
    void Start()
    {

        strokesLR = new List<LineRenderer>();
        strokes = new List<List<Vector3>>();
        allPoints = new List<Vector3>();
        i = 0;
        j = 0;
        nLooks = 0;
        drawState = DrawStates.idle;
        cam = Camera.main;
        center = Vector3.zero;
        normal = Vector3.zero;
        basis1SVD = Vector3.zero;
        basis2SVD = Vector3.zero;
        lengthDirection = Vector3.zero;
        avgLookDirection = Vector3.zero;
        avgTopDirection = Vector3.zero;
        strokesOnCoordSVD = new List<List<Vector2>>();
        tris = new List<int>();
        verts = new List<Vector3>();
        hullVerts2D = new List<Vector2>();
        predrawnSketches = new List<PredrawnSketch>();
        bestTrisIndex = 0;
        bestVertIndex = 0;
        isRecognizing = false;
        brushHand = rightHand;

        // ML
        // runtimeModel = ModelLoader.Load(kerasModel);
        // worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
        // worker = WorkerFactory.CreateWorker(runtimeModel, WorkerFactory.Device.CPU, false);
        // outputLayerName = runtimeModel.outputs[runtimeModel.outputs.Count - 1];

        // classes = System.IO.File.ReadLines(Application.dataPath + "/" + "classes.txt").ToArray();
        // Debug.Log(classes[83]);

        jsonSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
        };
        jsonSettings.Converters.Add(new MyConverter());

        // graph = new TFGraph ();
        // graph.Import(graphModel.bytes);
        // session = new TFSession (graph);

        List<Vector2> v = new List<Vector2> () {
            new Vector2 (0f, 0f),
            new Vector2 (0f, -3f),
            new Vector2 (3f, -3f),
            new Vector2 (0f, 3f)
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (MakeOnePredrawnSketchInput()) {
            Debug.Log("Making One Pre-drawn Sketch...");
            if (predrawnSketches.Count == 0) {
                string filename = Application.dataPath + "/" + "Predrawns.json";
                var jsons = File.ReadAllText(filename)
                            .Split(new string[] {"}{"}, StringSplitOptions.None);
                jsons = String.Join("}|{", jsons)
                        .Split('|');
                foreach (string json in jsons)
                {
                    Debug.Log("Json loaded:" + json);
                    predrawnSketches.Add(JsonConvert.DeserializeObject<PredrawnSketch>(json));
                    Debug.Log("Sketch label: " +  predrawnSketches[predrawnSketches.Count - 1].result);
                }
            }
            foreach (var sketch in predrawnSketches)
            {
                if (sketch.result == currentTestSketch && sketch.flat == currentTestSketchIsFlat) {
                    Debug.Log("Sketch matched. Drawing...");
                    strokes = sketch.strokes;
                    avgLookDirection = sketch.avgLookDirection;
                    avgTopDirection = sketch.avgTopDirection;
                    flat = sketch.flat;
                    var points = strokes.SelectMany(i => i).ToList();
                    var center = CalculationHelper.GetCenter(points);
                    var pos = Camera.main.transform.position + Camera.main.transform.forward * 1f;
                    strokes = strokes.Select(stroke => stroke.Select(point => point - center + pos).ToList()).ToList();
                    Debug.Log("The sketch contains " + strokes.Count + " strokes.");
                    strokesLR = new List<LineRenderer>();
                    foreach (var stroke in strokes) {
                        strokesLR.Add(Instantiate(strokePrefab, new Vector3(0, 0, 0), Quaternion.identity, drawingContainer));
                        strokesLR[strokesLR.Count - 1].positionCount = stroke.Count;
                        strokesLR[strokesLR.Count - 1].SetPositions(stroke.ToArray());
                    }
                    sketchProcessors.Add(gameObject.AddComponent<SketchProcessor>() as SketchProcessor);
                    drawState = DrawStates.generatingPlanes;
                    sketchProcessors[sketchProcessors.Count-1].Init(strokes, strokesLR, sketchProcessors.Count, avgLookDirection, avgTopDirection, this, ref drawState);
                    break;
                }
            }
        }
        // if (isExecuting) {
        //     Debug.Log("Drawing while still executing");
        // }
        #if DEBUG
        Debug.DrawLine(center, center + normalSVD.normalized * 200.0f, Color.blue); //forward
        Debug.DrawLine(center, center + basis1SVD.normalized * 200.0f, Color.red); //
        Debug.DrawLine(center, center + basis2SVD.normalized * 200.0f, Color.green); // up
        Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position - coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1f, slope2DSVD, 0f), coordinateSpaceBoundingPlanes.transform.position + coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1f, slope2DSVD, 0), Color.black);
        if (hullVerts2D.Count >= 3) {
            Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[0],
                           coordinateSpaceBoundingPlanes.transform.position
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[1],
                           Color.magenta);
            Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[1],
                           coordinateSpaceBoundingPlanes.transform.position
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[2],
                           Color.green);
            for (int i = 2; i < hullVerts2D.Count; ++i) {
                if (i%2 == 0) {
                    Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position + coordinateSpaceSVD.transform.rotation * (Vector3)hullVerts2D[i] , coordinateSpaceBoundingPlanes.transform.position + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[(i+1)%hullVerts2D.Count], Color.cyan);
                } else {
                    Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[i] , coordinateSpaceBoundingPlanes.transform.position + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[(i+1)%hullVerts2D.Count], Color.blue);
                }
            }
            // Bounding  lines
            Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position 
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[point1Index]
                         - coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1, slope2DBoundingPlanes, 0) * 10f, 
                           coordinateSpaceBoundingPlanes.transform.position 
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[point1Index] 
                         + coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1, slope2DBoundingPlanes, 0) * 10f, 
                           Color.black);
            Debug.DrawLine(coordinateSpaceBoundingPlanes.transform.position 
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[point2Index] 
                         - coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1, slope2DBoundingPlanes, 0) * 10f, 
                           coordinateSpaceBoundingPlanes.transform.position 
                         + coordinateSpaceBoundingPlanes.transform.rotation * (Vector3)hullVerts2D[point2Index] 
                         + coordinateSpaceBoundingPlanes.transform.rotation * new Vector3(1, slope2DBoundingPlanes, 0) * 10f, 
                           Color.blue);
        }
        Debug.DrawLine(center, center + lengthDirection * 200.0f, Color.black);
        // for (int i = 0; i < tris.Count; i = i + 3) {
        //     Debug.DrawLine(verts[tris[i]], verts[tris[i+1]], Color.cyan);
        //     Debug.DrawLine(verts[tris[i+1]], verts[tris[i+2]], Color.cyan);
        //     Debug.DrawLine(verts[tris[i+2]], verts[tris[i]], Color.cyan);
        // }
        if (tris.Count != 0) {
            Debug.DrawLine(verts[tris[bestTrisIndex]], verts[tris[bestTrisIndex+1]], Color.cyan);
            Debug.DrawLine(verts[tris[bestTrisIndex+1]], verts[tris[bestTrisIndex+2]], Color.cyan);
            Debug.DrawLine(verts[tris[bestTrisIndex+2]], verts[tris[bestTrisIndex]], Color.cyan);
            Debug.DrawLine(verts[tris[bestTrisIndex]], verts[bestVertIndex], Color.magenta);
            Debug.DrawLine(verts[tris[bestTrisIndex+1]], verts[bestVertIndex], Color.magenta);
            Debug.DrawLine(verts[tris[bestTrisIndex+2]], verts[bestVertIndex], Color.magenta);
        }
        // for (int i = 0; i < verts.Count - 1; ++i) {
        //     Debug.DrawLine(verts[i], verts[i+1], Color.cyan);
        // }
        // for (int i = 0; i < allPoints.Count - 1; ++i) {
        //     Debug.DrawLine(allPoints[i], allPoints[i+1], Color.grey);
        // }
        #endif
        #if GAME_MODE_VR
        brushPos = GetBrushPos();
        brush.transform.position = brushPos;
        #endif
        if (drawState == DrawStates.idle || drawState == DrawStates.drawn) {
            if (BrushDownInput()) {
                // Start Drawing
                if (drawState == DrawStates.idle) {
                    strokesLR = new List<LineRenderer>();
                    strokes = new List<List<Vector3>>();
                }
                drawState = DrawStates.drawing;
                strokesLR.Add(Instantiate(strokePrefab, new Vector3(0, 0, 0), Quaternion.identity, drawingContainer));
                strokes.Add(new List<Vector3>());
                brushPos = GetBrushPos();
                Debug.Log("i = " + i);
                strokesLR[i].startWidth = 0.01f;
                strokesLR[i].endWidth = 0.009f;
                strokesLR[i].positionCount = j + 2;
                strokesLR[i].SetPosition(j, brushPos);
                strokes[i].Add(brushPos);
                ++j;
                ++nLooks;
                avgLookDirection = (avgLookDirection * (float)(nLooks - 1) + (brushPos - cam.transform.position)) / (float)nLooks;
                avgTopDirection = (avgTopDirection * (float)(nLooks - 1) + cam.transform.up) / (float)nLooks;
                strokesLR[i].SetPosition(j, brushPos);
                strokes[i].Add(brushPos);
            }
            if (drawState == DrawStates.drawn && DrawFinishInput()) {
                drawState = DrawStates.generatingPlanes;

                sketchProcessors.Add(gameObject.AddComponent<SketchProcessor>() as SketchProcessor);
                sketchProcessors[sketchProcessors.Count-1].Init(strokes, strokesLR, sketchProcessors.Count, avgLookDirection, avgTopDirection, this, ref drawState);
                // Thread t = new Thread(FindPlanes);
                // t.Start();
                /* TODO: Pass everything on to SketchProcessor */
            }
        } else if (drawState == DrawStates.drawing) {
            if (!BrushUpInput()) {
                brushPos = GetBrushPos();
                if (Vector3.Distance(brushPos, strokesLR[i].GetPosition(j-1)) >= pointSpacing) {
                    strokesLR[i].positionCount = j + 2;
                    strokesLR[i].SetPosition(j, brushPos);
                    strokes[i][j] = brushPos;
                    ++j;
                    avgLookDirection = (avgLookDirection * (float)(nLooks - 1) 
                                    + (brushPos - cam.transform.position)) / (float)nLooks;
                    avgTopDirection = (avgTopDirection * (float)(nLooks - 1) 
                                    + cam.transform.up) / (float)nLooks;
                    ++nLooks;
                    strokesLR[i].SetPosition(j, brushPos);
                    strokes[i].Add(brushPos);
                } else {
                    strokes[i][j] = brushPos;
                }
            } else if (!isRecognizing) { // can go on to draw something else when recognizing but make sure not to process it
                // ProcessStroke(strokesLR[i], strokes[i]);
                drawState = DrawStates.drawn;
                i++;
                j = 0;
            }
        } else if (drawState == DrawStates.planesGenerated && !isRecognizing) {
            isRecognizing = true;
            i = 0;
            nLooks = 0;
            SketchProcessor sketchProcessor = sketchProcessors[sketchProcessors.Count - 1];
        }
    }

    public void PlanesGenerated(){
        drawState = DrawStates.planesGenerated;
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

    Vector2 GetAverageDirection2D(List<List<Vector2>> strokes) {
        return strokes.
               Aggregate(
                   Vector2.zero,
                   (p, n) 
                   => 
                   p
                   + n.Aggregate(Vector2.zero, (f, s) => f + s)
                )
                // /
                // (float) strokes.Aggregate((p, n) => p + n.Count) // nonecessary, since the direction remains anyway
                .
                normalized
                ;
    }



    // Input checking methods

    // Y: test one sketch
    // T: test entire batch
    private bool MakeOnePredrawnSketchInput() {
        #if GAME_MODE_VR
            return Input.GetKeyDown(KeyCode.Y); // TODO: VR
        #else
            return Input.GetKeyDown(KeyCode.Y);
        #endif
    }

    private bool TestAllPredrawnSketchesInput() {
        #if GAME_MODE_VR
            return Input.GetKeyDown(KeyCode.T); // TODO: VR
        #else
            return Input.GetKeyDown(KeyCode.T);
        #endif
    }

    private bool BrushDownInput() {
        #if GAME_MODE_VR
            return Input.GetMouseButtonDown(0); // TODO: VR
        #else
            return Input.GetMouseButtonDown(0);
        #endif
    }

    private bool BrushUpInput() {
        #if GAME_MODE_VR
            return Input.GetMouseButtonUp(0); // TODO: VR
        #else
            return Input.GetMouseButtonUp(0);
        #endif
    }

    private bool DrawFinishInput() {
        #if GAME_MODE_VR
            return Input.GetKeyDown(KeyCode.R); // TODO: VR
        #else
            return Input.GetKeyDown(KeyCode.R);
        #endif
    }


    private Vector3 GetBrushPos() {
        #if GAME_MODE_VR
            return brushHand.transform.position; // TODO: VR
        #else
            handDirection = (cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, cam.nearClipPlane)) - cam.transform.position).normalized;
            return cam.transform.position + handDirection * handDistance;
        #endif
    }

    string GetFinalResult(string[] resultSVD, string[] resultBoundingPlanes) {
        for (int i = 0; i < resultSVD[0].Length; ++i)
            if (resultSVD[0][i] == ' ')
                return resultSVD[0].Substring(0, i);
        return resultSVD[0];
    }

    public static Vector2 From3DTo2D(Vector3 v, double[,] m) {
        return new Vector2((float)(v.x * m[0, 0] + v.y * m[0, 1] + v.z * m[0,2]), 
                           (float)(v.x * m[1, 0] + v.y * m[1, 1] + v.z * m[1,2]));
    }

    public static Vector3 From2DTo3D(Vector2 v, double[,] m) {
        return new Vector3((float) (v.x * m[0,0] + v.y * m[0,1]), 
                           (float) (v.x * m[1,0] + v.y * m[1,1]), 
                           (float) (v.x * m[2,0] + v.y * m[2,1]));
    }

    public void ExportText(string filename, string text, bool append = false) {
        TextWriter tw = new StreamWriter(filename, append);
        tw.Write(text);
        tw.Close();
    }

    private void ProcessStroke(LineRenderer strokeLR, List<Vector3> stroke) {

        int point1, point2;
        float farthestDistance  = SketchProcessor.GetTwoFarthestApartPoints3D(stroke, out point1, out point2);
        float planeThreshold    = Mathf.Min(planeThresholdRatio * farthestDistance, planeThresholdAbs);
        float lineThreshold     = Mathf.Min(lineThresholdRatio * farthestDistance, lineThresholdAbs);
        
        List<Vector3> strokePoints = new List<Vector3> (stroke); //temp
        SketchProcessor.GetBestPlaneBySVD(strokePoints, avgLookDirection, avgTopDirection, out center, out normal);
        Plane plane = new Plane(normal, center);
        for (int i = 0; i < strokePoints.Count; i++) {
            // NOTE: Can't use matOrthoProjSVD yet, unless I create one now?
            if (plane.GetDistanceToPoint(strokePoints[i]) > planeThreshold) return; // a point went past the threashold
            strokePoints[i] = plane.ClosestPointOnPlane(strokePoints[i]);
        }
        stroke = strokePoints;

        // 1. Use the line going from the start to the end
        // 2. Or use the regression line
        // a. Use the starting point or b. use center (starting point sounds better)
        bool isAStraightLine = true;
        Vector3 startingPoint = strokePoints[0];
        Vector3 direction = (strokePoints[strokePoints.Count-1] - startingPoint).normalized;
        for (int i = 0; i < strokePoints.Count; i++) {
            Vector3 p = strokePoints[i] - startingPoint;
            Vector3 shortestPath = p - Mathf.Abs(Vector3.Dot(p, direction)) * direction;
            float distance = shortestPath.magnitude;
            strokePoints[i] = strokePoints[i] - shortestPath;
            if (distance > lineThreshold) {
                isAStraightLine = false;
                break; // needs morph
            }
        }
        // by this point strokePoints stores the points if they were on a line
        if (isAStraightLine) {
            stroke = strokePoints;
        }
        // morphLinesManager.MorphTo(strokeLR, stroke);
        strokeLR.positionCount = stroke.Count;
        strokeLR.SetPositions(stroke.ToArray());
    }

    void GetTwoFarthestApartPoints2D(List<Vector2> points, out int point1, out int point2) {
        float farthestDistance = 0f;
        point1 = 0;
        point2 = 0;
        for (int i = 0; i < points.Count; ++i) {
            for (int j = i + 1; j < points.Count; ++j) {
                if (points[i] != points[j] && Vector2.Distance(points[i], points[j]) >= farthestDistance) {
                    farthestDistance = Vector2.Distance(points[i], points[j]);
                    point1 = i;
                    point2 = j;
                }
            }
        }
    }

};

public class MyConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(List<Vector3>)
            || objectType == typeof(Vector3)
            || objectType == typeof(string[])
            || objectType == typeof(float[])
            || objectType == typeof(int[]);
    }
    public override object ReadJson(JsonReader reader, Type objectType, object existingValues, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteRawValue(JsonConvert.SerializeObject(value, Formatting.None));
    }
}