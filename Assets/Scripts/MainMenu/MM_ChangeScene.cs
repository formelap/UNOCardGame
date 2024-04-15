using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        // Check if the instance is a host (both server and client)
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            // Stop the host
            NetworkManager.singleton.StopHost();
        }
        // Check if the instance is only a server
        else if (NetworkServer.active)
        {
            // Stop the server
            NetworkManager.singleton.StopServer();
        }
        // Check if the instance is only a client
        else if (NetworkClient.isConnected)
        {
            // Disconnect the client
            NetworkClient.Disconnect();
        }

        SceneManager.LoadScene(sceneName);
    }
}
