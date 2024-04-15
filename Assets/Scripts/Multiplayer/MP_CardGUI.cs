using Mirror;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MP_CardGUI : MonoBehaviour
{
    public CardData cardData;
    public Image cardImage;

    public MP_GameController gameController;
    public GameObject card;
    public NetworkIdentity owner;

    public void UpdateCardData(CardData _cardData)
    {
        cardData = _cardData;
    }
    public void UpdateCardAppearance()
    {
        string color = cardData.cardColor.ToString();
        string number = cardData.cardValue.ToString("D");
        string path = "Textures/Cards/Front/" + color + "/" + number;

        Sprite sprite = Resources.Load<Sprite>(path);
        cardImage.sprite = sprite;
    }
}
