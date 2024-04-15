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

    // Dzia�a zar�wno na serwerze, jak i na kliencie
    // Sie� NIE jest zainicjowana, gdy to si� uruchamia
    public override void Awake()
    {
        //Debug.Log("Awake()");
        base.Awake();
        singleton = this;
        canvasController.InitializeData();
    }

    #region Server System Callbacks

    // Wywo�ywane na serwerze, gdy klient jest gotowy.
    // Domy�lna implementacja tej funkcji wywo�uje NetworkServer.SetClientReady(), aby kontynuowa� proces konfiguracji sieci.
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);
        canvasController.OnServerReady(conn);
    }

    // Wywo�ywane na serwerze, gdy klient si� roz��cza.
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

    // Wywo�ywane na kliencie po po��czeniu z serwerem
    // implementacja tej funkcji ustawia klienta jako gotowego i dodaje gracza
    public override void OnClientConnect()
    {
        //Debug.Log("OnClientConnect()");
        base.OnClientConnect();
        canvasController.OnClientConnect();
    }

    // Wywo�ywane na klientach, gdy zostan� roz��czone z serwerem.
    public override void OnClientDisconnect()
    {
        canvasController.OnClientDisconnect();
        base.OnClientDisconnect();
    }

    #endregion

    #region Start & Stop Callbacks

    // Wywo�ywane podczas uruchamiania serwera/hosta
    public override void OnStartServer()
    {
        if (mode == NetworkManagerMode.ServerOnly)
            canvas.SetActive(true);

        canvasController.OnStartServer();
    }

    // Wytwo�ywane przy uruchamianiu klienta
    public override void OnStartClient()
    {
        canvas.SetActive(true);
        canvasController.OnStartClient();
    }

    // Wywo�ywane przy zatrzymywaniu serwera/hosta
    public override void OnStopServer()
    {
        canvasController.OnStopServer();
        canvas.SetActive(false);
    }

    // Wywo�ywane przy zatrzymaniu klienta
    public override void OnStopClient()
    {
        canvasController.OnStopClient();
    }

    #endregion
}


