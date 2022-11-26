using UnityEngine;
using System.Collections.Generic;

public class Car : MonoBehaviour
{
    private Rigidbody rigidbody; // Rigidbody to apply forces to
    private float driveAxis, brakeAxis, turnAxis; // Save valid input values from public interface
    private bool grounded = false;



    [Header("Suspension")]

    [SerializeField] List<Transform> wheels;

    [Tooltip("Radius used for wheel raycasts.")]
    [Range(0.1f, 1f)]
    [SerializeField] float wheelRadius = 0.4f;

    [Tooltip("Spring force constant k. Applies upwards spring force proportional to wheel vertical offset.")]
    [Range(50f, 250f)]
    [SerializeField] float springStrength = 100f;

    [Tooltip("Spring damping value. Damps spring force proportional to point velocity.")]
    [Range(1f, 5f)]
    [SerializeField] float springDamping = 3f;

    [Header("Acceleration")]

    [Tooltip("Max longitudinal force output. Force output is proportional to (1 - (currentSpeed / maxSpeed)).")]
    [Range(15f, 35f)]
    [SerializeField] float maxSpeed = 25f;

    [Header("Friction")]

    [Tooltip("Longitudinal friction coefficient. Used to apply oppositional longitudinal force proportional to velocity.")]
    [Range(1f, 5f)]
    [SerializeField] float longitudinalFriction = 2f;

    [Tooltip("Lateral friction coefficient. Used to apply oppositional lateral force proportional to velocity.")]
    [Range(1f, 5f)]
    [SerializeField] float lateralFriction = 2f;

    [Header("Steering")]

    [Tooltip("Turn angle for wheels.")]
    [Range(10, 45)]
    [SerializeField] float steeringAngle = 30f;

    [Tooltip("Damping coefficient for Y-Axis rotational velocity")]
    [Range(1f, 10f)]
    [SerializeField] float turnDamping = 5f;

    [Header("Boost")]
    public bool applyBoost = false;
    public float boost = 1f;
    public float maxBoost = 2f;
    public float boostStrength = 25f;

    [Header("Air Rotation")]
    public float yawRate;
    public float pitchRate;
    public float rollRate;
    public float rotationDamping = 5f;
    public float pitchAxis, yawAxis, rollAxis;
    
    [Header("Jump")]
    public float jumpStrength;

    [Header("Flip")]
    public float flipStrength = 5f;


    # region Public Interface
    /*  Accepts and validates external drive input.
        Clamps driveAxis between -1 and 1.
    */
    public void Drive(float driveAxis){
        this.driveAxis = Mathf.Clamp(driveAxis, -1f , 1f);
    }

    /*  Accepts and validates external braking input.
        Clamps brakeAxis between 0 and 1.
    */
    public void Brake(float brakeAxis){
        this.brakeAxis = Mathf.Clamp(brakeAxis, 0f, 1f);
    }

    /*  Accepts and validates external turn input.
        Clamps turn axis between -1 and 1.
    */
    public void Turn(float turnAxis){
        this.turnAxis = Mathf.Clamp(turnAxis, -1f, 1f);
    }

    // NEW as of boost section
    public void Boost(bool applyBoost){
        this.applyBoost = applyBoost;
    }

    public void Pitch(float pitchAxis){
        this.pitchAxis = Mathf.Clamp(pitchAxis, -1f, 1f);
    }

    public void Yaw(float yawAxis){
        this.yawAxis = Mathf.Clamp(yawAxis, -1f, 1f);
    }

    public void Roll(float rollAxis){
        this.rollAxis = Mathf.Clamp(rollAxis, -1f, 1f);
    }

    public void Jump(){
        if (grounded){
            rigidbody.AddForce(transform.up * jumpStrength);
        }
        else {
            if (Physics.Raycast(transform.position, transform.up, 2f)){
                rigidbody.AddForce(transform.up * -jumpStrength);   
            }
        }
    }
    #endregion

    #region MonoBehaviour Life Cycle
    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ApplySuspensionForce();
        ApplyBoostForce();

        if (!grounded) {
            ApplyAirRotationForce();
            
        }
        else{
            ApplyLongitudinalForce();
            ApplyLateralForce();
            ApplyTurningForce();
        }

        
    }
    #endregion

    #region Forces
    private void ApplyBoostForce(){
        if (!applyBoost || boost <= 0f){
            if (rigidbody.velocity.magnitude > maxSpeed){
                rigidbody.AddForce(-rigidbody.velocity);
            }
        }
        else{
            float forwardVelocity = Vector3.Dot(transform.forward, rigidbody.velocity);
            float boostRatio = (1 - (forwardVelocity / boostStrength));

            boost -= Time.fixedDeltaTime;
            rigidbody.AddForce(transform.forward * boostStrength * boostRatio);
        }
        
    }

    private void ApplyAirRotationForce(){
        float yawVelocity = Vector3.Dot(transform.up, rigidbody.angularVelocity);
        float rollVelocity = Vector3.Dot(transform.forward, rigidbody.angularVelocity);
        float pitchVelocity = Vector3.Dot(transform.right, rigidbody.angularVelocity);

        float yawTorque = (Mathf.Abs(yawAxis) > 0f) ? yawAxis * yawRate : -yawVelocity * rotationDamping;
        float rollTorque = (Mathf.Abs(rollAxis) > 0f) ? rollAxis * rollRate : -rollVelocity * rotationDamping;
        float pitchTorque = (Mathf.Abs(pitchAxis) > 0f) ? pitchAxis * pitchRate : -pitchVelocity * rotationDamping;

        rigidbody.AddTorque(transform.up * yawTorque);
        rigidbody.AddTorque(transform.forward * rollTorque);
        rigidbody.AddTorque(transform.right * pitchTorque);
    }

    private void ApplySuspensionForce()
    {
        bool tempGrounded = false;

        foreach(Transform wheel in wheels)
        {
            Vector3 origin = wheel.position;
            Vector3 direction = -wheel.up;
            RaycastHit hit;
            float offset = 0f;

            if (Physics.Raycast(origin,direction,out hit, wheelRadius)){
                tempGrounded = true;

                Vector3 end = origin + (direction * wheelRadius);
                offset = (end - hit.point).magnitude;

                float pointVelocity = Vector3.Dot(wheel.up, rigidbody.GetPointVelocity(wheel.position));
                float suspensionForce = (springStrength * offset) + (-pointVelocity * springDamping);
                rigidbody.AddForceAtPosition(wheel.up * suspensionForce, wheel.position);

                // NEW 
                wheel.GetChild(0).transform.localPosition = Vector3.up * offset;
            }
        }

        grounded = tempGrounded;
    }

    private void ApplyLongitudinalForce()
    {
        Vector3 force = Vector3.zero;
        float forwardVelocity = Vector3.Dot(transform.forward, rigidbody.velocity);
        float maxSpeedRatio = (1 - (Mathf.Abs(forwardVelocity) / maxSpeed));

        if (Mathf.Abs(driveAxis) > 0){
           force = transform.forward * driveAxis * maxSpeed * maxSpeedRatio;
        }
        else if (applyBoost){
            force = transform.forward * maxSpeed * maxSpeedRatio;
        }
        else{
            force = transform.forward * -forwardVelocity * longitudinalFriction;
        }

        rigidbody.AddForce(force);
    }

    private void ApplyLateralForce()
    {
        float rightVelocity = Vector3.Dot(transform.right, rigidbody.velocity);
        rigidbody.AddForce(transform.right * -rightVelocity * lateralFriction);
    }
    
    private void ApplyTurningForce()
    {
        float forwardVelocity = Vector3.Dot(transform.forward, rigidbody.velocity);
        float rotationalVelocity = Vector3.Dot(transform.up, rigidbody.angularVelocity);

        float torque = forwardVelocity * turnAxis * (Mathf.Deg2Rad * steeringAngle);
        torque += -rotationalVelocity * turnDamping;

        rigidbody.AddTorque(transform.up * torque);
    }
    #endregion
}
