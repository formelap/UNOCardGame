using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MP_PlayerGUI : MonoBehaviour
{
    public Text playerName;

    [ClientCallback]
    public void SetPlayerInfo(PlayerInfo info)
    {
        playerName.text = $"Gracz #{info.playerIndex}";
        playerName.color = info.ready ? Color.green : Color.grey;
    }
}