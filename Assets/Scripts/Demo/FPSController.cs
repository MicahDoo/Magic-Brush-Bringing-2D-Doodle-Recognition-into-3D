using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Obi;

public class FPSController : PortalTraveller {

    [Header("Environmental Settings")]
    public float walkSpeed = 3;
    public float runSpeed = 6;
    public float smoothMoveTime = 0.1f;
    public float jumpForce = 300.0f;
    public float gravity = 10f;
    // public Vector2 terminalVelocity = new Vector2 (-15, 15);
    public Vector3 gravityDirection = new Vector3 (0, 1, 0);
    // public ObiSolver obiSolver;
    Vector3 headDirection;

    [Header("Control Settings")]
    public bool lockCursor;
    public float mouseSensitivity = 10;
    public Vector2 pitchMinMax = new Vector2 (-90, 90);
    public float rotationSmoothTime = 0.1f;

    CharacterController controller;
    Rigidbody body;
    Camera cam;
    public float yaw;
    public float pitch;
    float smoothYaw;
    float smoothPitch;
    private bool handMode;

    float yawSmoothV;
    float pitchSmoothV;
    float verticalVelocity;
    Vector3 velocity;
    Vector3 smoothV;
    Vector3 rotationSmoothVelocity;
    Vector3 currentRotation;

    bool jumping;
    float lastGroundedTime;
    bool disabled;
    bool balanced;
    bool isGrounded;

    void Start () {
        cam = Camera.main;
        if (lockCursor) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        balanced = false;
        isGrounded = false;
        handMode = false;

        controller = GetComponent<CharacterController> ();
        body = GetComponent<Rigidbody> ();

        Physics.gravity = gravity * gravityDirection;
        // obiSolver.parameters.gravity = Physics.gravity;
        // obiSolver.UpdateParameters ();
        headDirection = -1.0f * gravityDirection;

        yaw = transform.eulerAngles.y;
        pitch = cam.transform.localEulerAngles.x;
        smoothYaw = yaw;
        smoothPitch = pitch;

        // RenderSettings.ambientLight = Color.red;
    }

    void Update () {
        if (Input.GetKeyDown (KeyCode.P)) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Break ();
        }
        if (Input.GetKeyDown (KeyCode.O)) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            disabled = !disabled;
        }

        if (disabled) {
            return;
        }

        Vector2 input = new Vector2 (Input.GetAxisRaw ("Vertical"), Input.GetAxisRaw ("Horizontal"));
        Vector3 inputDir = new Vector2 (input.x, input.y).normalized;
        // Vector3 worldInputDir = transform.TransformDirection (inputDir);
        Vector3 worldInputDir = transform.forward * inputDir.x + transform.right * inputDir.y;
        float currentSpeed = (Input.GetKey (KeyCode.LeftShift)) ? runSpeed : walkSpeed;
        Vector3 targetVelocity = worldInputDir * currentSpeed;
        velocity = Vector3.SmoothDamp (velocity, targetVelocity, ref smoothV, smoothMoveTime);
        headDirection = -1.0f * gravityDirection;
        velocity = new Vector3 (velocity.x, velocity.y, velocity.z);// + headDirection * verticalVelocity;
        transform.position += velocity * Time.deltaTime;

        if (!balanced) {
            float stepSize = walkSpeed * Time.deltaTime;
            Vector3 targetForwardDirection = Vector3.ProjectOnPlane(transform.forward, headDirection);
            if (targetForwardDirection.x == 0 && targetForwardDirection.y == 0 && targetForwardDirection.z == 0) {
                targetForwardDirection = Vector3.ProjectOnPlane(new Vector3(0.57735f, 0.57735f, 0.57735f), headDirection);
            }
            Quaternion targetRotation = Quaternion.LookRotation(targetForwardDirection, headDirection);
            if (transform.up == headDirection && transform.forward == targetForwardDirection){
                balanced = true;
            }
            else if (Mathf.Abs(transform.up.x - headDirection.x) <= stepSize && Mathf.Abs(transform.up.y - headDirection.y) <= stepSize && Mathf.Abs(transform.up.z - headDirection.z) <= stepSize && Mathf.Abs(transform.forward.x - targetForwardDirection.x) <= stepSize && Mathf.Abs(transform.forward.y - targetForwardDirection.y) <= stepSize && Mathf.Abs(transform.forward.z - targetForwardDirection.z) <= stepSize) {
                transform.rotation = Quaternion.LookRotation(targetForwardDirection, headDirection);
                Debug.Log("Direction Corrected");
            }
            else {
                Debug.Log(transform.up + "->" + headDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, stepSize*100.0f);
            }
        }

        if (Input.GetKeyDown (KeyCode.Space)) {
            float timeSinceLastTouchedGround = Time.time - lastGroundedTime;
            if (isGrounded || (!jumping && timeSinceLastTouchedGround < 0.15f)) {
                Debug.Log("Jump!");
                jumping = true;
                isGrounded = false;
                GetComponent<Rigidbody>().AddForce(transform.up * jumpForce); // or headDirection?
            }
        }

        if (Input.GetKeyDown (KeyCode.G)) { // Change Gravity
            balanced = false;
            // Check all six directions
            Vector3 pointingTowards = transform.forward;
            if (Mathf.Abs(pointingTowards.x) >= Mathf.Abs(pointingTowards.y)) {
                if (Mathf.Abs(pointingTowards.x) >= Mathf.Abs(pointingTowards.z)) {
                    if (pointingTowards.x >= 0.0f) {
                        gravityDirection = new Vector3 (1.0f, 0.0f, 0.0f);
                    } else {
                        gravityDirection = new Vector3 (-1.0f, 0.0f, 0.0f);
                    }
                    Physics.gravity = gravity * gravityDirection;
                    headDirection = -1.0f * gravityDirection;
                } else {
                    if (pointingTowards.z >= 0.0f) {
                        gravityDirection = new Vector3 (0.0f, 0.0f, 1.0f);
                    } else {
                        gravityDirection = new Vector3 (0.0f, 0.0f, -1.0f);
                    }
                    Physics.gravity = gravity * gravityDirection;
                    headDirection = -1.0f * gravityDirection;
                }
            } else {
                if (Mathf.Abs(pointingTowards.y) >= Mathf.Abs(pointingTowards.z)) {
                    if (pointingTowards.y >= 0.0f) {
                        gravityDirection = new Vector3 (0.0f, 1.0f, 0.0f);
                    } else {
                        gravityDirection = new Vector3 (0.0f, -1.0f, 0.0f);
                    }
                    Physics.gravity = gravity * gravityDirection;
                    headDirection = -1.0f * gravityDirection;
                } else {
                    if (pointingTowards.z >= 0.0f) {
                        gravityDirection = new Vector3 (0.0f, 0.0f, 1.0f);
                    } else {
                        gravityDirection = new Vector3 (0.0f, 0.0f, -1.0f);
                    }
                    Physics.gravity = gravity * gravityDirection;
                    headDirection = -1.0f * gravityDirection;
                }
            }
            // obiSolver.parameters.gravity = Physics.gravity;
            // obiSolver.UpdateParameters();
        }

        // Move head
        if (handMode) {
            if (Input.GetMouseButtonDown(1)) {
                handMode = false;
                return; // useless in VR
            }
            return;
        }
        if (Input.GetMouseButtonDown(1)) {
            handMode = true;
            return; // useless in VR
        }

        float mX = Input.GetAxisRaw ("Mouse X");
        float mY = Input.GetAxisRaw ("Mouse Y");

        // Verrrrrry gross hack to stop camera swinging down at start???
        float mMag = Mathf.Sqrt (mX * mX + mY * mY);
        if (mMag > 5) {
            mX = 0;
            mY = 0;
        }

        yaw += mX * mouseSensitivity;
        pitch -= mY * mouseSensitivity;
        pitch = Mathf.Clamp (pitch, pitchMinMax.x, pitchMinMax.y);
        smoothPitch = Mathf.SmoothDampAngle (smoothPitch, pitch, ref pitchSmoothV, rotationSmoothTime);
        float oldSmoothYaw = smoothYaw;
        smoothYaw = Mathf.SmoothDampAngle (smoothYaw, yaw, ref yawSmoothV, rotationSmoothTime);

        // transform.eulerAngles = Vector3.up * smoothYaw;
        transform.Rotate(0, smoothYaw - oldSmoothYaw, 0, Space.Self);
        cam.transform.localEulerAngles = Vector3.right * smoothPitch; //KEY: Local pitch (look up and down)

    }

    public override void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
        base.Teleport (fromPortal, toPortal, pos, rot);
        balanced = false;
        Vector3 eulerRot = rot.eulerAngles; // 
        float delta = Mathf.DeltaAngle (smoothYaw, eulerRot.y);
        yaw += delta;
        smoothYaw += delta;
        // transform.eulerAngles = toPortal.up * smoothYaw; // This Vector3.up might be what I have to change.
        // transform.Rotate(0, delta, 0, Space.Self);
        velocity = toPortal.TransformVector (fromPortal.InverseTransformVector (velocity));
        Physics.SyncTransforms ();
    }

    void OnCollisionEnter(Collision collision)
    {
        if(!isGrounded && collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            if(Vector3.Dot(contact.normal, headDirection) > 0.5)
            {
                isGrounded = true;
                lastGroundedTime = Time.time;
                jumping = false;
                Debug.Log("Grounded");
            }
        }
    }

}