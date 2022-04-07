using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

using Random = UnityEngine.Random;

public class MyNetworkManager : NetworkManager
{
    //Color selection
    [Header("Color List")]public Color[] colors = new Color[16];
    [SerializeField] private bool[] isColorChoosed = new bool[16];

    //Number of racers
    public int maxRacers = 0;
    [SerializeField] private int m_numRacers = 0;

    public int NumRacers
    {
        get { return m_numRacers; }
    }

    //Number of laps
    public int maxLaps = 0;
    
    //Active race management
    [SerializeField] private bool m_IsActiveRace = false;

    public bool IsActiveRace
    {
        set { m_IsActiveRace = value; }
    }

    #region Mirror Callbacks

    public override void OnStopServer()
    {
        maxRacers = 0;
        maxLaps = 0;

        base.OnStopServer();
    }
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        if(conn.identity != null)
            ReleaseColor(conn.identity.GetComponent<SetupPlayer>().GetColor()); //Make color active for other players
        Debug.Log("[SERVER] Se ha desconectado el cliente: " + conn.connectionId);

        base.OnServerDisconnect(conn);
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);

        Debug.Log("[SERVER] Se ha conectado un nuevo cliente: " + conn.connectionId);
    }

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        base.OnServerAddPlayer(conn);

        Debug.Log("[SERVER] Jugadores conectados: " + numPlayers);

        SetupPlayer player = conn.identity.GetComponent<SetupPlayer>();
        player.SetName("Guest" + conn.connectionId); //Random Guest name
        player.SetColor(ChooseRandomColor()); //Start with random color
    }

    #endregion

    public override void Awake()
    {
        base.Awake();
        m_numRacers = 0;
    }

    private void Update()
    {
        if (m_numRacers < 2 && m_IsActiveRace && NetworkServer.active) //If there is one racer left in an active race, he wins
            FindObjectOfType<UIManager>().StopRace();                  //And we stop the race (win by default)
    }

    #region Color Managenent

    [Server]
    public Color ChooseRandomColor()
    {
        int choose = (int)Random.Range(0, 15.9f); //Random ID from 0 to 15
        while (isColorChoosed[choose])
        {
            choose = (int)Random.Range(0, 15.9f); //We try until we get an available color
        }

        isColorChoosed[choose] = true; //Prevent that color from being choose by other player

        return colors[choose];
    }

    [Server]
    public Color ChooseSpecificColor(uint id, Color colorToRelease)
    {
        try
        {
            if (!isColorChoosed[id]) //Check if new color is choosed
            {
                isColorChoosed[id] = true;
                ReleaseColor(colorToRelease);
                return colors[id];
            }

            else
                return Color.clear; //Return Color.clear if we can't change the color
        }
        catch (IndexOutOfRangeException ioore)
        {
            Debug.LogError(ioore);
            return Color.clear; //Return Color.clear if we can't change the color
        }
    }

    public void ReleaseColor(Color color)
    {
        for (int i = 0; i < colors.Length; i++)
            if (color == colors[i])
            {
                isColorChoosed[i] = false; //Make the color available for other players
                break;
            }
    }

    public bool GetColorChoosed(uint id)
    {
        return isColorChoosed[id];
    }

    #endregion

    #region Racers Management

    [Server]
    public void AddNumRacers(bool isRacer) //Adds a racer if player ready is a new racer
    {
        if (!isRacer) return;

        m_numRacers++;
    }

    [Server]
    public void RemoveNumRacers(bool isRacer) //Removes a racer if disconnected player is a racer
    {
        if (!isRacer) return;

        m_numRacers--;
    }

    [Server]
    public bool AvailableRacers() //Are there available racers in the server or not
    {
        return m_numRacers < maxRacers;
    }

    [Server]
    public bool CanRaceStart() //A race can start when there are two or more racers
    {
        return m_numRacers > 1;
    }

    [Server]
    public bool GetActiveRace() //Is there an active race or not
    {
        return m_IsActiveRace;
    }

    #endregion
}
