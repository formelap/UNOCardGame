using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class MP_MatchGUI : MonoBehaviour
{
    Guid matchId;

    [Header("GUI Elements")]
    public Image image;
    public Toggle toggleButton;
    public Text matchName;
    public Text playerCount;

    [Header("Diagnostics")]
    public MP_CanvasController canvasController;

    public void Awake()
    {
        canvasController = FindObjectOfType<MP_CanvasController>();
        toggleButton.onValueChanged.AddListener(delegate { OnToggleClicked(); });
    }

    [ClientCallback]
    public void OnToggleClicked()
    {
        canvasController.SelectMatch(toggleButton.isOn ? matchId : Guid.Empty);
        image.color = toggleButton.isOn ? new Color(0f, 1f, 0f, 1.0f) : new Color(1f, 1f, 1f, 1.0f);
    }

    [ClientCallback]
    public Guid GetMatchId() => matchId;

    [ClientCallback]
    public void SetMatchInfo(MatchInfo infos)
    {
        matchId = infos.matchId;
        matchName.text = $"Gra #{infos.matchId.ToString().Substring(0, 8)}";
        playerCount.text = $"{infos.players} / {infos.maxPlayers}";
    }
}
