using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Goals of script: <br></br>
/// Handle all aspects of Input. Will manage all different types of inputs and their scripts.<br></br>
/// Usage:<br></br>
/// 1. Put 'BreathInputHandler' on any gameobject, and then add either/both PitchEstimator/BreathInference to the object depending on inputs you want to capture<br></br>
/// 2. Setup all fields including microphoneDeviceIndex and sampleRate, as well as settings on individual scripts<br></br>
/// 3. For BreathInference, ensure that the settings reflect the neural network model, and assign your chosen NN model from the assets folder.<br></br>
/// Key settings for each model can be found below:<br></br>
/// test_0_4096:<br></br>
/// SAMPLE RATE: 16000, SAMPLES PER CHUNK: 4096, NFFT: 4096<br></br>
/// test_0_8000:<br></br>
/// SAMPLE RATE: 16000, SAMPLES PER CHUNK: 8000, NFFT: 2048<br></br>
/// 4. For PitchEstimator, tinker with tolerance percent (0 to 1). This determines on a relative scale how close you must be to the note in order for it to register.<br></br>
///<br></br>
/// Get Input from other scripts:<br></br>
/// Use BreathInference.BreathClassification and BreathInference.BreathClassificationCertainty<br></br>
/// and<br></br>
/// Use PitchEstimator.CurrentNote and PitchEstimator.CurrentFrequency<br></br>
///<br></br>
/// To change the microphone device, use:<br></br>
/// MicrophoneListener.Instance.BeginMicrophoneStream(microphoneDeviceIndex, clipLength, sampleRate); -> if there is already a stream, it will replace the old one<br></br>
/// You can also change the AudioSource with:<br></br>
/// MicrophoneListener.Instance.AttachStreamToAudioSource(audioSource); -> if already an audio source, removes old one<br></br>
/// and<br></br>
/// MicrophoneListener.Instance.DetachStreamFromAudioSource();<br></br>
/// <br></br>
/// Import Scripts are: BreathInputHandler, MicrophoneListener, BreathInference, PitchEstimator and AudioTransformations<br></br>
///  -> AudioTransformations relies on plugins in the Plugins folder.<br></br>
/// </summary>
public class BreathInputHandler : MonoBehaviour
{
    [SerializeField] private BreathInference breathInference;
    [SerializeField] private PitchEstimator pitchEstimator;
    [SerializeField] private int microphoneDeviceIndex;
    [SerializeField] private int sampleRate = 16000;
    
    
    void Start()
    {
        if (breathInference != null)
        {
            breathInference.Initialise(microphoneDeviceIndex, 10, sampleRate);
        }

        if (pitchEstimator != null)
        {
            pitchEstimator.Initialise(microphoneDeviceIndex, 10, sampleRate);
        }
    }
}
