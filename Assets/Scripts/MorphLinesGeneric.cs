using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// from tutorial on youtu.be/v1Q08dG1Me4
public class MorphLinesGeneric : MonoBehaviour
{  
    public float morphTime;

    public void MorphTo(LineRenderer fromRenderer, List<Vector3> finishingPoints, float time = -1)
    {
        if (time < 0f) {
            StartCoroutine(_MorphTo(fromRenderer, finishingPoints, morphTime));
        } else {
            StartCoroutine(_MorphTo(fromRenderer, finishingPoints, time));
        }
    }

    IEnumerator _MorphTo(LineRenderer fromRenderer, List<Vector3> finishingPoints, float time)
    {
        
        List<Vector3> startingPoints = GetPoints(fromRenderer);

        int difference = startingPoints.Count - finishingPoints.Count;
        if(difference > 0)
        {
            finishingPoints = AddFillerPoints(finishingPoints, difference);
        }
        else if(difference < 0)
        {
            startingPoints = AddFillerPoints(startingPoints, -difference);
        }
        
        float timer = 0;
        fromRenderer.positionCount = finishingPoints.Count;
        while(timer < time)
        {
            timer += Time.deltaTime;
            float percentage = timer/time;
            fromRenderer.SetPositions(CalculatePositions(startingPoints, finishingPoints,percentage));
            yield return null;
        }
        fromRenderer.SetPositions(finishingPoints.ToArray());

        yield return null;
    }
    
    List<Vector3> GetPoints(LineRenderer lineRenderer)
    {
        Vector3[] points = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(points);
        List<Vector3> pointsList = new List<Vector3>(points);
        return pointsList;
    }

    List<Vector3> AddFillerPoints(List<Vector3> points, int amount)
    {
        List<Vector3> originalPoints = new List<Vector3>(points);
        
        if(points.Count<=1)
        {
            for(int i = 0; i < amount; i++)
            {
                points.Add(Vector3.zero);
            }
            return points;
        }

        int sectionsAmount = points.Count-1;
        int pointsPerPoint = amount/sectionsAmount;
        int remainder = amount%sectionsAmount;
        int addedPoints = 0;

        for(int i = 0; i<sectionsAmount ; i ++)
        {
            int pointsToAdd = pointsPerPoint;
            if(remainder>i)
            {
                pointsToAdd++;
            }
            float percentagePerStep = (float)1/(pointsToAdd);
            for(int j = 0; j<pointsToAdd; j++)
            {
                float currentPercentage = percentagePerStep*j;
                points.Insert(i+j+1+addedPoints, Vector3.Lerp(originalPoints[i],originalPoints[i+1],currentPercentage));
            }
            addedPoints += pointsToAdd;
        }
        return points;
    }

    //LineRendererLerp?
    Vector3[] CalculatePositions(List<Vector3> points1 ,List<Vector3> points2, float progress)
    {
        List<Vector3> newPoints = new List<Vector3>();
        
        for(int i = 0; i<points1.Count; i++)
        {
            newPoints.Add(Vector3.Lerp(points1[i],points2[i],progress));
        }

        return newPoints.ToArray();
    }

    Vector3 LerpMath(Vector3 pointA, Vector3 pointB, float progress)
    {
        Vector3 difference = pointB - pointA;
        return pointA + (difference*progress);
    }
}
