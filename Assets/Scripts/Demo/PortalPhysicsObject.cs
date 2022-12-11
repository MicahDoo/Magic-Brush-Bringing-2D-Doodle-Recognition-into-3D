using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof (Rigidbody))]
public class PortalPhysicsObject : PortalTraveller {

    public float force = 10;
    // new Rigidbody rigidbody;
    Rigidbody rigidbody;
    public Color[] colors;
    static int i;

    void Awake () {
        rigidbody = GetComponent<Rigidbody> ();
        if (graphicsObject == null) {
            graphicsObject = transform.GetChild(0).gameObject;
            Debug.Log("No preassigned transport model. Assign automatically.");
            Debug.Log(graphicsObject);
        }
        rigidbody = GetComponent<Rigidbody> ();
        // graphicsObject.GetComponent<MeshRenderer> ().material.color = colors[i];
        // i++;
        // if (i > colors.Length - 1) {
        //     i = 0;
        // }
    }

    public override void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
        // base.Teleport (fromPortal, toPortal, pos, rot); // base: Access the oldest parent class
        transform.position = pos;
        transform.rotation = rot;
        GetComponent<Rigidbody>().velocity = toPortal.TransformVector (fromPortal.InverseTransformVector (GetComponent<Rigidbody>().velocity));
        GetComponent<Rigidbody>().angularVelocity = toPortal.TransformVector (fromPortal.InverseTransformVector (GetComponent<Rigidbody>().angularVelocity));
    }
}