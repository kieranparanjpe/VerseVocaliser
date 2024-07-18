using System.Collections;
using System.Collections.Generic;
using System;
using System.Drawing.Printing;
using System.Numerics;
using UnityEngine;
using UnityEngine.Profiling;
using FftFlat;
using Complex = System.Numerics.Complex;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
public class AudioTransformations
{
    public static MathNet.Numerics.LinearAlgebra.Matrix<float> MelSpectrogram(float[] signal, int nFFT, int windowSize, int hopLength, int sampleRate, int nMels)
    {
        MathNet.Numerics.LinearAlgebra.Matrix<float> spectrogram = Spectrogram(signal, nFFT, windowSize, hopLength);
        MathNet.Numerics.LinearAlgebra.Matrix<float> filterbanks = MelFilterbanks_T(nFFT, sampleRate, nMels);

        return filterbanks.Multiply(spectrogram);
    }

    private static MathNet.Numerics.LinearAlgebra.Matrix<float> MelFilterbanks_T(int nFFT, int sampleRate, int nMels)
    {
        int nSTFT = nFFT / 2 + 1;
        double[] allFreqs = Generate.LinearSpaced(nSTFT, 0, sampleRate / 2);

        float minMel = hzToMel(0);
        float maxMel = hzToMel(sampleRate / 2);

        double[] melPts = Generate.LinearSpaced(nMels + 2, minMel, maxMel);

        float[] freqPts = new float[melPts.Length];

        for (int i = 0; i < freqPts.Length; i++)
        {
            freqPts[i] = melToHz((float)melPts[i]);
        }

        float[] freqDiff = new float[freqPts.Length - 1];

        for (int i = 0; i < freqDiff.Length; i++)
        {
            freqDiff[i] = freqPts[i + 1] - freqPts[i];
        }

        MathNet.Numerics.LinearAlgebra.Matrix<float> slopes = Matrix<float>.Build.Dense(nSTFT, freqPts.Length);
        MathNet.Numerics.LinearAlgebra.Matrix<float> fb = Matrix<float>.Build.Dense(nMels, nSTFT);

        for (int r = 0; r < slopes.RowCount; r++)
        {
            for (int c = 0; c < slopes.ColumnCount; c++)
            {
                slopes[r, c] =  freqPts[c] - (float)allFreqs[r];
            }
        }

        for (int r = 0; r < nSTFT; r++)
        {
            for (int c = 0; c < nMels; c++)
            {
                float downSlope = -1f * slopes[r, c] / freqDiff[c];
                float upSlope = slopes[r, c + 2] / freqDiff[c + 1];

                fb[c, r] = Mathf.Max(0, Mathf.Min(downSlope, upSlope));
            }
        }

        return fb;
    }

    private static float melToHz(float mel)
    {
        return 700f * (Mathf.Pow(10, mel / 2595f) - 1f);
    }

    private static float hzToMel(float hz)
    {
        return 2595f * Mathf.Log10(1 + hz / 700f);
    }
    
    
    //overall time of this was ~60 seconds at worst
    public static MathNet.Numerics.LinearAlgebra.Matrix<float> Spectrogram(float[] signal, int nFFT, int windowSize, int hopLength)
    {
        Profiler.BeginSample("stft");
        MathNet.Numerics.LinearAlgebra.Matrix<Complex> stft = STFT(signal, nFFT, windowSize, hopLength);
        Profiler.EndSample();
        
        Profiler.BeginSample("compute stft square mag");
        MathNet.Numerics.LinearAlgebra.Matrix<float> spectrogramFloat = new MathNet.Numerics.LinearAlgebra.Single.DenseMatrix(stft.RowCount, stft.ColumnCount); //4
        stft.MapConvert(x => (float)x.MagnitudeSquared(), spectrogramFloat, Zeros.Include);
        Profiler.EndSample();
        
        return spectrogramFloat;
    }

    public static MathNet.Numerics.LinearAlgebra.Matrix<Complex> SpectrogramComplex(float[] signal, int nFFT,
        int windowSize, int hopLength)
    {
        Profiler.BeginSample("stft");
        MathNet.Numerics.LinearAlgebra.Matrix<Complex> stft = STFT(signal, nFFT, windowSize, hopLength);
        Profiler.EndSample();
        
        Profiler.BeginSample("compute stft mag squared");
        stft.MapInplace(x => x.MagnitudeSquared()); //1
        Profiler.EndSample();
        
        return stft;
    }
    
    public static MathNet.Numerics.LinearAlgebra.Matrix<Complex> STFT(float[] signal, int nFFT, int windowSize, int hopLength)
    {
        if (nFFT > signal.Length)
            throw new ArgumentOutOfRangeException("nFFT must be less than or equal to signal length");
        
        signal = PadReflect(signal, windowSize / 2);
        
        float[] hanningWindow = HanningWindow(windowSize);
        
        int numberChunks = (signal.Length - windowSize) / hopLength + 1;
        
        MathNet.Numerics.LinearAlgebra.Matrix<Complex> stftMatrix = new DenseMatrix(windowSize/2+1, numberChunks);
        
        for (int i = 0; i < numberChunks; i++)
        {
            if (i * hopLength + windowSize >= signal.Length)
                continue;
                    
            float[] chunk = new float[windowSize];
            Array.Copy(signal, i * hopLength, chunk, 0, windowSize);
            
            for (int j = 0; j < windowSize; j++)
            {
                chunk[j] *= hanningWindow[j];
            }

            if (nFFT < chunk.Length)
                Array.Resize(ref chunk, nFFT);
            
            Complex[] chunkComplex = Array.ConvertAll(chunk, x => new Complex(x, 0));
            
            Profiler.BeginSample("fft");
            var fft = new FastFourierTransform(nFFT);
            fft.Forward(chunkComplex);
            Profiler.EndSample();
            
            for (int r = 0; r < stftMatrix.RowCount; r++)
            {
                stftMatrix[r, i] = chunkComplex[r];
            }
        }
        return stftMatrix;
    }

    private static float[] HanningWindow(int n)
    {
        float[] window = new float[n];

        for (int i = 0; i < n; i++)
        {
            window[i] = 0.5f - 0.5f * Mathf.Cos(2.0f * Mathf.PI * i / n);
        }

        return window;
    }

    private static float[] PadReflect(float[] array, int n)
    {
        if (n >= array.Length)
            throw new ArgumentException("n must be less than length of array");
        float[] newArray = new float[array.Length + 2 * n];

        for (int i = 0; i < newArray.Length; i++)
        {
            if (i < n)
            {
                newArray[i] = array[n - i];
            }
            else if (i >= newArray.Length - n)
            {
                newArray[i] = array[(2 * newArray.Length) - (3 * n) - 2 - i];
            }
            else
            {
                newArray[i] = array[i - n];
            }
        }

        return newArray;
    }
}
