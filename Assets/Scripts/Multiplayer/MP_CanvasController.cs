using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MP_CanvasController : MonoBehaviour
{
    // Kontrolery meczów nas³uchuj¹ tego, aby zakoñczyæ swój mecz i posprz¹daæ
    public event Action<NetworkConnectionToClient> OnPlayerDisconnected;

    // Odniesienie do klienta, który utworzy³ odpowiadaj¹cy mecz z poni¿szych openMatches
    internal static readonly Dictionary<NetworkConnectionToClient, Guid> playerMatches = new Dictionary<NetworkConnectionToClient, Guid>();

    // Otwarte mecze dostêpne do do³¹czenia
    internal static readonly Dictionary<Guid, MatchInfo> openMatches = new Dictionary<Guid, MatchInfo>();

    // Po³¹czenia sieciowe wszystkich graczy w meczu
    internal static readonly Dictionary<Guid, HashSet<NetworkConnectionToClient>> matchConnections = new Dictionary<Guid, HashSet<NetworkConnectionToClient>>();

    // Informacje o graczu wed³ug po³¹czenia sieciowego
    internal static readonly Dictionary<NetworkConnection, PlayerInfo> playerInfos = new Dictionary<NetworkConnection, PlayerInfo>();

    // Po³¹czenia sieciowe, które jeszcze nie rozpoczê³y ani nie do³¹czy³y do meczu
    internal static readonly List<NetworkConnectionToClient> waitingConnections = new List<NetworkConnectionToClient>();

    // GUID meczu utworzonego przez lokalnego gracza
    internal Guid localPlayerMatch = Guid.Empty;

    // GUID meczu, do którego lokalny gracz do³¹czy³
    internal Guid localJoinedMatch = Guid.Empty;

    // GUID meczu, który lokalny gracz wybra³ z listy meczów
    internal Guid selectedMatch = Guid.Empty;

    // U¿ywane w UI dla "Gracz #"
    int playerIndex = 1;

    [Header("GUI References")]
    public GameObject matchList;
    public GameObject matchPrefab;
    public GameObject gameControllerPrefab;
    public Button createButton;
    public Button joinButton;
    public GameObject lobbyView;
    public GameObject roomView;
    public MP_RoomGUI roomGUI;
    public ToggleGroup toggleGroup;

    // RuntimeInitializeOnLoadMethod -> szybki tryb gry bez prze³adowania domeny
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStatics()
    {
        playerMatches.Clear();
        openMatches.Clear();
        matchConnections.Clear();
        playerInfos.Clear();
        waitingConnections.Clear();
    }

    #region UI Functions

    // Wywo³ywane z kilku miejsc, aby zapewniæ czysty reset
    //  - MatchNetworkManager.Awake
    //  - OnStartServer
    //  - OnStartClient
    //  - OnClientDisconnect
    //  - ResetCanvas
    internal void InitializeData()
    {
        playerMatches.Clear();
        openMatches.Clear();
        matchConnections.Clear();
        waitingConnections.Clear();
        localPlayerMatch = Guid.Empty;
        localJoinedMatch = Guid.Empty;
    }

    // Wywo³ywane z OnStopServer i OnStopClient podczas zamykania
    void ResetCanvas()
    {
        InitializeData();
        lobbyView.SetActive(false);
        roomView.SetActive(false);
        gameObject.SetActive(false);
    }

    #endregion

    #region Button Calls

    // Wywo³ywane z "MP_MatchGUI.OnToggleClicked"
    [ClientCallback]
    public void SelectMatch(Guid matchId)
    {
        if (matchId == Guid.Empty)
        {
            selectedMatch = Guid.Empty;
            joinButton.interactable = false;
        }
        else
        {
            if (!openMatches.Keys.Contains(matchId))
            {
                joinButton.interactable = false;
                return;
            }

            selectedMatch = matchId;
            MatchInfo infos = openMatches[matchId];
            joinButton.interactable = infos.players < infos.maxPlayers;
        }
    }

    // Przypisane w inspektorze do przycisku Utwórz
    [ClientCallback]
    public void RequestCreateMatch()
    {
        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Create });
    }

    // Przypisane w inspektorze do przycisku Do³¹cz
    [ClientCallback]
    public void RequestJoinMatch()
    {
        if (selectedMatch == Guid.Empty) return;

        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Join, matchId = selectedMatch });
    }

    // Przypisane w inspektorze do przycisku Opuœæ
    [ClientCallback]
    public void RequestLeaveMatch()
    {
        if (localJoinedMatch == Guid.Empty) return;

        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Leave, matchId = localJoinedMatch });
    }

    // Przypisane w inspektorze do przycisku Anuluj
    [ClientCallback]
    public void RequestCancelMatch()
    {
        if (localPlayerMatch == Guid.Empty) return;

        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Cancel });
    }

    // Przypisane w inspektorze do przycisku Gotowy
    [ClientCallback]
    public void RequestReadyChange()
    {
        if (localPlayerMatch == Guid.Empty && localJoinedMatch == Guid.Empty) return;

        Guid matchId = localPlayerMatch == Guid.Empty ? localJoinedMatch : localPlayerMatch;

        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Ready, matchId = matchId });
    }

    // Przypisane w inspektorze do przycisku Start
    [ClientCallback]
    public void RequestStartMatch()
    {
        if (localPlayerMatch == Guid.Empty) return;

        NetworkClient.Send(new ServerMatchMessage { serverMatchOperation = ServerMatchOperation.Start });
    }

    // Wywo³ywane z MP_GameController.RpcExitGame
    [ClientCallback]
    public void OnMatchEnded()
    {
        localPlayerMatch = Guid.Empty;
        localJoinedMatch = Guid.Empty;
        ShowLobbyView();
    }

    #endregion

    #region Server & Client Callbacks

    // Metody w tej sekcji s¹ wywo³ywane z odpowiadaj¹cych im metod MatchNetworkManagera
    [ServerCallback]
    internal void OnStartServer()
    {
        InitializeData();
        NetworkServer.RegisterHandler<ServerMatchMessage>(OnServerMatchMessage);
    }

    [ServerCallback]
    internal void OnServerReady(NetworkConnectionToClient conn)
    {
        waitingConnections.Add(conn);
        playerInfos.Add(conn, new PlayerInfo { playerIndex = this.playerIndex, ready = false });
        playerIndex++;

        SendMatchList();
    }

    [ServerCallback]
    internal IEnumerator OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // Wywo³aj OnPlayerDisconnected na wszystkich instancjach GameController.
        OnPlayerDisconnected?.Invoke(conn);

        Guid matchId;
        if (playerMatches.TryGetValue(conn, out matchId))
        {
            playerMatches.Remove(conn);
            openMatches.Remove(matchId);

            foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            {
                PlayerInfo _playerInfo = playerInfos[playerConn];
                _playerInfo.ready = false;
                _playerInfo.matchId = Guid.Empty;
                playerInfos[playerConn] = _playerInfo;
                playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Departed });
            }
        }

        foreach (KeyValuePair<Guid, HashSet<NetworkConnectionToClient>> kvp in matchConnections)
            kvp.Value.Remove(conn);

        PlayerInfo playerInfo = playerInfos[conn];
        if (playerInfo.matchId != Guid.Empty)
        {
            MatchInfo matchInfo;
            if (openMatches.TryGetValue(playerInfo.matchId, out matchInfo))
            {
                matchInfo.players--;
                openMatches[playerInfo.matchId] = matchInfo;
            }

            HashSet<NetworkConnectionToClient> connections;
            if (matchConnections.TryGetValue(playerInfo.matchId, out connections))
            {
                PlayerInfo[] infos = connections.Select(playerConn => playerInfos[playerConn]).ToArray();

                foreach (NetworkConnectionToClient playerConn in matchConnections[playerInfo.matchId])
                    if (playerConn != conn)
                        playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.UpdateRoom, playerInfos = infos });
            }
        }

        SendMatchList();

        yield return null;
    }

    [ServerCallback]
    internal void OnStopServer()
    {
        ResetCanvas();
    }

    [ClientCallback]
    internal void OnClientConnect()
    {
        playerInfos.Add(NetworkClient.connection, new PlayerInfo { playerIndex = this.playerIndex, ready = false });
    }

    [ClientCallback]
    internal void OnStartClient()
    {
        InitializeData();
        ShowLobbyView();
        createButton.gameObject.SetActive(true);
        joinButton.gameObject.SetActive(true);
        NetworkClient.RegisterHandler<ClientMatchMessage>(OnClientMatchMessage);
    }

    [ClientCallback]
    internal void OnClientDisconnect()
    {
        InitializeData();
    }

    [ClientCallback]
    internal void OnStopClient()
    {
        ResetCanvas();
    }

    #endregion

    #region Server Match Message Handlers

    [ServerCallback]
    void OnServerMatchMessage(NetworkConnectionToClient conn, ServerMatchMessage msg)
    {
        
        switch (msg.serverMatchOperation)
        {
            case ServerMatchOperation.None:
                {
                    Debug.LogWarning("Missing ServerMatchOperation");
                    break;
                }
            case ServerMatchOperation.Create:
                {
                    OnServerCreateMatch(conn);
                    break;
                }
            case ServerMatchOperation.Cancel:
                {
                    OnServerCancelMatch(conn);
                    break;
                }
            case ServerMatchOperation.Start:
                {
                    OnServerStartMatch(conn);
                    break;
                }
            case ServerMatchOperation.Join:
                {
                    OnServerJoinMatch(conn, msg.matchId);
                    break;
                }
            case ServerMatchOperation.Leave:
                {
                    OnServerLeaveMatch(conn, msg.matchId);
                    break;
                }
            case ServerMatchOperation.Ready:
                {
                    OnServerPlayerReady(conn, msg.matchId);
                    break;
                }
        }
    }

    [ServerCallback]
    void OnServerPlayerReady(NetworkConnectionToClient conn, Guid matchId)
    {
        PlayerInfo playerInfo = playerInfos[conn];
        playerInfo.ready = !playerInfo.ready;
        playerInfos[conn] = playerInfo;

        HashSet<NetworkConnectionToClient> connections = matchConnections[matchId];
        PlayerInfo[] infos = connections.Select(playerConn => playerInfos[playerConn]).ToArray();

        foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.UpdateRoom, playerInfos = infos });
    }

    [ServerCallback]
    void OnServerLeaveMatch(NetworkConnectionToClient conn, Guid matchId)
    {
        MatchInfo matchInfo = openMatches[matchId];
        matchInfo.players--;
        openMatches[matchId] = matchInfo;

        PlayerInfo playerInfo = playerInfos[conn];
        playerInfo.ready = false;
        playerInfo.matchId = Guid.Empty;
        playerInfos[conn] = playerInfo;

        foreach (KeyValuePair<Guid, HashSet<NetworkConnectionToClient>> kvp in matchConnections)
            kvp.Value.Remove(conn);

        HashSet<NetworkConnectionToClient> connections = matchConnections[matchId];
        PlayerInfo[] infos = connections.Select(playerConn => playerInfos[playerConn]).ToArray();

        foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.UpdateRoom, playerInfos = infos });

        SendMatchList();

        conn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Departed });
    }

    [ServerCallback]
    void OnServerCreateMatch(NetworkConnectionToClient conn)
    {
        if (playerMatches.ContainsKey(conn)) return;

        Guid newMatchId = Guid.NewGuid();
        matchConnections.Add(newMatchId, new HashSet<NetworkConnectionToClient>());
        matchConnections[newMatchId].Add(conn);
        playerMatches.Add(conn, newMatchId);
        openMatches.Add(newMatchId, new MatchInfo { matchId = newMatchId, maxPlayers = 2, players = 1 });

        PlayerInfo playerInfo = playerInfos[conn];
        playerInfo.ready = false;
        playerInfo.matchId = newMatchId;
        playerInfos[conn] = playerInfo;

        PlayerInfo[] infos = matchConnections[newMatchId].Select(playerConn => playerInfos[playerConn]).ToArray();

        conn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Created, matchId = newMatchId, playerInfos = infos });

        SendMatchList();
    }

    [ServerCallback]
    void OnServerCancelMatch(NetworkConnectionToClient conn)
    {
        if (!playerMatches.ContainsKey(conn)) return;

        conn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Cancelled });

        Guid matchId;
        if (playerMatches.TryGetValue(conn, out matchId))
        {
            playerMatches.Remove(conn);
            openMatches.Remove(matchId);

            foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            {
                PlayerInfo playerInfo = playerInfos[playerConn];
                playerInfo.ready = false;
                playerInfo.matchId = Guid.Empty;
                playerInfos[playerConn] = playerInfo;
                playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Departed });
            }

            SendMatchList();
        }
    }

    [ServerCallback]
    void OnServerStartMatch(NetworkConnectionToClient conn)
    {
        if (!playerMatches.ContainsKey(conn)) return;

        Guid matchId;
        if (playerMatches.TryGetValue(conn, out matchId))
        {
            GameObject gameControllerObject = Instantiate(gameControllerPrefab);
            gameControllerObject.GetComponent<NetworkMatch>().matchId = matchId;
            NetworkServer.Spawn(gameControllerObject);

            MP_GameController gameController = gameControllerObject.GetComponent<MP_GameController>();

            foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            {
                playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Started });

                GameObject player = Instantiate(NetworkManager.singleton.playerPrefab);
                player.GetComponent<NetworkMatch>().matchId = matchId;
                NetworkServer.AddPlayerForConnection(playerConn, player);

                if (gameController.player1 == null)
                    gameController.player1 = playerConn.identity;
                else
                    gameController.player2 = playerConn.identity;

                /* Reset ready state for after the match. */
                PlayerInfo playerInfo = playerInfos[playerConn];
                playerInfo.ready = false;
                playerInfos[playerConn] = playerInfo;
            }

            gameController.startingPlayer = gameController.player1;
            gameController.currentPlayer = gameController.player1;

            playerMatches.Remove(conn);
            openMatches.Remove(matchId);
            matchConnections.Remove(matchId);
            SendMatchList();

            OnPlayerDisconnected += gameController.OnPlayerDisconnected;
        }
    }

    [ServerCallback]
    void OnServerJoinMatch(NetworkConnectionToClient conn, Guid matchId)
    {
        if (!matchConnections.ContainsKey(matchId) || !openMatches.ContainsKey(matchId)) return;

        MatchInfo matchInfo = openMatches[matchId];
        matchInfo.players++;
        openMatches[matchId] = matchInfo;
        matchConnections[matchId].Add(conn);

        PlayerInfo playerInfo = playerInfos[conn];
        playerInfo.ready = false;
        playerInfo.matchId = matchId;
        playerInfos[conn] = playerInfo;

        PlayerInfo[] infos = matchConnections[matchId].Select(playerConn => playerInfos[playerConn]).ToArray();
        SendMatchList();

        conn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.Joined, matchId = matchId, playerInfos = infos });

        foreach (NetworkConnectionToClient playerConn in matchConnections[matchId])
            playerConn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.UpdateRoom, playerInfos = infos });
    }

    // Wysy³a zaktualizowan¹ listê rozgrywek do wszystkich oczekuj¹cych po³¹czeñ lub tylko do jednego, jeœli jest okreœlone.

    internal void SendMatchList(NetworkConnectionToClient conn = null)
    {
        if (conn != null)
            conn.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.List, matchInfos = openMatches.Values.ToArray() });
        else
            foreach (NetworkConnectionToClient waiter in waitingConnections)
                waiter.Send(new ClientMatchMessage { clientMatchOperation = ClientMatchOperation.List, matchInfos = openMatches.Values.ToArray() });
    }

    #endregion

    #region Client Match Message Handler

    [ClientCallback]
    void OnClientMatchMessage(ClientMatchMessage msg)
    {
        switch (msg.clientMatchOperation)
        {
            case ClientMatchOperation.None:
                {
                    Debug.LogWarning("Missing ClientMatchOperation");
                    break;
                }
            case ClientMatchOperation.List:
                {
                    openMatches.Clear();
                    foreach (MatchInfo matchInfo in msg.matchInfos)
                        openMatches.Add(matchInfo.matchId, matchInfo);

                    RefreshMatchList();
                    break;
                }
            case ClientMatchOperation.Created:
                {
                    localPlayerMatch = msg.matchId;
                    ShowRoomView();
                    roomGUI.RefreshRoomPlayers(msg.playerInfos);
                    roomGUI.SetOwner(true);
                    break;
                }
            case ClientMatchOperation.Cancelled:
                {
                    localPlayerMatch = Guid.Empty;
                    ShowLobbyView();
                    break;
                }
            case ClientMatchOperation.Joined:
                {
                    localJoinedMatch = msg.matchId;
                    ShowRoomView();
                    roomGUI.RefreshRoomPlayers(msg.playerInfos);
                    roomGUI.SetOwner(false);
                    break;
                }
            case ClientMatchOperation.Departed:
                {
                    localJoinedMatch = Guid.Empty;
                    ShowLobbyView();
                    break;
                }
            case ClientMatchOperation.UpdateRoom:
                {
                    roomGUI.RefreshRoomPlayers(msg.playerInfos);
                    break;
                }
            case ClientMatchOperation.Started:
                {
                    lobbyView.SetActive(false);
                    roomView.SetActive(false);
                    break;
                }
        }
    }

    [ClientCallback]
    void ShowLobbyView()
    {
        lobbyView.SetActive(true);
        roomView.SetActive(false);

        foreach (Transform child in matchList.transform)
            if (child.gameObject.GetComponent<MP_MatchGUI>().GetMatchId() == selectedMatch)
            {
                Toggle toggle = child.gameObject.GetComponent<Toggle>();
                toggle.isOn = true;
            }
    }

    [ClientCallback]
    void ShowRoomView()
    {
        lobbyView.SetActive(false);
        roomView.SetActive(true);
    }

    [ClientCallback]
    void RefreshMatchList()
    {
        foreach (Transform child in matchList.transform)
            Destroy(child.gameObject);

        joinButton.interactable = false;

        foreach (MatchInfo matchInfo in openMatches.Values)
        {
            GameObject newMatch = Instantiate(matchPrefab, Vector3.zero, Quaternion.identity);
            newMatch.transform.SetParent(matchList.transform, false);
            newMatch.GetComponent<MP_MatchGUI>().SetMatchInfo(matchInfo);

            Toggle toggle = newMatch.GetComponent<Toggle>();
            toggle.group = toggleGroup;
            if (matchInfo.matchId == selectedMatch)
                toggle.isOn = true;
        }
    }

    #endregion
}
