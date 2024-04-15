using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MP_DrawPileGUI : NetworkBehaviour
{
    public MP_GameController gameController;

    [Header("Diagnostics")]
    public NetworkIdentity playerIdentity;

    public void OnClick()
    {
        if (!gameController.currentPlayer.isLocalPlayer)
            return;

        if (!gameController.CardsDealt)
        {
            gameController.CmdDealCards();
        }
        else
        {
            gameController.CmdDrawCard();
        }
            
    }


}
