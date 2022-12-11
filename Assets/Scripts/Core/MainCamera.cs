using UnityEngine;

public class MainCamera : MonoBehaviour {

    Portal[] portals;

    public Camera probeCamera;
    bool[] portalRendered; // This is useless?

    void Awake () {
        portals = FindObjectsOfType<Portal> ();
        portalRendered = new bool[portals.Length]; //defaulted to all false
        probeCamera = GetComponentsInChildren<Camera>()[1]; //(the 0th one is itself)
        probeCamera.enabled = false;
        for (int i = 0; i < portals.Length; i++) {
            Debug.Log(portals[i].linkedPortal.name);
        }
    }

    // Use this script to determine render order

    async void OnPreCull () {

        for (int i = 0; i < portals.Length; i++) {
            portals[i].PrePortalRender ();
        }

        // !How the render works is that if you can see its linked portal you can render the camera from THIS portal for the other portal to use
        // i's linked portal = outer portal
        // j = inner portal
        // can i see i's linked portal from j? -> if so, i should render it's camera for i's linked portal's screen
        // j is the portal to render, i is only used to set camera position
        for (int i = 0; i < portals.Length; i++) {
            if (!CameraUtility.VisibleFromCamera(portals[i].linkedPortal.screen, Camera.main)) continue;
            // Debug.Log("Can see " + portals[i].linkedPortal.name + " from MainCamera");
            var localToWorldMatrix = Camera.main.transform.localToWorldMatrix; //MainCamera
            localToWorldMatrix =
                portals[i].transform.localToWorldMatrix *
                portals[i].linkedPortal.transform.worldToLocalMatrix *
                localToWorldMatrix;
            probeCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            probeCamera
                .transform
                .SetPositionAndRotation(localToWorldMatrix.GetColumn(3),
                localToWorldMatrix.rotation);
            for (int j = 0; j < portals.Length; j++) {
                if (j != i 
                    && portals[i].name != portals[j].linkedPortal.name 
                    && CameraUtility.
                    VisibleFromCameraThroughPortal(
                        portals[j].linkedPortal.screen, 
                        portals[i].screen, 
                        probeCamera)) 
                {
                    Debug.Log("Can see " + portals[j].linkedPortal.name + " from " + portals[i].name);
                    portals[j].playerCam = probeCamera;
                    portals[j].Render ();
                    portals[j].playerCam = Camera.main;
                }
            }
            // portals[i].Render ();
            // portalRendered[i] = true;
        }

        for (int i = portalRendered.Length - 1; i >= 0; i--) {
            if (!CameraUtility.VisibleFromCamera(portals[i].linkedPortal.screen, Camera.main)) continue;
            portals[i].Render ();
            // portalRendered[i] = true;
        }

        for (int i = 0; i < portals.Length; i++) {
            portals[i].PostPortalRender ();
        }

    }

}