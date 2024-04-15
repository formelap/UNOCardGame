using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Informacje o karcie
public struct CardData
{
    public CardColor cardColor;
    public CardValue cardValue;

    public CardData(CardColor color, CardValue value)
    {
        cardColor = color;
        cardValue = value;
    }
}

// Kolor karty na obiekcie karty (GameObject)
public enum CardColor
{
    Yellow,
    Green,
    Red,
    Blue,
    Wild
}

// Wartoœæ karty na obiekcie karty (GameObject)
public enum CardValue
{
    One = 1, 
    Two = 2, 
    Three = 3, 
    Four = 4, 
    Five = 5,
    Six = 6, 
    Seven = 7, 
    Eight = 8, 
    Nine = 9, 
    SkipTurn = 10, 
    DrawTwo = 11, 
    ChooseColor = 12,
    DrawFourChangeColor = 13,
}

public struct ServerMatchMessage : NetworkMessage
{
    public ServerMatchOperation serverMatchOperation;
    public Guid matchId;
}

// Wiadomoœæ o meczu do wys³ania do klienta
public struct ClientMatchMessage : NetworkMessage
{
    public ClientMatchOperation clientMatchOperation;
    public Guid matchId;
    public MatchInfo[] matchInfos;
    public PlayerInfo[] playerInfos;
}

// Informacje o meczu
[Serializable]
public struct MatchInfo
{
    public Guid matchId;
    public byte players;
    public byte maxPlayers;
}

// Informacje o graczu
[Serializable]
public struct PlayerInfo
{
    public int playerIndex;
    public bool ready;
    public Guid matchId;
}

[Serializable]
public struct MatchPlayerData
{
    public int playerIndex;
    public int wins;
}

// Operacje meczu do wykonania na serwerze
public enum ServerMatchOperation : byte
{
    None,
    Create,
    Cancel,
    Start,
    Join,
    Leave,
    Ready
}

// Operacje meczu do wykonania na kliencie
public enum ClientMatchOperation : byte
{
    None,
    List,
    Created,
    Cancelled,
    Joined,
    Departed,
    UpdateRoom,
    Started
}


