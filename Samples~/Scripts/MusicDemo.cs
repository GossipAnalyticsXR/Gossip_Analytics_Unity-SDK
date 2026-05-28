using UnityEngine;
using UnityEngine.InputSystem;

public class MusicDemo : MonoBehaviour
{
    public AudioSource source;

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (source.mute) source.mute = false;
            else source.mute = true;
        }
    }
}
