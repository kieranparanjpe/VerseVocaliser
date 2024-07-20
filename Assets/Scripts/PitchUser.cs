using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PitchUser : MonoBehaviour
{
    public AudioPitchEstimator estimator;

    public TextMeshProUGUI freqText;
    public TextMeshProUGUI noteText;
    public TextMeshProUGUI frText;

    [SerializeField] private int microphoneDeviceIndex = 1;

    public float tolPercent;
    // Start is called before the first frame update
    void Start()
    {
        MicrophoneListener.Instance.BeginMicrophoneStream(microphoneDeviceIndex, 1, 16000)
            .AttachStreamToAudioSource(GetComponent<AudioSource>());
        //estimator.InvokeUpdateNote(0.01f, GetComponent<AudioSource>());
    }

    // Update is called once per frame
    void Update()
    {
        noteText.text = estimator.CurrentNote.ToString();
        freqText.text = estimator.CurrentFrequency.ToString("F2");
        
        frText.text = "" + 1f / Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            
        }
    }
}
