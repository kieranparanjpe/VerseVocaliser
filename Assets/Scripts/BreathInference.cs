using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine;
using System.IO;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

//somewhere this script has a lot of delay ~0.5 seconds from when you make a sound and it computes spectrogram. More and more i think AudioTransformations needs to be done in C.

/// <summary>
/// This class is used for getting the current breath state using Microphone Input and an ML model in ONNX form
/// To get the breath state from another class, access static variables BreathClassification and BreathClassificationCertainty
/// </summary>
public class BreathInference : MonoBehaviour
{
    // .onnx model
    [SerializeField] private NNModel audioAsset;
    private Model audioRuntimeModel;
    private IWorker audioRuntimeWorker;
    
    // if you want to see the mel spectrogram as it enters the model
    [SerializeField] private RawImage debugSpectrogramImage;
    
    // data should be the same as what was used for training the model. sample rate will be changed by BreathInputHandler
    [SerializeField] int samplesPerUpdate = 2000;        
    [SerializeField] private int samplesPerChunk = 8000;
    [SerializeField] private int nFFT = 2048;
    
    private int sampleOffset = 0;
    private int samplesPerClip = 16000 * 10;
    private int sampleRate = 16000;
    
    private Queue<float> audioData = new Queue<float>();
    // What the model thinks the audio is
    public static BreathClassMapping BreathClassification { get; private set; } = BreathClassMapping.SILENCE;
    //Certainty between 0 and 1f of inference
    public static float BreathClassificationCertainty { get; private set; } = 0f;

    private AudioTransformations audioTransformations;
    
    // init
    public void Initialise(int microphoneDeviceIndex, int clipLength, int sampleRate)
    {
        this.sampleRate = sampleRate;
        samplesPerClip = sampleRate * clipLength;
        // if no mic stream, start one
        if (MicrophoneListener.Instance.microphoneStream == null)
            MicrophoneListener.Instance.BeginMicrophoneStream(microphoneDeviceIndex, clipLength, sampleRate);
        
        // load model
        audioRuntimeModel = ModelLoader.Load(audioAsset);
        audioRuntimeWorker = WorkerFactory.CreateWorker(WorkerFactory.Type.PixelShader, audioRuntimeModel);

        audioTransformations = new AudioTransformations(samplesPerChunk, 2048, 2048, 128, 16000, 64);
        
        InvokeRepeating(nameof(Infer), 0, samplesPerUpdate / (float)this.sampleRate);
    }

    private void Infer()
    {
        if (MicrophoneListener.Instance.microphoneStream == null)
            Debug.LogError("Microphone stream is null");
        
        Profiler.BeginSample("infer");
        float[] data = new float[samplesPerUpdate];
        MicrophoneListener.Instance.microphoneStream.GetData(data, sampleOffset % samplesPerClip);
        sampleOffset += samplesPerUpdate;
        
        foreach (float d in data)
        {
            audioData.Enqueue(d);
        }

        while (audioData.Count > samplesPerChunk)
            audioData.Dequeue();

        if (audioData.Count != samplesPerChunk)
            return;
        
        Profiler.BeginSample("Spectrogram");
        MathNet.Numerics.LinearAlgebra.Matrix<float> melSpectrogram = audioTransformations.MelSpectrogram(audioData.ToArray());
        Profiler.EndSample();
        
        if (debugSpectrogramImage != null) 
            DrawImage(melSpectrogram);
        
        Profiler.BeginSample("create tensor");
        Tensor input = new Tensor(1, melSpectrogram.RowCount, melSpectrogram.ColumnCount, 1);
        for (int r = 0; r < melSpectrogram.RowCount; r++)
        {
            for (int c = 0; c < melSpectrogram.ColumnCount; c++)
            {
                input[0, r, c, 0] = melSpectrogram[r, c];
            }
        }
        Profiler.EndSample();

        audioRuntimeWorker.Execute(input);
        Tensor output = audioRuntimeWorker.PeekOutput();

        int prediction = output.ArgMax()[0];
        BreathClassification = (BreathClassMapping)prediction;
        BreathClassificationCertainty = output[0, 0, 0, prediction];
        output.Dispose();
        input.Dispose();
        Profiler.EndSample();
    }

    private void DrawImage(MathNet.Numerics.LinearAlgebra.Matrix<float> img)
    {
        Texture2D tex = new Texture2D(img.ColumnCount, img.RowCount);
        float max = img.ToRowMajorArray().Max();
        print(max);
        // Apply the color array to the texture
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                float c = img[y, x] / 1;
                tex.SetPixel(x, y, new Color(c, c, c));
            }
        }

        // Apply the changes
        tex.Apply();

        // Assign the texture to the RawImage
        debugSpectrogramImage.texture = tex;
    }

    public enum BreathClassMapping
    {
        INHALE,
        EXHALE,
        SILENCE,
        SPEECH,
        KEYBOARD
    }
}

