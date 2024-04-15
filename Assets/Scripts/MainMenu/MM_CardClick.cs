using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MM_CardClick : MonoBehaviour
{
    public void OnCardClick()
    {
        AudioManager.Instance.PlaySfx("CardSound");        
    }
}