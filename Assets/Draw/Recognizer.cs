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

public class Recognizer : MonoBehaviour
{
    public NNModel kerasModel;
    public DrawManager drawManager;
    public bool saveBitmap;
    public CalculationHelper helper;

    private Model runtimeModel;
    private string outputLayerName;
    private IWorker worker;
    private string[] classes;

    [System.NonSerialized]
    public float[] resultedBitmap;
    public int[] resultedBitmap256;
    public string[] bitmapsInString;
    private string[] result;


    void Awake() {
        Debug.Log("Hey!");
    }

    void Start() {
        runtimeModel = ModelLoader.Load(kerasModel);
        outputLayerName = runtimeModel.outputs[runtimeModel.outputs.Count - 1];
        classes = System.IO.File.ReadLines(Application.dataPath + "/" + "classes.txt").ToArray();
        bitmapsInString = new string[2];
    }

    public IEnumerator DoodleRecognition(List<List<Vector2>> strokes, string[] result, SketchProcessor sketchProcessor, int index) {
        print("DoodleRecognition: strokes.Count = " + strokes.Count);
        print("strokes[0].Count = " + strokes[0].Count);
        resultedBitmap = GetBitmap(strokes, index);

        // var runner = session.GetRunner ();
        // runner.AddInput (graph["input"][0], bitmap);
        // runner.Fetch (graph["dense_3"][0]);
        // float[,] recurrent_tensor = runner.Run () [0].GetValue () as float[,];
        // Debug.Log(recurrent_tensor);

        // [n, h, w, t] (batch, height, width, channels)
        Tensor inputTensor = new Tensor(1, 28, 28, 1, resultedBitmap); 
        worker = WorkerFactory.CreateWorker(runtimeModel, WorkerFactory.Device.CPU, false);

        Debug.Log("Recognition started.");
        Tensor outputTensor = worker.Execute(inputTensor).PeekOutput(outputLayerName);
        yield return new WaitForCompletion(outputTensor);
        Debug.Log("Recognition complete.");

        List<float> probs = new List<float>(outputTensor.ToReadOnlyArray());
        var sorted = probs
                    .Select((x, i) => new KeyValuePair<float, int>(x, i))
                    .OrderByDescending(x => x.Key)
                    .ToList();
        List<float> probsSorted = sorted.Select(x => x.Key).ToList();
        List<int> idxSorted = sorted.Select(x => x.Value).ToList();
        for (int i = 0; i < result.Length; ++i) {
            result[i] = classes[idxSorted[i]] + " " + probsSorted[i].ToString();
        }
        // outputTensor?.Dispose();
        worker?.Dispose();
        Debug.Log("Worker Disposed");

        sketchProcessor.IsExecuting = false;
    }

    float[] GetBitmap(List<List<Vector2>> strokes, int index) {
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = -float.PositiveInfinity, maxY = -float.PositiveInfinity;
        float[] bitmap = new float[28 * 28];
        foreach (List<Vector2> stroke in strokes) {
            foreach (Vector2 point in stroke) {
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.y > maxY) maxY = point.y;
            }
        }
        float scale;
        float xOffset;
        float yOffset;
        if (maxX - minX > maxY - minY) {
            scale = 26.0f / (maxX - minX);
            xOffset = 0.0f;
            yOffset = ((maxX - minX) - (maxY - minY)) / 2f * scale;
        } else {
            scale = 26.0f / (maxY - minY);
            xOffset = ((maxY - minY) - (maxX - minX)) / 2f * scale;
            yOffset = 0f;
        }
        // List<List<Vector2>> strokesCompressed = new List<List<Vector2>>(strokes);
        // for (int i = 0; i < strokesCompressed.Count; ++i) {
        //         strokesCompressed[i][0] = new Vector2((maxX - strokesCompressed[i][0].x) * scale + 1.0f, (maxY - strokesCompressed[i][0].y) * scale + 1.0f);
        //     for (int j = 1; j < strokesCompressed[i].Count; ++j) {
        //         strokesCompressed[i][j] = new Vector2((maxX - strokesCompressed[i][j].x) * scale + 1.0f, maxY - strokesCompressed[i][j].y) * scale + 1.0f);
        //         lastPoint = strokesCompressed[i][j-1];
        //         currPoint = strokesCompressed[i][j];
        //         xDiff = Mathf.Abs(currPoint.x - lastPoint.x);
        //         yDiff = Mathf.Abs(currPoint.y - lastPoint.y);
        //         if (xDiff > 0.5f || yDiff > 0.5f) {
        //             if (xDiff > y Diff) {
                        
        //             } else {

        //             }
        //         }
        //     }
        // }

        foreach (List<Vector2> stroke in strokes) {
            for (int i = 1; i < stroke.Count; ++i) {
                Vector2 endPoint = new Vector2((maxX - stroke[i].x) * scale + 1.0f + xOffset, (stroke[i].y - minY) * scale + 1.0f + yOffset);
                Vector2 startPoint = new Vector2((maxX - stroke[i-1].x) * scale + 1.0f + xOffset, (stroke[i-1].y - minY) * scale + 1.0f + yOffset);
                // Debug.Log ("from " + startPoint + " to " + endPoint);
                int smallX, bigX, smallY, bigY;
                if (endPoint.x > startPoint.x) {
                    smallX = (int)Mathf.Floor(startPoint.x)-1;
                    bigX = Mathf.Min((int)Mathf.Floor(endPoint.x)+1, 27);
                } else {
                    smallX = (int)Mathf.Floor(endPoint.x)-1;
                    bigX = Mathf.Min((int)Mathf.Floor(startPoint.x)+1, 27);
                }
                if (endPoint.y > startPoint.y) {
                    smallY = (int)Mathf.Floor(startPoint.y)-1;
                    bigY = Mathf.Min((int)Mathf.Floor(endPoint.y)+1, 27);
                } else {
                    smallY = (int)Mathf.Floor(endPoint.y)-1;
                    bigY = Mathf.Min((int)Mathf.Floor(startPoint.y)+1, 27);
                }
                for (int ix = smallX; ix <=  bigX; ++ix) {
                    for (int iy = smallY; iy <= bigY; ++iy) {
                        Vector2 closestPoint = CalculationHelper.FindNearestPointOnLineSegment(startPoint, endPoint, new Vector2((float)ix + 0.5f, (float)iy + 0.5f));
                        float depth = 1.2f - (Mathf.Pow(closestPoint.x - ((float)ix + 0.5f), 2.0f) + Mathf.Pow(closestPoint.y - ((float)iy + 0.5f), 2.0f));
                        depth = Mathf.Min(depth, 1.0f);
                        // Debug.Log(ix.ToString() + " " + iy.ToString() + " " + "depth: " + depth);
                        bitmap[(27-iy) * 28 + (27-ix)] = Mathf.Max(bitmap[(27-iy) * 28 + (27-ix)], depth);
                    }
                }
            }
            // foreach (Vector2 point in stroke) {
            //     // TODO: if x - lastX > 1, then fill the gap
            //     float x = (maxX - point.x) * scale + 1.0f;
            //     float y = (point.y - minY) * scale + 1.0f;
            //     int xIdx = (int)Mathf.Floor(x);
            //     int yIdx = (int)Mathf.Floor(y);
            //     float depth = 1.2f - (Mathf.Pow(x - ((float)xIdx + 0.5f), 2.0f) + Mathf.Pow(y - ((float)yIdx + 0.5f), 2.0f));
            //     depth = Mathf.Min(depth, 1.0f);
            //     bitmap[xIdx * 28 + yIdx] = Mathf.Max(bitmap[xIdx * 28 + yIdx], depth);
            //     int xIdxNext = xIdx + ((x > (float)xIdx + 0.5f)? 1 : (-1));
            //     int yIdxNext = yIdx + ((y > (float)yIdx + 0.5f)? 1 : (-1));
            //     if (xIdxNext != 28 && yIdxNext != 28) {
            //         depth = 1.2f - (Mathf.Pow(x - ((float)xIdxNext + 0.5f), 2.0f) + Mathf.Pow(y - ((float)yIdxNext + 0.5f), 2.0f)); 
            //         bitmap[xIdxNext * 28 + yIdxNext] = Mathf.Max(bitmap[xIdxNext * 28 + yIdxNext], depth);
            //     }
            //     if (xIdxNext != 28) {
            //         depth = 1.2f - (Mathf.Pow(x - ((float)xIdxNext + 0.5f), 2.0f) + Mathf.Pow(y - ((float)yIdx + 0.5f), 2.0f)); 
            //         bitmap[xIdxNext * 28 + yIdx] = Mathf.Max(bitmap[xIdxNext * 28 + yIdx], depth);
            //     }
            //     if (yIdxNext != 28) {
            //         depth = 1.2f - (Mathf.Pow(x - ((float)xIdx + 0.5f), 2.0f) + Mathf.Pow(y - ((float)yIdxNext + 0.5f), 2.0f)); 
            //         bitmap[xIdx * 28 + yIdxNext] = Mathf.Max(bitmap[xIdx * 28 + yIdxNext], depth);
            //     }
            // }
        }

        string bitmapStr = "";
        // string bitmapStr = bitmap.Aggregate("", (p, n) => p + ((n > 0.0) ? "■" : " ")).
        // for (int i = 0; i < 28; ++i) {
        //     bitmapStr = bitmapStr.Insert(i*28-1, "\n");
        // }
        for (int i = 0; i < 28; ++i) {
            for (int j = 0; j < 28; ++j) {
                if (bitmap[i * 28 + j] > 0.0) {
                    bitmapStr += "■";
                } else {
                    bitmapStr += " ";
                }
            }
            bitmapStr += "\n";
        }

        drawManager.ResultedBitmap256 = (from value in bitmap select (int)Math.Round(value * 255f)).ToArray();
        if (saveBitmap) {
            var bitmapExport = JsonConvert.SerializeObject(resultedBitmap256, drawManager.jsonSettings) + "\n";
            ExportBitmap(bitmapExport);
        }

        bitmapsInString[index] = bitmapStr;

        return bitmap;
    }

    public void ExportBitmap(string bitmapExport) {
        string filename = Application.dataPath + "/" + "bitmap.txt";
        drawManager.ExportText(filename, bitmapExport);
    }
}
