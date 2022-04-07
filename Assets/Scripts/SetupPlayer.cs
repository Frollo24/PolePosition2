using System;
using Mirror;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    //ID and Name of the player
    [SyncVar] private int _id;
    [SyncVar(hook = nameof(NameHandler))] private string _name;

    //Color of the player
    [SyncVar(hook = nameof(ColorHandler))] [SerializeField] private Color renderColor;
    [SerializeField] private TMP_Text displayText = null; //Display name on top of player

    private UIManager _uiManager;
    private MyNetworkManager _networkManager;
    private PlayerController _playerController;
    private PlayerInfo _playerInfo;
    private PolePositionManager _polePositionManager;

    //Synchronization with server
    [SyncVar(hook = nameof(ActiveRaceHandler))][SerializeField]private bool m_isActiveRace; //Is there an active race
    [SyncVar(hook = nameof(LapsHandler))] [SerializeField] private int m_numLaps; //Number of laps of this race
    [SyncVar][SerializeField] private bool m_iAmRacer; //Is this player a racer or not
    [SyncVar(hook = nameof(CanBeRacerHandler))][SerializeField] private bool m_canBeRacer; //Can this player be a racer or not

    public bool IsRacer
    {
        get { return m_iAmRacer; }
    }

    public int NumLaps
    {
        get { return m_numLaps; }
    }

    public PlayerInfo PlayerInfo
    {
        get { return _playerInfo; }
    }

    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        _id = NetworkServer.connections.Count - 1;
        _name = "Player" + _id;
        _playerInfo.Name = _name;

        _playerInfo.CurrentLap = -1; //We do this in order to check race order properly
        _playerInfo.SetMaxPosition(_polePositionManager.GetMaxPosition());
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        _playerInfo.Name = _name;

        m_numLaps = _networkManager.maxLaps;
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
    }

    /// <summary>
    /// Called on the server when a player disconnects.
    /// </summary>
    public override void OnStopServer()
    {
        _polePositionManager.RemovePlayer(netIdentity);
        _networkManager.RemoveNumRacers(m_iAmRacer);
        base.OnStopServer();
    }

    /// <summary>
    /// Called on the client when a player disconnects.
    /// <para>This is called on every client when a unique client disconnects</para>
    /// </summary>
    public override void OnStopClient()
    {
        _networkManager.ReleaseColor(renderColor); //Called in order to restart available colors on client reconnect
        if (hasAuthority)
        {
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = null;
            _uiManager.ActivateMainMenu();
        }
        Physics.autoSimulation = true;
        base.OnStopClient();
    }

    /// <summary>
    /// Called on the client when a player disconnects.
    /// <para>This is called only on the object if the client has authority on it.</para>
    /// </summary>
    public override void OnStopAuthority()
    {
        Camera.main.gameObject.GetComponent<CameraController>().m_Focus = null;
        _uiManager.ActivateMainMenu();
        base.OnStopAuthority();
    }

    #endregion

    #region ControlSyncVars

    #region Color
    private void ColorHandler(Color oldColor, Color newColor)
    {
        GetComponentInChildren<Renderer>().materials[1].color = newColor;
        displayText.color = newColor; //The color of the name is the same as the player color
    }

    [Server]
    public bool SetColor(Color newColor)
    {
        if (newColor == Color.clear)
            return false;

        renderColor = newColor; //Updates the color on the server
        GetComponentInChildren<Renderer>().materials[1].color = newColor;
        displayText.color = newColor; //Updates the text color on the server
        return true;
    }

    public Color GetColor()
    {
        return renderColor;
    }

    [Command]
    public void CmdUpdateColor(uint id) //Petition to server to change the color
    {
        SetColor(_networkManager.ChooseSpecificColor(id, renderColor));
    }

#if UNITY_EDITOR
    [Command]
    [ContextMenu("Update to yellow color")]
    public void CmdUpdateToYellow() //Petition to server to change to yellow color, only available on UnityEditor.
    {
        SetColor(_networkManager.ChooseSpecificColor(6, renderColor));
    }
#endif

#endregion

    #region Name
    private void NameHandler(string oldDisplay, string newDisplay)
    {
        displayText.text = newDisplay; //Displays the name on top of the player
        _playerInfo.Name = newDisplay; //Updates player info name as well
    }

    [Server]
    public void SetName(string name)
    {
        if (name.Length < 3) return; //Name must be at least 3 characters long
        _name = name;
        displayText.text = name; //Updates player display name
        _playerInfo.Name = name; //Updates player info name as well
    }

    public string GetName()
    {
        return _name;
    }

    [Command]
    public void CmdUpdateName(string newName)
    {
        SetName(newName.ToUpper()); //Player selected name always appears in uppercase
    }

    [Command]
    public void CmdUpdateNameLower(string newName)
    {
        SetName(newName); //Called when a name is selected by the server
    }

    #endregion

    #region CanBeRacer

    [Command]
    public void CmdGetCanBeRacer()
    {
        UpdateCanBeRacer();
    }

    [Server]
    public void UpdateCanBeRacer()
    {
        m_canBeRacer = _networkManager.AvailableRacers();
    }

    public void CanBeRacerHandler(bool oldValue, bool newValue)
    {
        m_canBeRacer = newValue;
        _uiManager.CanBeRacer = newValue; //If a player cannot be a racer, don't let them select it
    }

    #endregion

    #region ActiveRace

    [Command]
    public void CmdGetActiveRace()
    {
        UpdateActiveRace();
    }

    [Server]
    public void StopRaceInServer() //Called when a player finish a race
    {
        _uiManager.StopRace();
    }

    [Server]
    public void UpdateActiveRace()
    {
        m_isActiveRace = _networkManager.GetActiveRace();
    }

    public void ActiveRaceHandler(bool oldValue, bool newValue)
    {
        m_isActiveRace = newValue;
    }

    #endregion

    #region NumLaps

    [Command]
    public void CmdGetMaxLaps()
    {
        UpdateMaxLaps();
    }

    [Server]
    public void UpdateMaxLaps()
    {
        m_numLaps = _networkManager.maxLaps;
    }

    public void LapsHandler(int oldValue, int newValue)
    {
        m_numLaps = newValue;
    }

    #endregion

    #endregion

    #region Other Callbacks

    [Command]
    public void CmdAddRacer(bool isRacer)
    {
        int newId = _networkManager.NumRacers;
        //You cannot be a racer if there is no available races left OR if there is an active race
        if (!_networkManager.AvailableRacers() || _networkManager.GetActiveRace())
            isRacer = false;

        m_iAmRacer = isRacer;
        _networkManager.AddNumRacers(isRacer);
        if(!isRacer)
            SetName("spectator");
        else
            _polePositionManager.AddPlayer(netIdentity); //We add the player to the list if it is a racer

        _playerInfo.ID = isRacer ? newId : 9999; //We set the ID to 9999 if player is a spectator

        TargetRacerOrSpectator(isRacer);
    }

    [TargetRpc]
    public void TargetRacerOrSpectator(bool isRacer)
    {
        _uiManager.RacerOrSpectator(isRacer);
    }

    [Command]
    public void CmdPlayerReady()
    {
        _playerController.AllowMovement(); //Allow movement when player is ready
    }

    [ClientRpc]
    public void RpcStartRacing(int id) //Server starts a race, and teleports each racer to a concrete position
    {
        _playerController.CanMove = m_iAmRacer;
        if(hasAuthority)
            _playerController.CmdStartRace(id);
        Debug.LogWarning("Starting race");
    }

    [ClientRpc]
    public void RpcStopRacing() //Server stops the race, and teleport each racer to a "lobby" position
    {
        _playerController.CanMove = true;
        if (hasAuthority)
            _playerController.CmdStopRace();
        Debug.LogError("Stoping race");
    }

    [Command]
    public void CmdFinishRace() //Player finishes a race, and makes a petition to server to stop the race
    {
        Debug.LogWarning("Finishing race");
        _networkManager.IsActiveRace = false;
        StopRaceInServer();
        RpcStopRacing();
    }

    #endregion

    #region Tests

#if UNITY_EDITOR
    [ClientRpc]
    [ContextMenu("Allow Movement")]
    public void AllowMovement() //Allows movement to a player, only available in UnityEditor.
    {
        _playerController.CanMove = true;
    }
#endif

    [ClientRpc]
    public void RpcPrintId(int id) //Prints a number, only for debugging purposes
    {
        Debug.Log("ID: " + id);
    }

    #endregion

    private void Awake()
    {
        _playerInfo = GetComponent<PlayerInfo>();
        _playerController = GetComponent<PlayerController>();
        _networkManager = FindObjectOfType<MyNetworkManager>();
        _polePositionManager = FindObjectOfType<PolePositionManager>();
        _uiManager = FindObjectOfType<UIManager>();
        
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            _playerController.enabled = true;
            _playerController.OnSpeedChangeEvent += OnSpeedChangeGUI;
            _playerInfo.OnLapChangeEvent += OnLapChangeGUI;
            _playerInfo.OnWrongWayEvent += OnWrongWayGUI;

            CmdGetMaxLaps();
        }
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            ConfigureCamera();
            CmdGetActiveRace();
            CmdGetCanBeRacer();
            _uiManager.SwitchWaitUntilStartRace(m_isActiveRace);
        }
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            if (_playerInfo.CurrentLap >= m_numLaps && m_isActiveRace) //Checks if a player finish a race
                CmdFinishRace();
        }
    }

    void OnSpeedChangeGUI(float speed)
    {
        _uiManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    void OnLapChangeGUI(int laps)
    {
        if (laps < 0) 
            _uiManager.UpdateLaps(0); //We don't want to show a negative number of laps
        else
            _uiManager.UpdateLaps(laps);
    }

    void OnWrongWayGUI(bool wrong)
    {
        _uiManager.UpdateWrong(wrong);
    }

    void ConfigureCamera()
    {
        if (Camera.main != null && m_isActiveRace) //Check if camera should focus a player or not
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = GetFocus();
        else
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = null;
    }

    GameObject GetFocus()
    {
        if (m_iAmRacer) //If player is a racer, camera should focus him
            return this.gameObject;
        else
            return Camera.main.gameObject.GetComponent<CameraController>().GetOtherPlayerFocus();
    }
}