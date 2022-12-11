using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CameraUtility {
    static readonly Vector3[] cubeCornerOffsets = {
        new Vector3 (1, 1, 1),
        new Vector3 (-1, 1, 1),
        new Vector3 (-1, -1, 1),
        new Vector3 (-1, -1, -1),
        new Vector3 (-1, 1, -1),
        new Vector3 (1, -1, -1),
        new Vector3 (1, 1, -1),
        new Vector3 (1, -1, 1),
    };

    // http://wiki.unity3d.com/index.php/IsVisibleFrom
    public static bool VisibleFromCamera (Renderer renderer, Camera camera) {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes (camera);
        return GeometryUtility.TestPlanesAABB (frustumPlanes, renderer.bounds);
    }

    public static bool VisibleFromCameraThroughPortal (Renderer renderer, Renderer rendererFrame, Camera camera) {
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes (camera);
        // adjust frustrum planes
        // Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
        // Calculate far first. Then adjust left right up down accordingly.
        Mesh frameMesh = rendererFrame.gameObject.GetComponent<MeshFilter> ().mesh;
        // assumed mesh has two triangles
        Vector3[] frameInWorldSpace = new Vector3[4];
        List<int> vertexOrder = new List<int>();
        for (int i = 0; i < 4; i++) {
            frameInWorldSpace[i] = rendererFrame.transform.TransformPoint(frameMesh.vertices[i]);
            Debug.DrawLine(camera.transform.position, rendererFrame.transform.TransformPoint(frameMesh.vertices[i]));
        }

        Vector3 camOrigin = camera.transform.position;
        // Debug.DrawRay(camOrigin, frustumPlanes[2].normal);

        Vector3 lowerLeft;
        Vector3 lower;
        Vector3 lowerNormal;
        Vector3 upperRight;
        Vector3 upper;
        Vector3 upperNormal;
        Vector3 upperLeft;
        Vector3 left;
        Vector3 leftNormal;
        Vector3 lowerRight;
        Vector3 right;
        Vector3 rightNormal;

        if (Vector3.Dot(rendererFrame.transform.forward, camera.transform.forward) < 0) {

            lowerLeft = frameInWorldSpace[0] - camOrigin;
            lower = frameInWorldSpace[1] - frameInWorldSpace[0];
            lowerNormal = Vector3.Cross(lowerLeft, lower).normalized;
            upperRight = frameInWorldSpace[2] - camOrigin;
            upper = frameInWorldSpace[3] - frameInWorldSpace[2];
            upperNormal = Vector3.Cross(upperRight, upper).normalized;
            upperLeft = frameInWorldSpace[3] - camOrigin;
            left = frameInWorldSpace[0] - frameInWorldSpace[3];
            leftNormal = Vector3.Cross(upperLeft, left).normalized;
            lowerRight = frameInWorldSpace[1] - camOrigin;
            right = frameInWorldSpace[2] - frameInWorldSpace[1];
            rightNormal = Vector3.Cross(lowerRight, right).normalized;

        } else {

            lowerLeft = frameInWorldSpace[1] - camOrigin;
            lower = frameInWorldSpace[0] - frameInWorldSpace[1];
            lowerNormal = Vector3.Cross(lowerLeft, lower).normalized;
            upperRight = frameInWorldSpace[3] - camOrigin;
            upper = frameInWorldSpace[2] - frameInWorldSpace[3];
            upperNormal = Vector3.Cross(upperRight, upper).normalized;
            upperLeft = frameInWorldSpace[2] - camOrigin;
            left = frameInWorldSpace[1] - frameInWorldSpace[2];
            leftNormal = Vector3.Cross(upperLeft, left).normalized;
            lowerRight = frameInWorldSpace[0] - camOrigin;
            right = frameInWorldSpace[3] - frameInWorldSpace[0];
            rightNormal = Vector3.Cross(lowerRight, right).normalized;

        }

        frustumPlanes[0].SetNormalAndPosition(leftNormal, camOrigin);
        Debug.DrawRay(frameInWorldSpace[0], leftNormal * 5, Color.red);
        frustumPlanes[1].SetNormalAndPosition(rightNormal, camOrigin);
        Debug.DrawRay(frameInWorldSpace[1], lowerNormal * 5,  Color.yellow);
        frustumPlanes[2].SetNormalAndPosition(lowerNormal, camOrigin);
        Debug.DrawRay(frameInWorldSpace[2], rightNormal * 5,  Color.black);
        frustumPlanes[3].SetNormalAndPosition(upperNormal, camOrigin);
        Debug.DrawRay(frameInWorldSpace[3], upperNormal * 5, Color.blue);

        return GeometryUtility.TestPlanesAABB (frustumPlanes, renderer.bounds);
    }

    public static bool BoundsOverlap (MeshFilter nearObject, MeshFilter farObject, Camera camera) {

        var near = GetScreenRectFromBounds (nearObject, camera);
        var far = GetScreenRectFromBounds (farObject, camera);

        // ensure far object is indeed further away than near object
        if (far.zMax > near.zMin) {
            // Doesn't overlap on x axis
            if (far.xMax < near.xMin || far.xMin > near.xMax) {
                return false;
            }
            // Doesn't overlap on y axis
            if (far.yMax < near.yMin || far.yMin > near.yMax) {
                return false;
            }
            // Overlaps
            return true;
        }
        return false;
    }

    // With thanks to http://www.turiyaware.com/a-solution-to-unitys-camera-worldtoscreenpoint-causing-ui-elements-to-display-when-object-is-behind-the-camera/
    public static MinMax3D GetScreenRectFromBounds (MeshFilter renderer, Camera mainCamera) {
        MinMax3D minMax = new MinMax3D (float.MaxValue, float.MinValue);
        
        Vector3[] screenBoundsExtents = new Vector3[8];
        var localBounds = renderer.sharedMesh.bounds;
        bool anyPointIsInFrontOfCamera = false;

        for (int i = 0; i < 8; i++) {
            Vector3 localSpaceCorner = localBounds.center + Vector3.Scale (localBounds.extents, cubeCornerOffsets[i]);
            Vector3 worldSpaceCorner = renderer.transform.TransformPoint (localSpaceCorner);
            Vector3 viewportSpaceCorner = mainCamera.WorldToViewportPoint (worldSpaceCorner);

            if (viewportSpaceCorner.z > 0) {
                anyPointIsInFrontOfCamera = true;
            } else {
                // If point is behind camera, it gets flipped to the opposite side
                // So clamp to opposite edge to correct for this
                viewportSpaceCorner.x = (viewportSpaceCorner.x <= 0.5f) ? 1 : 0;
                viewportSpaceCorner.y = (viewportSpaceCorner.y <= 0.5f) ? 1 : 0;
            }

            // Update bounds with new corner point
            minMax.AddPoint (viewportSpaceCorner);
        }

        // All points are behind camera so just return empty bounds
        if (!anyPointIsInFrontOfCamera) {
            return new MinMax3D ();
        }

        return minMax;
    }

    public struct MinMax3D {
        public float xMin;
        public float xMax;
        public float yMin;
        public float yMax;
        public float zMin;
        public float zMax;

        public MinMax3D (float min, float max) {
            this.xMin = min;
            this.xMax = max;
            this.yMin = min;
            this.yMax = max;
            this.zMin = min;
            this.zMax = max;
        }

        public void AddPoint (Vector3 point) {
            xMin = Mathf.Min (xMin, point.x);
            xMax = Mathf.Max (xMax, point.x);
            yMin = Mathf.Min (yMin, point.y);
            yMax = Mathf.Max (yMax, point.y);
            zMin = Mathf.Min (zMin, point.z);
            zMax = Mathf.Max (zMax, point.z);
        }
    }

}