using System.Collections.Generic;
using UnityEngine;

/* If graphicsObject is assigned, use if, if not, use the first child. */

public class PortalTraveller : MonoBehaviour {

    public GameObject graphicsObject;
    public GameObject graphicsClone { get; set; }
    public Vector3 previousOffsetFromPortal { get; set; }

    public Material[] originalMaterials { get; set; }
    public Material[] cloneMaterials { get; set; }

    public virtual void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
        transform.position = pos;
        transform.rotation = rot;
        GetComponent<Rigidbody>().velocity = toPortal.TransformVector (fromPortal.InverseTransformVector (GetComponent<Rigidbody>().velocity));
        GetComponent<Rigidbody>().angularVelocity = toPortal.TransformVector (fromPortal.InverseTransformVector (GetComponent<Rigidbody>().angularVelocity));
        Physics.SyncTransforms ();
    }

    void Awake () {
        if (graphicsObject == null) {
            graphicsObject = transform.GetChild(0).gameObject;
            Debug.Log("No preassigned transport model. Assign automatically.");
            Debug.Log(graphicsObject);
        }
    }

    // void Start () {
    //     /* Not called in child classes unless you call base.Start () */
    // }

    // Called when first touches portal
    public virtual void EnterPortalThreshold () {
        if (graphicsClone == null) {
            graphicsClone = Instantiate (graphicsObject);
            graphicsClone.transform.parent = graphicsObject.transform.parent;
            graphicsClone.transform.localScale = graphicsObject.transform.localScale;
            originalMaterials = GetMaterials (graphicsObject);
            for (int i = 0; i < originalMaterials.Length; ++i) {
                Debug.Log("originalMaterials " + i + ": " + originalMaterials[i].name);
            }
            cloneMaterials = GetMaterials (graphicsClone);
        } else {
            graphicsClone.SetActive (true);
        }
    }

    // Called once no longer touching portal (excluding when teleporting)
    public virtual void ExitPortalThreshold () {
        graphicsClone.SetActive (false);
        // Disable slicing
        for (int i = 0; i < originalMaterials.Length; i++) {
            originalMaterials[i].SetVector ("sliceNormal", Vector3.zero);
        }
    }

    public void SetSliceOffsetDst (float dst, bool clone) {
        for (int i = 0; i < originalMaterials.Length; i++) {
            if (clone) {
                cloneMaterials[i].SetFloat ("sliceOffsetDst", dst);
            } else {
                originalMaterials[i].SetFloat ("sliceOffsetDst", dst);
            }

        }
    }

    Material[] GetMaterials (GameObject g) {
        var renderers = g.GetComponentsInChildren<MeshRenderer> ();
        var matList = new List<Material> ();
        foreach (var renderer in renderers) {
            foreach (var mat in renderer.materials) {
                matList.Add (mat);
            }
        }
        return matList.ToArray ();
    }
}