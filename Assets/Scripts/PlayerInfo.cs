using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInfo : NetworkBehaviour
{
    //Name of the player
    [SyncVar][SerializeField] private string _name;
    public string Name {
        get { return _name; }
        set { _name = value; }
    }

    //ID of the player (9999 ID means a spectator player)
    [SyncVar] [SerializeField] private int _id;
    public int ID {
        get { return _id; }
        set { _id = value; } 
    }

    //Position of the player on the circuit
    [SyncVar][SerializeField] private int m_currentPosition;
    public int CurrentPosition {
        get { return m_currentPosition; } 
        set {
            //We must travel circuit checkpoints in order
            if (m_currentPosition + 1 != value && m_currentPosition - 1 != value) return;

            //Check if going wrong way
            if (value < m_currentPosition)
                WrongWay = true;
            if (value > m_currentPosition)
                WrongWay = false;

            m_currentPosition = value;
        } 
    }

    //Resets position when player completes a lap
    public void ResetPosition()
    {
        m_currentPosition = 0;
    }

    //Set the position at the start of the circuit
    //And updates it if player makes a lap backwards
    public void SetMaxPosition(int pos)
    {
        m_currentPosition = pos;
    }

    //Is the player going wrong way on the circuit
    [SyncVar(hook = nameof(HandleUpdateWrong))][SerializeField] private bool m_WrongWay;
    public bool WrongWay
    {
        get { return m_WrongWay; }
        set {
            m_WrongWay = value;
            OnWrongWayEvent?.Invoke(m_WrongWay);
        }
    }

    public delegate void OnWrongWayDelegate(bool newVal);

    public event OnWrongWayDelegate OnWrongWayEvent;


    //Current lap of the player
    [SyncVar(hook = nameof(HandleUpdateLap))][SerializeField] private int m_currentLap;
    //Max lap made by the player. Prevents from number of laps going down on GUI
    [SyncVar][SerializeField] private int m_maxLap; 
    
    public int CurrentLap { 
        get { return m_currentLap; } 
        set
        {
            m_currentLap = value;
            if (value > m_maxLap)
            {
                m_maxLap = value;
                OnLapChangeEvent?.Invoke(m_currentLap);
            }
        }
    }

    public delegate void OnLapChangeDelegate(int newVal);

    public event OnLapChangeDelegate OnLapChangeEvent;

    public void HandleUpdateWrong(bool oldWrong, bool newWrong)
    {
        OnWrongWayEvent?.Invoke(newWrong);
    }
    
    public void HandleUpdateLap(int oldLap, int newLap)
    {
        OnLapChangeEvent?.Invoke(newLap);
    }

    public override string ToString()
    {
        return Name;
    }
}