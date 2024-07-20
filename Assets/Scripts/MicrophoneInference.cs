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


public class MicrophoneInference : MonoBehaviour
{
    public NNModel modelAsset;
    private Model m_RuntimeModel;
    private IWorker m_Worker;
    public RawImage rawImage;

    [SerializeField] private int microphoneDeviceIndex = 1;

    private int sampleOffset = 0;
    private int samplesPerClip = 16000 * 10;
    private int samplesPerUpdate = 2000;
    private int samplesPerChunk = 8000;
    private int sampleRate = 16000;
    
    public TextMeshProUGUI inferenceText;

    private Queue<float> audio_data = new Queue<float>(); 

    private string[] classMapping = new string[] { "inhale", "exhale", "other", "speech", "keyboard" };

    private AudioTransformations audioTransformations;
    
    void Start()
    {
        MicrophoneListener.Instance.BeginMicrophoneStream(microphoneDeviceIndex, 10, 16000);
        
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        m_Worker = WorkerFactory.CreateWorker(WorkerFactory.Type.PixelShader, m_RuntimeModel);

        audioTransformations = new AudioTransformations(samplesPerChunk, 2048, 2048, 128, 16000, 64);
        
        InvokeRepeating(nameof(Infer), 0, samplesPerUpdate / (float)sampleRate);
    }

    void Infer()
    {
        Profiler.BeginSample("infer");
        float[] data = new float[samplesPerUpdate];
        MicrophoneListener.Instance.microphoneStream.GetData(data, sampleOffset % samplesPerClip);
        sampleOffset += samplesPerUpdate;
        
        foreach (float d in data)
        {
            audio_data.Enqueue(d);
        }

        while (audio_data.Count > samplesPerChunk)
            audio_data.Dequeue();

        if (audio_data.Count != samplesPerChunk)
            return;
        
        //Profiler.BeginSample("old spec");
        //MathNet.Numerics.LinearAlgebra.Matrix<float> melSpectrogram1 = AudioTransformations.MelSpectrogram(audio_data.ToArray(), 2048, 2048, 128, 16000, 64);
        //Profiler.EndSample();
        
        Profiler.BeginSample("new spec");
        MathNet.Numerics.LinearAlgebra.Matrix<float> melSpectrogram = audioTransformations.MelSpectrogram(audio_data.ToArray());
        Profiler.EndSample();
        
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

        m_Worker.Execute(input);
        Tensor output = m_Worker.PeekOutput();

        //Debug.Log(output.Flatten() + " " + string.Join(" ", output.ArgMax()));
        inferenceText.text = classMapping[output.ArgMax()[0]];
        input.Dispose();
        Profiler.EndSample();
    }

    private void DrawImage(MathNet.Numerics.LinearAlgebra.Matrix<float> img)
    {
        Texture2D tex = new Texture2D(img.ColumnCount, img.RowCount);
        float max = img.ToRowMajorArray().Max();

        // Apply the color array to the texture
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                float c = img[y, x] / max;
                tex.SetPixel(x, y, new Color(c, c, c));
            }
        }

        // Apply the changes
        tex.Apply();

        // Assign the texture to the RawImage
        rawImage.texture = tex;
    }
}

