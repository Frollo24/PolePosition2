using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private MyNetworkManager m_NetworkManager;
    private PolePositionManager m_PolePositionManager;
    private SetupPlayer m_SetupPlayer;

    /*************************/
    /******* MAIN MENU *******/
    /*************************/
    [Header("Main Menu")] [SerializeField] 
    private GameObject mainMenu;
    
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;

    /***********************/
    /******* IN-GAME *******/
    /***********************/
    [Header("In-Game HUD")] [SerializeField]
    private GameObject inGameHUD;

    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] private GameObject textWrongWay;
    [SerializeField] private GameObject textWaitUntilStart;
    [SerializeField] private Button buttonDisconnectInGame;

    public bool CanBeRacer { get; set; }

    /**********************/
    /******* CLIENT *******/
    /**********************/
    [Header("ClientHUD")] [SerializeField]
    private GameObject clientHUD;

    [SerializeField] private InputField inputName;
    [SerializeField] private Button buttonName;
    [SerializeField] private Toggle toggleRacer;
    [SerializeField] private Button buttonClientReady;
    [SerializeField] private Button buttonGoBackLobby;

    [SerializeField] private GameObject colorPrefab;
    private GameObject[] colorPrefabList;

    /*********************************/
    /******* SERVER PROPERTIES *******/
    /*********************************/
    [Header("ServerPropertiesHUD")] [SerializeField]
    private GameObject serverPropertiesHUD;

    //MaxConnections
    [SerializeField] private Button minusButtonConn;
    [SerializeField] private Button plusButtonConn;
    [SerializeField] private Text textNumConnections;
    //MaxRacers
    [SerializeField] private Button minusButtonRacers;
    [SerializeField] private Button plusButtonRacers;
    [SerializeField] private Text textNumRacers;
    //NumLaps
    [SerializeField] private Button minusButtonLaps;
    [SerializeField] private Button plusButtonLaps;
    [SerializeField] private Text textNumLaps;
    //Start and Go Back
    [SerializeField] private Button buttonStartServer;
    [SerializeField] private Button buttonGoBackServer;

    private int numConnections;
    private int numRacers;
    private int numLaps;

    /****************************/
    /******* SERVER READY *******/
    /****************************/
    [Header("ServerReadyHUD")] [SerializeField]
    private GameObject serverReadyHUD;

    [SerializeField] private Button buttonStartRace;
    [SerializeField] private Button buttonStopServer;

    /*************************/
    /******* SPECTATOR *******/
    /*************************/
    [Header("ServerReadyHUD")] [SerializeField]
    private GameObject spectatorHUD;

    [SerializeField] private Button buttonDisconnectSpectator;

    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<MyNetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();

        numConnections = 4;
        numRacers = 4;
        numLaps = 3;

        inputName.characterLimit = 11;
    }

    private void Start()
    {
        buttonClient.onClick.AddListener(() => ClientSelected());
        buttonServer.onClick.AddListener(() => ServerSelected());

        //Instantiate color prefabs
        colorPrefabList = new GameObject[16];

        for (uint i = 0; i < m_NetworkManager.colors.Length; i++)
        {
            GameObject newColor = Instantiate(colorPrefab);
            
            newColor.transform.SetParent(clientHUD.transform, false);
            newColor.transform.Translate(Vector3.right * 40 * (i % 4));
            newColor.transform.Translate(Vector3.down * 40 * (i / 4));
            newColor.GetComponentInChildren<Image>().color = m_NetworkManager.colors[i];
            
            colorPrefabList[i] = newColor;
        }

        for (uint i = 0; i < colorPrefabList.Length; i++)
        {
            uint color = i; //This is because using iteration variable when adding events does not work
            colorPrefabList[i].GetComponent<Button>().onClick.AddListener(() => {
                ChangeColor(color);
            });
        }

        //Client buttons
        buttonName.onClick.AddListener(() => ChangeName());

        buttonClientReady.onClick.AddListener(() => PlayerReady());
        buttonGoBackLobby.onClick.AddListener(() => {
            m_NetworkManager.StopClient();
            Debug.LogWarning(m_NetworkManager.numPlayers);
            ActivateMainMenu();
        });

        buttonDisconnectInGame.onClick.AddListener(() => m_NetworkManager.StopClient());
        buttonDisconnectSpectator.onClick.AddListener(() => m_NetworkManager.StopClient());

        //Server buttons
        minusButtonConn.onClick.AddListener(() => MinusButtonConnections());
        plusButtonConn.onClick.AddListener(() => PlusButtonConnections());

        minusButtonRacers.onClick.AddListener(() => MinusButtonRacers());
        plusButtonRacers.onClick.AddListener(() => PlusButtonRacers());

        minusButtonLaps.onClick.AddListener(() => MinusButtonLaps());
        plusButtonLaps.onClick.AddListener(() => PlusButtonLaps());

        buttonStartServer.onClick.AddListener(() => LaunchServer());
        buttonGoBackServer.onClick.AddListener(() => ActivateMainMenu());

        //Server ready buttons
        buttonStartRace.onClick.AddListener(() => StartRace());
        buttonStopServer.onClick.AddListener(() => StopServer());

        ActivateMainMenu();
    }

    private void Update()
    {
        CheckButtonInteractable();
        if (m_SetupPlayer == null)
            FindPlayerAuthority();
    }

    #region GUI Delegates
    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    public void UpdateLaps(int laps)
    {
        textLaps.text = "LAP: " + laps + "/" + m_SetupPlayer.NumLaps;
    }

    public void UpdateWrong(bool wrong)
    {
        textWrongWay.SetActive(wrong);
    }

    #endregion

    void CheckButtonInteractable()
    {
        //Client Buttons
        if(buttonName.isActiveAndEnabled)
            buttonName.interactable = !string.IsNullOrEmpty(inputName.text);

        if (toggleRacer.isActiveAndEnabled)
        {
            toggleRacer.interactable = CanBeRacer;
        }
        if (!toggleRacer.interactable)
        {
            toggleRacer.isOn = false;
        }

        //Server Buttons
        if (buttonStartRace.isActiveAndEnabled)
            buttonStartRace.interactable = m_NetworkManager.CanRaceStart();
    }

    void FindPlayerAuthority()
    {
        foreach (var setup in FindObjectsOfType<SetupPlayer>())
        {
            if (setup.hasAuthority)
                m_SetupPlayer = setup;
        }
    }

    #region HUD_Activation
    public void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        clientHUD.SetActive(false);
        serverPropertiesHUD.SetActive(false);
        serverReadyHUD.SetActive(false);
        spectatorHUD.SetActive(false);
    }
    private void ActivateInGameHUD()
    {
        mainMenu.SetActive(false);
        clientHUD.SetActive(false);
        inGameHUD.SetActive(true);

        textLaps.text = "LAP: 0/" + m_SetupPlayer.NumLaps;
    }
    private void ActivateClientHUD()
    {
        mainMenu.SetActive(false);
        clientHUD.SetActive(true);

        inputName.text = string.Empty;
    }
    private void ActivateServerPropertiesHUD()
    {
        mainMenu.SetActive(false);
        serverReadyHUD.SetActive(false);
        serverPropertiesHUD.SetActive(true);
    }

    private void ActivateInServerHUD()
    {
        serverReadyHUD.SetActive(true);
    }

    private void ActivateSpectatorHUD()
    {
        mainMenu.SetActive(false);
        clientHUD.SetActive(false);
        spectatorHUD.SetActive(true);
    }

    public void RacerOrSpectator(bool isRacer)
    {
        if (isRacer)
            ActivateInGameHUD();
        else
            ActivateSpectatorHUD();
    }

    #endregion

    #region MainMenuSelection

    private void ClientSelected()
    {
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = (!string.IsNullOrEmpty(inputFieldIP.text)) ? inputFieldIP.text : "localhost";
        ActivateClientHUD();
    }

    private void ServerSelected()
    {
        ActivateServerPropertiesHUD();
    }

    #endregion

    #region ClientScreen

    public void SwitchWaitUntilStartRace(bool isActiveRace)
    {
        textWaitUntilStart.SetActive(!isActiveRace);
    }

    private void ChangeColor(uint id)
    {
        m_SetupPlayer.CmdUpdateColor(id);
    }

    private void ChangeName()
    {
        m_SetupPlayer.CmdUpdateName(inputName.text);
    }

    private void PlayerReady()
    {
        if (!string.IsNullOrEmpty(inputName.text))
            m_SetupPlayer.CmdUpdateName(inputName.text);
        else
            m_SetupPlayer.CmdUpdateNameLower(m_SetupPlayer.GetName());

        m_SetupPlayer.CmdAddRacer(toggleRacer.isOn);
        m_SetupPlayer.CmdPlayerReady();
    }

    public void PrintRaceOrder(string raceOrder)
    {
        textPosition.text = raceOrder;
    }

    #endregion

    #region ServerScreen
    private void MinusButtonConnections()
    {
        numConnections--;
        if (numConnections < 2) numConnections = 2;
        if (numConnections < numRacers) 
        { 
            numRacers = numConnections;
            textNumRacers.text = numRacers.ToString();
        }
        textNumConnections.text = numConnections.ToString();
    }

    private void PlusButtonConnections()
    {
        numConnections++;
        if (numConnections > 8) numConnections = 8;
        textNumConnections.text = numConnections.ToString();
    }
    
    private void MinusButtonRacers()
    {
        numRacers--;
        if (numRacers < 2) numRacers = 2;
        textNumRacers.text = numRacers.ToString();
    }

    private void PlusButtonRacers()
    {
        if (numRacers < numConnections) numRacers++;
        if (numRacers > 4) numRacers = 4;
        textNumRacers.text = numRacers.ToString();
    }

    private void MinusButtonLaps()
    {
        numLaps--;
        if (numLaps < 2) numLaps = 2;
        textNumLaps.text = numLaps.ToString();
    }

    private void PlusButtonLaps()
    {
        numLaps++;
        if (numLaps > 6) numLaps = 6;
        textNumLaps.text = numLaps.ToString();
    }

    private void LaunchServer()
    {
        m_NetworkManager.maxConnections = numConnections;
        m_NetworkManager.maxRacers = numRacers;
        m_NetworkManager.maxLaps = numLaps;
        m_NetworkManager.StartServer();

        serverPropertiesHUD.SetActive(false);
        ActivateInServerHUD();
    }

    #endregion

    #region Server Ready

    private void StartRace()
    {
        int i = 0;
        var foo = FindObjectsOfType<SetupPlayer>();
        foreach (var setup in foo)
        {
            setup.RpcPrintId(i);
            if (setup.IsRacer)
            {
                setup.RpcStartRacing(i);
                //TODO This line fails when server runs on a build, because m_PolePositionManager
                //TODO Not hardcode a 23
                setup.GetComponent<PlayerInfo>().SetMaxPosition(23 - i);
                setup.RpcPrintId(i + 1);
                i++;
            }
            setup.RpcPrintId(i);
        }
        m_NetworkManager.IsActiveRace = true;
        buttonStartRace.GetComponentInChildren<Text>().text = "Stop Race";
        buttonStartRace.onClick.RemoveAllListeners();
        buttonStartRace.onClick.AddListener(() => StopRace());
    }

    [Server]
    public void StopRace()
    {
        foreach (var setup in FindObjectsOfType<SetupPlayer>())
        {
            setup.RpcStopRacing(); 
            setup.GetComponent<PlayerInfo>().SetMaxPosition(23);
            setup.GetComponent<PlayerInfo>().CurrentLap = -1;
        }
        m_NetworkManager.IsActiveRace = false;
        buttonStartRace.GetComponentInChildren<Text>().text = "Start Race";
        buttonStartRace.onClick.RemoveAllListeners();
        buttonStartRace.onClick.AddListener(() => StartRace());
    }

    private void StopServer()
    {
        m_NetworkManager.StopServer();
        ActivateServerPropertiesHUD();
    }

    #endregion

}