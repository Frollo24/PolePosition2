using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class PlayerController : NetworkBehaviour
{
    #region Variables

    private MyNetworkManager m_networkManager;

    [Header("Movement")] public List<AxleInfo> axleInfos;
    public float forwardMotorTorque = 100000;
    public float backwardMotorTorque = 50000;
    public float maxSteeringAngle = 15;
    public float engineBrake = 1e+12f;
    public float footBrake = 1e+24f;
    public float topSpeed = 200f;
    public float downForce = 100f;
    public float slipLimit = 0.2f;

    private Vector3 CurrentRotation { get; set; }
    private float timeFlipped;

    /// <summary>
    /// Can player move his avatar or not. Managed by client, movement managed by server.
    /// </summary>
    private bool m_canMove = false;
    public bool CanMove
    {
        get { return m_canMove; }
        set { m_canMove = value; }
    }
    private float InputAcceleration { get; set; }
    private float InputSteering { get; set; }
    private float InputBrake { get; set; }

    private InputController _input;

    private PlayerInfo m_PlayerInfo;

    private Rigidbody m_Rigidbody;
    private float m_SteerHelper = 0.8f;


    private float m_CurrentSpeed = 0;
    private int m_SignSpeed = 1;

    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon) return;
            m_CurrentSpeed = value;
            OnSpeedChangeEvent?.Invoke(m_CurrentSpeed);
        }
    }
    private int SignSpeed
    {
        get { return m_SignSpeed; }
        set
        {
            m_SignSpeed = value;
        }
    }

    public delegate void OnSpeedChangeDelegate(float newVal);

    public event OnSpeedChangeDelegate OnSpeedChangeEvent;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_networkManager = FindObjectOfType<MyNetworkManager>();

        _input = new InputController();

        timeFlipped = 0;
    }

    private void OnEnable()
    {
        _input.Enable();
    }

    private void OnDisable()
    {
        _input.Disable();
    }

    private void Update()
    {
        Vector3 input = new Vector3(
            _input.Player.Acceleration.ReadValue<float>(),
            _input.Player.Steering.ReadValue<float>(),
            _input.Player.Brake.ReadValue<float>()
        );
        if(CanMove)
            CmdCalculateMovement(input.x, input.y, input.z);

        if (!isServer) //Only calculates physics on server
            Physics.autoSimulation = false;
        
        Speed = m_Rigidbody.velocity.magnitude * SignSpeed;
        UpdateSignSpeed();
    }

    [Command]
    private void CmdCalculateMovement(float acceleration, float steering, float brake)
    {
        InputAcceleration = acceleration;
        InputSteering = steering;
        InputBrake = brake;
    }

    [Command]
    public void CmdStartRace(int id)
    {
        transform.position = FindObjectOfType<PolePositionManager>().startingPoints[id].position;
        transform.rotation = FindObjectOfType<PolePositionManager>().startingPoints[id].rotation;
    }

    [Command]
    public void CmdStopRace()
    {
        int i = 0;
        foreach (var player in NetworkServer.connections)
        {
            player.Value.identity.transform.position = m_networkManager.GetStartPosition().position;
            player.Value.identity.transform.rotation = m_networkManager.GetStartPosition().rotation;

            player.Value.identity.GetComponent<PlayerInfo>().CurrentLap = -1;
            i++;
        }
    }

    #region Test Methods

    [ClientRpc]
    public void AllowMovement()
    {
        CanMove = true;
    }

#if UNITY_EDITOR
    [ContextMenu("TryMoveForward")]
    [Command]
    private void TryMoveForward() //Test method, only can be called by UnityEditor
    {
        m_Rigidbody.velocity = 20 * transform.forward;
    }

    [ContextMenu("Flip car")]
    private void FlipCar() //Flips the car on purpose, only can be called by UnityEditr
    {
        transform.Translate(Vector3.up * 2, Space.World);
        transform.Rotate(new Vector3(0, 0, 180));
    }
#endif

    #endregion

    [Command] //Movement petition by client, managed by server
    public void FixedUpdate()
    {
        InputSteering = Mathf.Clamp(InputSteering, -1, 1);
        InputAcceleration = Mathf.Clamp(InputAcceleration, -1, 1);
        InputBrake = Mathf.Clamp(InputBrake, 0, 1);

        float steering = maxSteeringAngle * InputSteering;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor)
            {
                if (InputAcceleration > float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = forwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = forwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (InputAcceleration < -float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (Math.Abs(InputAcceleration) < float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.leftWheel.brakeTorque = engineBrake;
                    axleInfo.rightWheel.motorTorque = 0;
                    axleInfo.rightWheel.brakeTorque = engineBrake;
                }

                if (InputBrake > 0)
                {
                    axleInfo.leftWheel.brakeTorque = footBrake;
                    axleInfo.rightWheel.brakeTorque = footBrake;
                }
            }

            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }

        FixCarDrift();
        SteerHelper();
        SpeedLimiter();
        AddDownForce();
        TractionControl();
        CheckIfFlippedCar();
    }

#endregion

#region Methods

    // crude traction control that reduces the power to wheel if the car is wheel spinning too much
    private void TractionControl()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit wheelHitLeft;
            WheelHit wheelHitRight;
            axleInfo.leftWheel.GetGroundHit(out wheelHitLeft);
            axleInfo.rightWheel.GetGroundHit(out wheelHitRight);

            if (wheelHitLeft.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitLeft.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.leftWheel.motorTorque -= axleInfo.leftWheel.motorTorque * howMuchSlip * slipLimit;
            }

            if (wheelHitRight.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitRight.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.rightWheel.motorTorque -= axleInfo.rightWheel.motorTorque * howMuchSlip * slipLimit;
            }
        }
    }

    // this is used to add more grip in relation to speed
    private void AddDownForce()
    {
        foreach (var axleInfo in axleInfos)
        {
            axleInfo.leftWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.leftWheel.attachedRigidbody.velocity.magnitude));
            axleInfo.rightWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.rightWheel.attachedRigidbody.velocity.magnitude));
        }
    }

    private void SpeedLimiter()
    {
        float speed = m_Rigidbody.velocity.magnitude;
        if (speed > topSpeed)
            m_Rigidbody.velocity = topSpeed * m_Rigidbody.velocity.normalized;
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider col)
    {
        if (col.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = col.transform.GetChild(0);
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        var myTransform = visualWheel.transform;
        myTransform.position = position;
        myTransform.rotation = rotation;
    }

    private void SteerHelper()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit[] wheelHit = new WheelHit[2];
            axleInfo.leftWheel.GetGroundHit(out wheelHit[0]);
            axleInfo.rightWheel.GetGroundHit(out wheelHit[1]);
            foreach (var wh in wheelHit)
            {
                if (wh.normal == Vector3.zero)
                    return; // wheels arent on the ground so dont realign the rigidbody velocity
            }
        }

        // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
        if (Mathf.Abs(CurrentRotation.y - transform.eulerAngles.y) < 10f)
        {
            var turnAdjust = (transform.eulerAngles.y - CurrentRotation.y) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);

            //y-component of quaternion may vary subtly
            if (Math.Abs(velRotation.y) < 0.001f)
                velRotation = Quaternion.identity;

            m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
            m_Rigidbody.angularVelocity = new Vector3(m_Rigidbody.angularVelocity.x, m_Rigidbody.angularVelocity.y, 0);
        }

        CurrentRotation = transform.eulerAngles;
    }

    /// <summary>
    /// Checks if car is flipped more than 45 degrees for more than 2 seconds
    /// We assume that more than 2 seconds flipped means a stuck car
    /// </summary>
    
    private void CheckIfFlippedCar()
    {
        if(Vector3.Dot(Vector3.up, transform.up) < (Mathf.Sqrt(2) / 2)) //Check if angle is greater than 45 degrees
        {
            timeFlipped += Time.fixedUnscaledDeltaTime;
            if (timeFlipped > 2) 
            {
                timeFlipped = 0;
                transform.Translate(Vector3.up * 3, Space.World);
                transform.Rotate(new Vector3(-transform.localEulerAngles.x, 0, -transform.localEulerAngles.z));
            }
        }
        else
        {
            timeFlipped = 0;
        }
    }

    private void FixCarDrift()
    {
        //Prevents the car from sliding with no velocity
        if (Mathf.Abs(Speed) < 0.05f)
        {
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            m_Rigidbody.constraints = RigidbodyConstraints.None;
        }
    }

    /// <summary>
    /// Checks if the car is moving forward or backwards, and calculate it.
    /// Dot product is used for knowing if principal direction is forward-aligned or backwards-aligned.
    /// </summary>
    public void UpdateSignSpeed()
    {
        SignSpeed = Math.Sign(Vector3.Dot(m_Rigidbody.velocity, transform.forward));
    }

#endregion
}