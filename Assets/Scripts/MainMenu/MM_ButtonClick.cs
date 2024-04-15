using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MM_ButtonClick : MonoBehaviour
{
     public void OnButtonClick()
    {
        AudioManager.Instance.PlaySfx("ButtonHoverSound");        
    }

}
