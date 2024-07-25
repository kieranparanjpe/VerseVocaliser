using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Goals of script:
 * Handle all aspects of Input. Will manage all different types of inputs and their scripts.
 *
 * Usage:
 * 1. Put 'BreathInputHandler' on any gameobject, and then add either/both PitchEstimator/BreathInference to the object depending on inputs you want to capture
 * 2. Setup all fields including microphoneDeviceIndex and sampleRate, as well as settings on individual scripts
 * 3. For BreathInference, ensure that the settings reflect the neural network model, and assign your chosen NN model from the assets folder.
 * Key settings for each model can be found below:
 * test_0_4096:
 * SAMPLE RATE: 16000, SAMPLES PER CHUNK: 4096, NFFT: 4096
 * test_0_8000:
 * SAMPLE RATE: 16000, SAMPLES PER CHUNK: 8000, NFFT: 2048
 * 4. For PitchEstimator, tinker with tolerance percent (0<t<1). This determines on a relative scale how close you must be to the note in order for it to register.
 *
 * Get Input from other scripts:
 * Use BreathInference.BreathClassification and BreathInference.BreathClassificationCertainty
 * and
 * Use PitchEstimator.CurrentNote and PitchEstimator.CurrentFrequency
 *
 * To change the microphone device, use:
 * MicrophoneListener.Instance.BeginMicrophoneStream(microphoneDeviceIndex, clipLength, sampleRate); -> if there is already a stream, it will replace the old one
 * You can also change the AudioSource with:
 * MicrophoneListener.Instance.AttachStreamToAudioSource(audioSource); -> if already an audio source, removes old one
 * and
 * MicrophoneListener.Instance.DetachStreamFromAudioSource();
 * 
 * 
 */

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
