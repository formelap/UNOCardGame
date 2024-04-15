using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("")]
public class MP_MatchNetworkManager : NetworkManager
{
    [Header("Match GUI")]
    public GameObject canvas;
    public MP_CanvasController canvasController;

    public static new MP_MatchNetworkManager singleton { get; private set; }

    // Dzia³a zarówno na serwerze, jak i na kliencie
    // Sieæ NIE jest zainicjowana, gdy to siê uruchamia
    public override void Awake()
    {
        //Debug.Log("Awake()");
        base.Awake();
        singleton = this;
        canvasController.InitializeData();
    }

    #region Server System Callbacks

    // Wywo³ywane na serwerze, gdy klient jest gotowy.
    // Domyœlna implementacja tej funkcji wywo³uje NetworkServer.SetClientReady(), aby kontynuowaæ proces konfiguracji sieci.
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);
        canvasController.OnServerReady(conn);
    }

    // Wywo³ywane na serwerze, gdy klient siê roz³¹cza.
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        StartCoroutine(DoServerDisconnect(conn));
    }

    IEnumerator DoServerDisconnect(NetworkConnectionToClient conn)
    {
        yield return canvasController.OnServerDisconnect(conn);
        base.OnServerDisconnect(conn);
    }

    #endregion

    #region Client System Callbacks

    // Wywo³ywane na kliencie po po³¹czeniu z serwerem
    // implementacja tej funkcji ustawia klienta jako gotowego i dodaje gracza
    public override void OnClientConnect()
    {
        //Debug.Log("OnClientConnect()");
        base.OnClientConnect();
        canvasController.OnClientConnect();
    }

    // Wywo³ywane na klientach, gdy zostan¹ roz³¹czone z serwerem.
    public override void OnClientDisconnect()
    {
        canvasController.OnClientDisconnect();
        base.OnClientDisconnect();
    }

    #endregion

    #region Start & Stop Callbacks

    // Wywo³ywane podczas uruchamiania serwera/hosta
    public override void OnStartServer()
    {
        if (mode == NetworkManagerMode.ServerOnly)
            canvas.SetActive(true);

        canvasController.OnStartServer();
    }

    // Wytwo³ywane przy uruchamianiu klienta
    public override void OnStartClient()
    {
        canvas.SetActive(true);
        canvasController.OnStartClient();
    }

    // Wywo³ywane przy zatrzymywaniu serwera/hosta
    public override void OnStopServer()
    {
        canvasController.OnStopServer();
        canvas.SetActive(false);
    }

    // Wywo³ywane przy zatrzymaniu klienta
    public override void OnStopClient()
    {
        canvasController.OnStopClient();
    }

    #endregion
}


