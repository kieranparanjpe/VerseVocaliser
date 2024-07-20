using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Made By Kieran Paranjpe for The Verse, July 2024

public class MicrophoneListener
{
    private AudioSource Source = null;
    private int microphoneDeviceIndex = 0;
    public AudioClip microphoneStream { get; private set; } = null;

    private static readonly MicrophoneListener _instance = new MicrophoneListener();

    private MicrophoneListener() { }

    public static MicrophoneListener Instance => _instance;
    
    /// <summary>
    /// Begin updating an AudioClip with the data from microphone. Does not attach to an AudioSource.
    /// </summary>
    /// <param name="device">Microphone Device to use.</param>
    /// <returns>Itself</returns>
    public MicrophoneListener BeginMicrophoneStream(int device, int clipLength, int sampleRate)
    {
        microphoneDeviceIndex = device;
        if (microphoneDeviceIndex >= Microphone.devices.Length || microphoneDeviceIndex < 0)
            Debug.LogError($"Microphone Device Index of {microphoneDeviceIndex} is not a valid microphone index.");
        
        string microphoneName = Microphone.devices[microphoneDeviceIndex];
        if (microphoneStream != null)
        {
            EndMicrophoneStream();
        }
        
        microphoneStream = Microphone.Start(microphoneName, true, clipLength, sampleRate);
        return this;
    }

    /// <summary>
    /// End updating an AudioClip with the data from microphone. Does not detach from AudioSource.
    /// </summary>
    public void EndMicrophoneStream()
    {
        if (microphoneDeviceIndex >= Microphone.devices.Length || microphoneDeviceIndex < 0)
            Debug.LogError($"Microphone Device Index of {microphoneDeviceIndex} is not a valid microphone index.");
        string microphoneName = Microphone.devices[microphoneDeviceIndex];
        Microphone.End(microphoneName);
        microphoneStream = null;
    }
    
    /// <summary>
    /// Prints out the microphone devices available.
    /// </summary>
    public void ShowMicrophoneDevices()
    {
        Debug.Log("Microphone Options: " + string.Join(',', Microphone.devices));
    }
    
    /// <summary>
    /// Attach the microphone stream to audio source.
    /// </summary>
    /// <param name="source">Audio Source to attach to</param>>
    /// <returns>Itself</returns>
    public MicrophoneListener AttachStreamToAudioSource(AudioSource source)
    {
        Source = source;
        if (Source == null)
        {
            Debug.LogError("Could not find an Audio Source to attach to!");
        }
        if (microphoneStream == null)
            Debug.LogError("Microphone Stream is null");
        
        Source.clip = microphoneStream;
        Source.Play();
        Source.loop = true;

        return this;
    }

    /// <summary>
    /// Detach current microphone stream from audio source.
    /// </summary>
    public void DetachStreamFromAudioSource()
    {
        if (Source == null)
        {
            Debug.LogError("Could not find an Audio Source to detach from!");
        }
        Source.clip = null;
        Source.loop = false;
        Source.Stop();
    }
}
