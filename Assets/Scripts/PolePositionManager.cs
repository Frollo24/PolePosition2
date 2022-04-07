using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

public class PolePositionManager : NetworkBehaviour
{
    private MyNetworkManager _networkManager;
    public Transform[] startingPoints; //Starting points of a race

    //Synchronized List of the racers in the circuit
    [SerializeField] private SyncList<NetworkIdentity> _syncRacerList = new SyncList<NetworkIdentity>();
    private CircuitController _circuitController; //Circuit logic manager
    [SyncVar][SerializeField] private int _numPositions; //Number of checkpoints in the circuit
#if UNITY_EDITOR
    //An array of spheres that follows the player along the circuit. Not created in a build.
    private GameObject[] _debuggingSpheres;
#endif

    private void Awake()
    {
        if (_networkManager == null) _networkManager = FindObjectOfType<MyNetworkManager>();
        if (_circuitController == null) _circuitController = FindObjectOfType<CircuitController>();

#if UNITY_EDITOR
        _debuggingSpheres = new GameObject[_networkManager.maxConnections]; //Debugging spheres created only in UnityEditor.
        for (int i = 0; i < _networkManager.maxConnections; ++i)
        {
            _debuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _debuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
            _debuggingSpheres[i].SetActive(false);
        }
#endif
    }

    private void Start()
    {
        _numPositions = _circuitController.CircuitPath.positionCount - 2;
    }

    private void Update()
    {
#if UNITY_EDITOR
        for (int i = 0; i < _networkManager.maxConnections; i++)
        {
            _debuggingSpheres[i].SetActive(i < _syncRacerList.Count);
        }
#endif

        if (!isServer)
            return;

        UpdateRaceProgress();
    }

    [Server]
    public void AddPlayer(NetworkIdentity nid)
    {
        _syncRacerList.Add(nid);
    }

    [Server]
    public void RemovePlayer(NetworkIdentity nid)
    {
        bool success = _syncRacerList.Remove(nid);
        if (success)
            Debug.LogWarning("Successfully removed");
    }

#if UNITY_EDITOR

    [ContextMenu("Test Teleport Players")]
    private void TeleportPlayers() //Teleports players for debugging purposes. Only available in UnityEditor.
    {
        int i = 0;
        foreach (var player in NetworkServer.connections)
        {
            player.Value.identity.transform.position = new Vector3(0, 0, 100) + 1.53f * i * Vector3.right;
            player.Value.identity.transform.Rotate(new Vector3(0, -90, 0), Space.Self);
            i++;
        }
    }
#endif

    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] _arcLengths;

        public PlayerInfoComparer(float[] arcLengths)
        {
            _arcLengths = arcLengths;
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            var lap = y.CurrentLap.CompareTo(x.CurrentLap); //First we check which player has made more laps
            if (lap != 0)
                return lap;

            var pos = y.CurrentPosition.CompareTo(x.CurrentPosition); //Then we check which player is more advanced
            if (pos != 0)
                return pos;

            if (_arcLengths[x.ID] < _arcLengths[y.ID]) //Finally we check which player has advanced more in a segment
                return 1;
            else return -1;
        }
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[_syncRacerList.Count];
        List<PlayerInfo> _foo = new List<PlayerInfo>(); //Auxiliar list which sorts players by order

        for (int i = 0; i < _syncRacerList.Count; ++i)
        {
            _foo.Add(_syncRacerList[i].GetComponent<PlayerInfo>());
            arcLengths[i] = ComputeCarArcLength(i);
        }
        
        _foo.Sort(new PlayerInfoComparer(arcLengths));

        string myRaceOrderDebug = "";
        string myRaceOrderGUI = "";
        foreach (var player in _foo)
        {
            myRaceOrderDebug += player.Name + " ";
            myRaceOrderGUI += player.Name + "\n";
        }

        myRaceOrderGUI = myRaceOrderGUI.Remove(myRaceOrderGUI.Length - 1); //Remove last '\n'
        Debug.Log("[PolePosition] El orden de carrera es: " + myRaceOrderDebug);
        RpcPrintRaceOrder(myRaceOrderGUI); //Print race order in client GUIs
    }

    float ComputeCarArcLength(int id)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        //Vector3 carPos = this._players[id].transform.position;
        Vector3 carPos = this._syncRacerList[id].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this._circuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

#if UNITY_EDITOR
        this._debuggingSpheres[id].transform.position = carProj;
#endif

        /** //Client-side logic
        if(_players[id].CurrentPosition == _circuitController.CircuitPath.positionCount - 2 && segIdx == 0)
        {
            _players[id].ResetPosition();
            _players[id].CurrentLap += 1;
        }

        if (this._players[id].CurrentLap == 0)
        {
            minArcL -= _circuitController.CircuitLength;
            _players[id].CurrentPosition = segIdx;
        }
        else
        {
            minArcL += _circuitController.CircuitLength *
                       (_players[id].CurrentLap - 1);
            _players[id].CurrentPosition = segIdx;
        }

        if (id == 0)
            Debug.Log(_players[id].CurrentPosition);
        //*/

        //Server-side logic
        //Server logic is the same as client-side logic, using _syncPlayerList

        if (_networkManager.GetActiveRace()) {
            if (_syncRacerList[id].GetComponent<PlayerInfo>().CurrentPosition == _numPositions && segIdx == 0) //Add a lap
            {
                _syncRacerList[id].GetComponent<PlayerInfo>().ResetPosition();
                _syncRacerList[id].GetComponent<PlayerInfo>().CurrentLap += 1;
            }

            if (_syncRacerList[id].GetComponent<PlayerInfo>().CurrentPosition == 0 && segIdx == _numPositions) //Remove a lap
            {
                if (_syncRacerList[id].GetComponent<PlayerInfo>().CurrentLap >= 0) //We do not count negative laps
                {
                    _syncRacerList[id].GetComponent<PlayerInfo>().SetMaxPosition(segIdx);
                    _syncRacerList[id].GetComponent<PlayerInfo>().CurrentLap -= 1;
                }
            }

            if (this._syncRacerList[id].GetComponent<PlayerInfo>().CurrentLap <= 0)
            {
                minArcL -= _circuitController.CircuitLength;
                _syncRacerList[id].GetComponent<PlayerInfo>().CurrentPosition = segIdx;
            }
            else
            {
                minArcL += _circuitController.CircuitLength *
                           (_syncRacerList[id].GetComponent<PlayerInfo>().CurrentLap - 1);
                _syncRacerList[id].GetComponent<PlayerInfo>().CurrentPosition = segIdx;
            }
        }

        return minArcL;
    }

    public int GetMaxPosition()
    {
        return _numPositions;
    }

    [ClientRpc]
    private void RpcPrintRaceOrder(string raceOrder)
    {
        if (FindObjectOfType<UIManager>() != null)
            FindObjectOfType<UIManager>().PrintRaceOrder(raceOrder);
        else
            Debug.LogError("UIManager is null");
    }
}