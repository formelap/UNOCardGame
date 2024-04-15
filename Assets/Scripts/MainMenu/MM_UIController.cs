using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public Slider _musicSlider, _sfxSlider;

    public void ToggleMusic()
    {
        AudioManager.Instance.ToggleMusic();
    }

    public void ToggleSFX()
    {
        AudioManager.Instance.ToggleSfx();
    }

    public void MusicVolume()
    {
        AudioManager.Instance.MusicVolume(_musicSlider.value);
    }

    public void SfxVolume()
    {
        AudioManager.Instance.SfxVolume(_sfxSlider.value);
    }

    public void setSliderMusicVolume()
    {
        _musicSlider.value = AudioManager.Instance.getMusicVolume();
    }

    public void setSliderSfxVolume()
    {
        _sfxSlider.value = AudioManager.Instance.getSfxVolume();
    }
}
