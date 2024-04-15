using Mirror;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkMatch))]
public class MP_GameController : NetworkBehaviour
{
    [Header("Diagnostics")]
    internal readonly SyncDictionary<NetworkIdentity, MatchPlayerData> matchPlayerData = new SyncDictionary<NetworkIdentity, MatchPlayerData>();
    public MP_CanvasController canvasController;
    public NetworkIdentity player1;
    public NetworkIdentity player2;
    [SyncVar(hook = nameof(UpdateGameUI))]
    public NetworkIdentity startingPlayer;
    [SyncVar(hook = nameof(UpdateGameUI))]
    public NetworkIdentity currentPlayer;
    [SyncVar]
    public bool CardsDealt;
    internal bool playAgain = false;

    [Header("Card prefabs and deck data structures")]
    internal readonly SyncList<CardData> deckCards = new SyncList<CardData>();
    internal readonly SyncList<CardData> discardedCards = new SyncList<CardData>();
    public GameObject CardPrefab;
    public GameObject BackCard;

    [Header("GUI References")]
    public CanvasGroup canvasGroup;
    public Text gameText;
    public Button exitButton;
    public Button playAgainButton;
    public Text winCountLocal;
    public Text winCountOpponent;
    public GameObject playerArea;
    public GameObject opponentArea;
    public GameObject discardPile;
    [SyncVar(hook = nameof(UpdateDiscardPileData))]
    CardData CardOnDiscardPileData;

    [Header("Color Picker GUI")]
    public GameObject colorPicker;
    public Button redButton;
    public Button greenButton;
    public Button blueButton;
    public Button yellowButton;
    internal Dictionary<Button, CardColor> colorButtons;

    [Header("Dialogue Box GUI")]
    [SyncVar(hook = nameof(UpdateComments))]
    internal string DialogueText;
    public ScrollRect scrollRect;
    public TextMeshProUGUI DialogueBox;

    #region Game initialization functions

    void Awake()
    {
        canvasController = FindObjectOfType<MP_CanvasController>();
    }

    public override void OnStartServer()
    {
        StartCoroutine(AddPlayersToGameController());

        deckCards.Clear();
        discardedCards.Clear();
        StartCoroutine(AddCardsToGameController());

    }

    public override void OnStartClient()
    {
        matchPlayerData.Callback += UpdateWins;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        exitButton.gameObject.SetActive(false);
        playAgainButton.gameObject.SetActive(false);
        colorPicker.gameObject.SetActive(false);

        colorButtons = new Dictionary<Button, CardColor>
        {
            {redButton, CardColor.Red},
            {greenButton, CardColor.Green},
            {blueButton, CardColor.Blue},
            {yellowButton, CardColor.Yellow}
        };

    }

    // Aby SyncDictionary właściwie wywołało callback aktualizacji, musimy
    // poczekać klatkę przed dodaniem graczy do już uruchomionego GameController
    IEnumerator AddPlayersToGameController()
    {
        yield return null;

        matchPlayerData.Add(player1, new MatchPlayerData { playerIndex = MP_CanvasController.playerInfos[player1.connectionToClient].playerIndex });
        matchPlayerData.Add(player2, new MatchPlayerData { playerIndex = MP_CanvasController.playerInfos[player2.connectionToClient].playerIndex });
    }

    IEnumerator AddCardsToGameController()
    {
        yield return null;

        foreach(CardColor cardColor in EnumUtil.GetValues<CardColor>())
        {
            if (cardColor == CardColor.Wild)
            {
                for(int i = 0; i < 4; i++)
                {
                    CardData chooseColorCardInfo = new CardData(cardColor, CardValue.ChooseColor);
                    deckCards.Add(chooseColorCardInfo);

                    CardData drawFourCardInfo = new CardData(cardColor, CardValue.DrawFourChangeColor);
                    deckCards.Add(drawFourCardInfo);
                }

                continue;
            }

            foreach(CardValue cardValue in EnumUtil.GetValues<CardValue>())
            {
                if (cardValue == CardValue.ChooseColor || cardValue == CardValue.DrawFourChangeColor) 
                    continue;

                CardData cardInfo = new CardData(cardColor, cardValue);
                deckCards.Add(cardInfo);

            }
        }
    }

    // klasa pomocnicza do iteracji przez enumy
    public static class EnumUtil
    {
        public static IEnumerable<T> GetValues<T>()
        {
            return System.Enum.GetValues(typeof(T)).Cast<T>();
        }
    }

    #endregion

    #region SyncVar functions

    [ClientCallback]
    public void UpdateGameUI(NetworkIdentity _, NetworkIdentity newPlayerTurn)
    {
        StartCoroutine(UpdateGameUICoroutine(newPlayerTurn));
    }

    IEnumerator UpdateGameUICoroutine(NetworkIdentity newPlayerTurn)
    {
        yield return null;

        if (!newPlayerTurn)
            yield break;

        if (!CardsDealt)
        {
            if (newPlayerTurn.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                gameText.text = "Rozdaj karty";
            }
            else
            {
                gameText.text = "";
            }
        }
        else
        {
            if (newPlayerTurn.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                gameText.text = "Twoja tura";
            }
            else
            {
                gameText.text = "Tura przeciwnika";
            }
        }
    }

    [ClientCallback]
    void UpdateComments(string _, string newDialogueText)
    {
        DialogueBox.text += newDialogueText + "\n";
        StartCoroutine(UpdateScroll());
    }

    IEnumerator UpdateScroll()
    {
        yield return null;
        scrollRect.verticalNormalizedPosition = 0;
    }

    [ClientCallback]
    public void UpdateWins(SyncDictionary<NetworkIdentity, MatchPlayerData>.Operation op, NetworkIdentity key, MatchPlayerData matchPlayerData)
    {
        if (key.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
            winCountLocal.text = $"Gracz #{matchPlayerData.playerIndex}: {matchPlayerData.wins}";
        else
            winCountOpponent.text = $"Gracz #{matchPlayerData.playerIndex}: {matchPlayerData.wins}";
    }

    [ClientCallback]
    void UpdateDiscardPileData(CardData _, CardData newCardData)
    {
        string color = newCardData.cardColor.ToString();
        string number = newCardData.cardValue.ToString("D");
        string path = "Textures/Cards/Front/" + color + "/" + number;

        Sprite sprite = Resources.Load<Sprite>(path);
        discardPile.GetComponent<Image>().sprite = sprite;
    }

    #endregion

    #region Command functions for game logic

    // rozdaje karty każdemu graczowi, jeśli CardsDealt = false
    // Wywoływane z "MP_DrawPileGUI.OnClick"
    [Command(requiresAuthority = false)]
    public void CmdDealCards(NetworkConnectionToClient sender = null)
    {
        CardData firstCardData;
        int numOfCards = 7;
        float timeDelay = 0.0f;
        float delayStep = 0.2f;

        foreach (NetworkIdentity player in matchPlayerData.Keys)
        {
            
            for(int i = 0; i < numOfCards;  i++)
            {
                StartCoroutine(DelayedDrawCard(player, timeDelay));
                
                timeDelay += delayStep;
            }
        }

        CardsDealt = true;

        Invoke(nameof(RpcUpdateGameUI), timeDelay);

        do
        {
            firstCardData = deckCards[Random.Range(0, deckCards.Count)];
        }
        while (firstCardData.cardColor == CardColor.Wild);

        deckCards.Remove(firstCardData);
        discardedCards.Add(firstCardData);
        CardOnDiscardPileData = firstCardData;

    }

    // daje kartę aktualnemu graczowi, jeśli CardsDealt = true.
    // Wywoływane z "MP_DrawPileGUI.OnClick"
    [Command(requiresAuthority = false)]
    public void CmdDrawCard(NetworkConnectionToClient sender = null)
    {
        // jeśli niewłaściwy gracz to zignoruj
        if (sender.identity != currentPlayer)
            return;

        RpcDrawCard(sender.identity);

        int playerIndex = matchPlayerData[currentPlayer].playerIndex;
        string message = "Gracz #" + playerIndex + " wziął kartę ze stosu";
        AudioManager.Instance.PlaySfx("CardSound");
        CmdUpdateComments(message);

        currentPlayer = currentPlayer == player1 ? player2 : player1;

    }

    [Command(requiresAuthority = false)]
    void CmdRemoveCardFromDeck(CardData cardData)
    {
        deckCards.Remove(cardData);

        

        if (deckCards.Count != 0)
            return;

        List<CardData> cardsToMove = new List<CardData>();

        // znajdź karty do przeniesienia
        foreach (CardData card in discardedCards)
        {
            if (!card.Equals(CardOnDiscardPileData))
            {
                cardsToMove.Add(card);
            }
        }

        // Przenieś karty
        foreach (CardData card in cardsToMove)
        {
            discardedCards.Remove(card);
            deckCards.Add(card);
        }
    }

    [Command(requiresAuthority = false)]
    void CmdDiscardCard(CardData cardData)
    {
            
        discardedCards.Add(cardData);
        RpcUpdateOpponentBoard(currentPlayer);

        CardOnDiscardPileData = cardData;
        

    }

    [Command(requiresAuthority = false)]
    void CmdUpdateDiscardCard(CardData cardData)
    {
        CardOnDiscardPileData = cardData;
    }

    [Command(requiresAuthority = false)]
    public void CmdOpponentDrawTwo(NetworkConnectionToClient sender = null)
    {
        NetworkIdentity opponent = matchPlayerData.Keys.FirstOrDefault(networkIdentity => networkIdentity != currentPlayer);

        if (opponent == null) 
            return;

        int numOfCards = 2;
        float timeDelay = 0.0f;
        float delayStep = 0.2f;

        for (int i = 0; i < numOfCards; i++)
        {
            StartCoroutine(DelayedDrawCard(opponent, timeDelay));

            timeDelay += delayStep;

        }

    }

    [Command(requiresAuthority = false)]
    public void CmdOpponentDrawFour(NetworkConnectionToClient sender = null)
    {
        NetworkIdentity opponent = matchPlayerData.Keys.FirstOrDefault(networkIdentity => networkIdentity != currentPlayer);

        if (opponent == null)
            return;

        int numOfCards = 4;
        float timeDelay = 0.0f;
        float delayStep = 0.2f;

        for (int i = 0; i < numOfCards; i++)
        {
            StartCoroutine(DelayedDrawCard(opponent, timeDelay));

            timeDelay += delayStep;

        }

    }

    [Command(requiresAuthority = false)]
    public void CmdChangePlayer(NetworkConnectionToClient sender = null)
    {
        currentPlayer = currentPlayer == player1 ? player2 : player1;
    }

    [Command(requiresAuthority = false)]
    public void CmdShowWinner()
    {
        MatchPlayerData mpd = matchPlayerData[currentPlayer];
        mpd.wins += 1;
        matchPlayerData[currentPlayer] = mpd;

        int playerIndex = matchPlayerData[currentPlayer].playerIndex;
        string message = "Gracz #" + playerIndex + " wygrał turę";
        CmdUpdateComments(message);

        RpcShowWinner(currentPlayer);

        currentPlayer = null;
    }

    [Command(requiresAuthority = false)]
    public void CmdUpdateComments(string text, NetworkConnectionToClient sender = null)
    {
        DialogueText = text;
    }

    #endregion

    #region RPC functions for game logic

    [ClientRpc]
    void RpcUpdateGameUI()
    {

        UpdateGameUI(null, currentPlayer);

    }

    [ClientRpc]
    void RpcDrawCard(NetworkIdentity owner)
    {
        if (owner.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            GameObject card = Instantiate(CardPrefab);
            MP_CardGUI script = card.GetComponent<MP_CardGUI>();

            CardData cardData = deckCards[Random.Range(0, deckCards.Count)];

            script.UpdateCardData(cardData);
            script.UpdateCardAppearance();
            CmdRemoveCardFromDeck(cardData);
            card.transform.SetParent(playerArea.transform, false);

            card.GetComponent<Button>().onClick.AddListener(() => StartCoroutine(PlayCardCoroutine(card, owner)));

        }
        else
        {
            GameObject back = Instantiate(BackCard);
            back.transform.SetParent(opponentArea.transform, false);

        }


    }

    IEnumerator DelayedDrawCard(NetworkIdentity player, float delay)
    {

        yield return new WaitForSeconds(delay);
        RpcDrawCard(player);

    }

    [ClientRpc]
    void RpcUpdateOpponentBoard(NetworkIdentity player)
    {
        if (!player.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            Destroy(opponentArea.transform.GetChild(0).gameObject);
        }

    }

    [ClientRpc]
    public void RpcShowWinner(NetworkIdentity winner)
    {
        if (winner.gameObject.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            gameText.text = "WYGRANA!!!";

        }
        else
        {
            gameText.text = "PRZEGRANA!";

        }

        exitButton.gameObject.SetActive(true);
        playAgainButton.gameObject.SetActive(true);
    }

    #endregion

    #region Client callback functions for game logic

    [ClientCallback]
    public IEnumerator PlayCardCoroutine(GameObject card, NetworkIdentity owner)
    {
        if (owner != currentPlayer)
            yield break;

        CardData cardData = card.GetComponent<MP_CardGUI>().cardData;

        if ((cardData.cardColor == CardOnDiscardPileData.cardColor) || (cardData.cardValue == CardOnDiscardPileData.cardValue) || (cardData.cardColor == CardColor.Wild))
        {
            AudioManager.Instance.PlaySfx("CardSound");

            string message = "Gracz #" + matchPlayerData[owner].playerIndex + " zagrał kartę " + TranslateCardData(cardData);
            CmdUpdateComments(message);
            CmdDiscardCard(cardData);

            Destroy(card);

            // Czekaj na zniszczenie obiektu i aktualizację komentarzy
            yield return null;

            int cardsLeft = playerArea.transform.childCount;

            if (cardsLeft == 0) // gracz zagrał ostatnią kartę
            {
                CmdShowWinner();
            }

            else if (cardData.cardValue == CardValue.SkipTurn) // zagrano kartę ominięcia kolejki
            {
                yield break; // Nic nie rób. Obecny gracz ma kolejną turę
            }
            else if (cardData.cardValue == CardValue.DrawTwo) // zagrano kartę "Weź dwie"
            {
                CmdOpponentDrawTwo();
                CmdChangePlayer();
            }
            else if (cardData.cardValue == CardValue.ChooseColor)
            {
                // Aktywacja tablicy wyboru koloru
                ShowHideColorPicker();

                foreach (KeyValuePair<Button, CardColor> entry in colorButtons)
                {
                    entry.Key.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        cardData.cardColor = entry.Value;
                        CmdUpdateDiscardCard(cardData);
                        ShowHideColorPicker();
                        string message = "Wybrano kolor " + TranslateCardColor(cardData.cardColor);
                        CmdUpdateComments(message);
                        CmdChangePlayer();

                        foreach (KeyValuePair<Button, CardColor> entry in colorButtons)
                        {
                            entry.Key.GetComponent<Button>().onClick.RemoveAllListeners();
                        }


                    });
                }

            }
            else if (cardData.cardValue == CardValue.DrawFourChangeColor)
            {
                CmdOpponentDrawFour();

                ShowHideColorPicker();

                foreach (KeyValuePair<Button, CardColor> entry in colorButtons)
                {
                    entry.Key.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        cardData.cardColor = entry.Value;
                        CmdUpdateDiscardCard(cardData);
                        ShowHideColorPicker();
                        string message = "Wybrano kolor " + TranslateCardColor(cardData.cardColor);
                        CmdUpdateComments(message);

                        CmdChangePlayer();

                        foreach (KeyValuePair<Button, CardColor> entry in colorButtons)
                        {
                            entry.Key.GetComponent<Button>().onClick.RemoveAllListeners();
                        }


                    });
                }


            }
            else // zagrano zwykłą kartę
            {
                CmdChangePlayer();
            }
        }

        yield return null;
    }

    [ClientCallback]
    void ShowHideColorPicker()
    {
        if (colorPicker.activeSelf)
            colorPicker.gameObject.SetActive(false);
        else
        {
            colorPicker.gameObject.SetActive(true);

        }
    }

    [ClientCallback]
    string TranslateCardColor(CardColor cardColor)
    {
        if (cardColor == CardColor.Yellow)
            return "żółty";

        if (cardColor == CardColor.Green)
            return "zielony";

        if (cardColor == CardColor.Red)
            return "czerwony";

        if (cardColor == CardColor.Blue)
            return "niebieski";

        return "";
    }

    [ClientCallback]
    string TranslateCardData(CardData cardData)
    {

        if (cardData.cardValue == CardValue.SkipTurn) // ominięcie kolejki
        {
            NetworkIdentity opponent = matchPlayerData.Keys.FirstOrDefault(networkIdentity => networkIdentity != currentPlayer);

            if (opponent == null)
                return "postoju";

            return "postoju\nGracz #" + matchPlayerData[opponent].playerIndex + " traci kolejkę";
        }

        if (cardData.cardValue == CardValue.DrawTwo)
        {
            NetworkIdentity opponent = matchPlayerData.Keys.FirstOrDefault(networkIdentity => networkIdentity != currentPlayer);

            if (opponent == null)
                return "\"Weź dwie\"";

            return "\"Weź dwie\"\nGracz #" + matchPlayerData[opponent].playerIndex + " bierze dwie karty ze stosu";
        }

        if (cardData.cardValue == CardValue.ChooseColor)
            return "zmiany koloru";

        if (cardData.cardValue == CardValue.DrawFourChangeColor)
        {
            NetworkIdentity opponent = matchPlayerData.Keys.FirstOrDefault(networkIdentity => networkIdentity != currentPlayer);

            if (opponent == null)
                return "\"Weź cztery\"";

            return "\"Weź cztery\"\nGracz #" + matchPlayerData[opponent].playerIndex + " bierze cztery karty ze stosu";
        }

        if (cardData.cardColor == CardColor.Yellow)
            return "żółtą z numerem " + cardData.cardValue.ToString("D");

        if (cardData.cardColor == CardColor.Green)
            return "zieloną z numerem " + cardData.cardValue.ToString("D");
        if (cardData.cardColor == CardColor.Red)
            return "czerwoną z numerem " + cardData.cardValue.ToString("D");
        if (cardData.cardColor == CardColor.Blue)
            return "niebieską z numerem " + cardData.cardValue.ToString("D");

        return "";
    }

    #endregion

    #region End of tour functions

    // Przypisano do przycisku Zagraj ponownie
    [ClientCallback]
    public void RequestPlayAgain()
    {
        AudioManager.Instance.PlaySfx("ButtonHoverSound");
        playAgainButton.gameObject.SetActive(false);
        CmdPlayAgain();
    }

    [Command(requiresAuthority = false)]
    public void CmdPlayAgain(NetworkConnectionToClient sender = null)
    {
        if (!playAgain)
            playAgain = true;
        else
        {
            playAgain = false;
            RestartGame();
        }
    }

    [ServerCallback]
    public void RestartGame()
    {

        NetworkIdentity[] keys = new NetworkIdentity[matchPlayerData.Keys.Count];
        matchPlayerData.Keys.CopyTo(keys, 0);

        RpcRestartGame();

        CardsDealt = false;

        deckCards.Clear();
        discardedCards.Clear();
        StartCoroutine(AddCardsToGameController());

        startingPlayer = startingPlayer == player1 ? player2 : player1;
        currentPlayer = startingPlayer;
    }

    [ClientRpc]
    public void RpcRestartGame()
    {
        foreach (Transform card in playerArea.transform)
        {
            Destroy(card.gameObject);
        }
        foreach (Transform card in opponentArea.transform)
        {
            Destroy(card.gameObject);
        }

        exitButton.gameObject.SetActive(false);
        playAgainButton.gameObject.SetActive(false);
        DialogueBox.text = "";

    }

    // Przypisano do przycisku Powrót do Lobby
    [Client]
    public void RequestExitGame()
    {
        AudioManager.Instance.PlaySfx("ButtonHoverSound");
        exitButton.gameObject.SetActive(false);
        playAgainButton.gameObject.SetActive(false);
        CmdRequestExitGame();
    }

    [Command(requiresAuthority = false)]
    public void CmdRequestExitGame(NetworkConnectionToClient sender = null)
    {
        StartCoroutine(ServerEndMatch(sender, false));
    }

    [ServerCallback]
    public void OnPlayerDisconnected(NetworkConnectionToClient conn)
    {
        // Sprawdź, czy rozłączający się klient jest graczem w tym meczu
        if (player1 == conn.identity || player2 == conn.identity)
            StartCoroutine(ServerEndMatch(conn, true));
    }

    [ServerCallback]
    public IEnumerator ServerEndMatch(NetworkConnectionToClient conn, bool disconnected)
    {
        RpcExitGame();

        canvasController.OnPlayerDisconnected -= OnPlayerDisconnected;

        // Poczekaj, aż ClientRpc wyprzedzi zniszczenie obiektu
        yield return new WaitForSeconds(0.1f);

        // Mirror usunie rozłączającego się klienta, więc musimy tylko posprzątać pozostałego klienta.
        // Jeśli obaj gracze wracają tylko do Lobby, musimy usunąć połączenia obu graczy
        
        if (!disconnected)
        {
            NetworkServer.RemovePlayerForConnection(player1.connectionToClient, true);
            MP_CanvasController.waitingConnections.Add(player1.connectionToClient);

            NetworkServer.RemovePlayerForConnection(player2.connectionToClient, true);
            MP_CanvasController.waitingConnections.Add(player2.connectionToClient);
        }
        else if (conn == player1.connectionToClient)
        {
            // gracz1 się rozłączył - prześlij gracza2 z powrotem do Lobby
            NetworkServer.RemovePlayerForConnection(player2.connectionToClient, true);
            MP_CanvasController.waitingConnections.Add(player2.connectionToClient);
        }
        else if (conn == player2.connectionToClient)
        {
            // gracz2 się rozłączył - prześlij gracza1 z powrotem do Lobby
            NetworkServer.RemovePlayerForConnection(player1.connectionToClient, true);
            MP_CanvasController.waitingConnections.Add(player1.connectionToClient);
        }

        // Przeskocz jedną klatkę, aby umożliwić zakończenie Usunięć.
        yield return null;

        // Wyślij najnowszą listę rozgrywek
        canvasController.SendMatchList();

        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    public void RpcExitGame()
    {
        canvasController.OnMatchEnded();
    }

    #endregion
}