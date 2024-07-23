using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugTexts : MonoBehaviour
{
    public TextMeshProUGUI freqText;
    public TextMeshProUGUI noteText;
    public TextMeshProUGUI frText;

    public TextMeshProUGUI breathText;
    // Start is called before the first frame update

    // Update is called once per frame
    void Update()
    {
        noteText.text = PitchEstimator.CurrentNote.ToString();
        freqText.text = PitchEstimator.CurrentFrequency.ToString("F2");
        breathText.text = BreathInference.BreathClassification.ToString();
        
        frText.text = "" + 1f / Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            
        }
    }
}
