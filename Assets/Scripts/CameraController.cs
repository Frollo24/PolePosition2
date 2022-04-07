using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class CameraController : MonoBehaviour
{
    /// <summary>
    /// Focus logic. Spectator can focus on players and on the circuit
    /// </summary>
    [SerializeField] public GameObject m_Focus;
    [SerializeField] public int m_specFocus; //ID of focused player
    [SerializeField] public Transform m_specCircuit; //Focus on the circuit

    [SerializeField] public Vector3 m_offset = new Vector3(10, 10, 10);

    [SerializeField] public CircuitController m_Circuit;
    [SerializeField] private float m_Distance = 10;
    [SerializeField] private float m_Elevation = 8;
    [Range(0, 1)] [SerializeField] private float m_Following = 0.5f;

    private Vector3 m_Direction = Vector3.zero;

    private Camera mainCamera;

    //Camera position when game starts. It shows main menu assets when there is no active race.
    [SerializeField]private Vector3 startPosition;
    [SerializeField]private Quaternion startRotation;

    private InputController _input;

    private void Awake()
    {
        _input = new InputController();
    }
    private void OnEnable()
    {
        _input.Enable();
    }

    private void OnDisable()
    {
        _input.Disable();
    }

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = this.GetComponent<Camera>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (m_Focus != null)
        {
            if (this.m_Circuit != null)
            {
                if (this.m_Direction.magnitude == 0)
                {
                    this.m_Direction = new Vector3(0f, -1f, 0f);
                }

                if (m_specFocus != 9999) { //Focusing on a player
                    int segIdx;
                    float carDist;
                    Vector3 carProj;

                    m_Circuit.ComputeClosestPointArcLength(m_Focus.transform.position, out segIdx, out carProj,
                        out carDist);

                    Vector3 pathDir = -m_Circuit.GetSegment(segIdx);
                    pathDir = new Vector3(pathDir.x, 0f, pathDir.z);
                    pathDir.Normalize();

                    this.m_Direction = Vector3.Lerp(this.m_Direction, pathDir, this.m_Following * Time.deltaTime);
                    Vector3 offset = this.m_Direction * this.m_Distance;
                    offset = new Vector3(offset.x, m_Elevation, offset.z);

                    mainCamera.transform.position = m_Focus.transform.position + offset;
                    mainCamera.transform.LookAt(m_Focus.transform.position);
                }
                else
                {
                    //Focusing on the circuit
                    mainCamera.transform.position = m_Focus.transform.position;
                    mainCamera.transform.LookAt(m_specCircuit);
                }
            }
            else
            {
                //Default focus if no circuit is found
                mainCamera.transform.position = m_Focus.transform.position + m_offset;
                mainCamera.transform.LookAt(m_Focus.transform.position);
            }
        }
        else
        {
            //Showing main menu assets when no focus is available
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
    }

    /// <summary>
    /// Gets focus on another player or on the circuit if you are not a racer.
    /// </summary>
    /// <returns>GameObject to be focused.</returns>
    public GameObject GetOtherPlayerFocus()
    {
        //9999 represents a neutral spectator position (focusing on the cicuit)
        SetupPlayer[] players = FindObjectsOfType<SetupPlayer>();
        GameObject[] focuses = new GameObject[players.Length];

        //Loops through all possible spectating points. For example:
        //2 players and 1 spectator: 0 -> 1 -> 9999 -> 0 -> 1 -> 9999...
        //3 players and 1 spectator: 0 -> 1 -> 2 -> 9999 -> 0 -> 1...
        if (Keyboard.current[Key.F].wasPressedThisFrame) {
            if (m_specFocus == 9999) m_specFocus = 0;
            else
            m_specFocus = (m_specFocus + 1) % players.Length; 
        }

        //Maps players into focuses using Player ID
        for(int i = 0; i < players.Length; i++)
        {
            int id = players[i].PlayerInfo.ID;
            if(id != 9999) //This player is a racer that can be focused
                focuses[id] = players[i].gameObject;
        }

        if (m_specFocus == 9999) //Focusing on the circuit
        {
            return GameObject.Find("Spectator Object");
        }

        if (focuses[m_specFocus] != null) //Focusing on a player
        {
            return focuses[m_specFocus];
        }
        else
            m_specFocus = 9999; //Avoids focusing on a null player

        return GameObject.Find("Spectator Object");
    }
}