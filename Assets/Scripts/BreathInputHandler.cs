using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Goals of script:
 * Handle all aspects of Input. Will manage all different types of inputs and their scripts
 * 
 */

public class BreathInputHandler : MonoBehaviour
{
    [SerializeField] private BreathInference breathInference;
    [SerializeField] private PitchEstimator pitchEstimator;
    [SerializeField] private int microphoneDeviceIndex;
    [SerializeField] private int sampleRate;
    
    
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
