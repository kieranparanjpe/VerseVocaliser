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

//Made by Kieran Paranjpe for The Verse, July 2024.

/// <summary>
/// AudioTransformations is a class designed to compute the Short Time Fourier Transform, Spectrogram and Mel Spectrogram of an audio signal.
/// It is meant to produce the same output as the coresponding functions in torchaudio.
/// Can be used in two ways, with static methods (good for infrequent calls / calls where parameters change frequently), or by instantiating the class and using methods (will be faster because it can pre compute some values).
///If audio transformations have performance issues, can use the C library pffft (pretty fast fft) and rewrite bits of this (likely the fft calls in STFT) in C.
/// </summary>
public class AudioTransformations
{
    //all private variables are just to store things that can be precomputed
    private float[] hanningWindow;
    private MathNet.Numerics.LinearAlgebra.Matrix<Complex> stftMatrix;
    private MathNet.Numerics.LinearAlgebra.Matrix<float> filterbanks;
    private FastFourierTransform fft;

    private int numberChunks;
    
    private int nFFT;
    private int windowSize;
    private int hopLength;
    private int sampleRate;
    private int nMels;
    

    public AudioTransformations(int length, int nFFT, int windowSize, int hopLength, int sampleRate, int nMels)
    {
        //save settings
        this.nFFT = nFFT;
        this.windowSize = windowSize;
        this.hopLength = hopLength;
        this.sampleRate = sampleRate;
        this.nMels = nMels;
        
        this.numberChunks = (length) / hopLength + 1;
        
        // precompute certain things that shouldnt change if the input signal changes (only dependant on parameters)
        hanningWindow = HanningWindow(this.windowSize);
        stftMatrix = new DenseMatrix(windowSize/2+1, this.numberChunks);
        fft = new FastFourierTransform(this.nFFT);
        filterbanks = MelFilterbanks_T(this.nFFT, this.sampleRate, this.nMels);

    }
    
    /// <summary>
    /// Compute the mel spectrogram of the signal based on parameters specified in constructor 
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <returns>Matrix representing the MelSpectrogram. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
    public MathNet.Numerics.LinearAlgebra.Matrix<float> MelSpectrogram(float[] signal)
    {
        MathNet.Numerics.LinearAlgebra.Matrix<float> spectrogram = Spectrogram(signal);

        // this might normally be given as (spectrogram.T @ filterbanks).T
        // I wrote my filterbanks function to just return the transpose of filterbanks
        // And I use (AB).T = B.T @ A.T, so the above = filterbanks.T @ spectrogram
        //did this so i don't take an unneeded transpose 
        return filterbanks.Multiply(spectrogram);
    } 
    
    /// <summary>
    /// Compute the mel spectrogram of the signal.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <param name="nFFT">Length to FFT</param>
    /// <param name="windowSize">Window size for STFT. Torchaudio defaults this to the same as nFFT.</param>>
    /// <param name="hopLength">Hop Length for STFT.</param>
    /// <param name="nMels">Number of Mel Bands. This will be the height of the output matrix.</param>
    /// <returns>Matrix representing the MelSpectrogram. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
    public static MathNet.Numerics.LinearAlgebra.Matrix<float> MelSpectrogram(float[] signal, int nFFT, int windowSize, int hopLength, int sampleRate, int nMels)
    {
        MathNet.Numerics.LinearAlgebra.Matrix<float> spectrogram = Spectrogram(signal, nFFT, windowSize, hopLength);
        MathNet.Numerics.LinearAlgebra.Matrix<float> filterbanks = MelFilterbanks_T(nFFT, sampleRate, nMels);

        // this might normally be given as (spectrogram.T @ filterbanks).T
        // I wrote my filterbanks function to just return the transpose of filterbanks
        // And I use (AB).T = B.T @ A.T, so the above = filterbanks.T @ spectrogram
        //did this so i don't take an unneeded transpose 
        return filterbanks.Multiply(spectrogram);
    }

    //compute mel filterbanks matrix
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
    
    
    /// <summary>
    /// Compute the spectrogram of the signal.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <param name="nFFT">Length to FFT</param>
    /// <param name="windowSize">Window size for STFT. Torchaudio defaults this to the same as nFFT.</param>>
    /// <param name="hopLength">Hop Length for STFT.</param>
    /// <returns>Matrix representing the Spectrogram. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
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

    /// <summary>
    /// Compute the spectrogram of the signal, and return it in the original complex datatype. This is slightly faster than the float version.
    /// The result will still be real numbers, but it is an in-place operation on the STFT, so it returns the complex datatype.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <param name="nFFT">Length to FFT</param>
    /// <param name="windowSize">Window size for STFT. Torchaudio defaults this to the same as nFFT.</param>>
    /// <param name="hopLength">Hop Length for STFT.</param>
    /// <returns>Matrix representing the Spectrogram. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
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
    
    /// <summary>
    /// Compute the spectrogram of the signal based on params from constructor.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <returns>Matrix representing the Spectrogram. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
    public MathNet.Numerics.LinearAlgebra.Matrix<float> Spectrogram(float[] signal)
    {
        Profiler.BeginSample("stft");
        MathNet.Numerics.LinearAlgebra.Matrix<Complex> stft = STFT(signal);
        Profiler.EndSample();
        
        Profiler.BeginSample("compute stft square mag");
        MathNet.Numerics.LinearAlgebra.Matrix<float> spectrogramFloat = new MathNet.Numerics.LinearAlgebra.Single.DenseMatrix(stft.RowCount, stft.ColumnCount); //4
        stft.MapConvert(x => (float)x.MagnitudeSquared(), spectrogramFloat, Zeros.Include);
        Profiler.EndSample();
        
        return spectrogramFloat;
    }


    // compute hanning window for input size N
    private static float[] HanningWindow(int n)
    {
        float[] window = new float[n];

        for (int i = 0; i < n; i++)
        {
            window[i] = 0.5f - 0.5f * Mathf.Cos(2.0f * Mathf.PI * i / n);
        }

        return window;
    }
    
    /// <summary>
    /// Compute the short time fourier transform of the signal.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <param name="nFFT">Length to FFT</param>
    /// <param name="windowSize">Window size for STFT. Torchaudio defaults this to the same as nFFT.</param>>
    /// <param name="hopLength">Hop Length for STFT.</param>
    /// <returns>Complex matrix representing STFT. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
    public static MathNet.Numerics.LinearAlgebra.Matrix<Complex> STFT(float[] signal, int nFFT, int windowSize, int hopLength)
    {
        if (nFFT > signal.Length)
            throw new ArgumentOutOfRangeException("nFFT must be less than or equal to signal length");
        
        // this just takes an array that looks like this: [1, 2, 3, 4, 5, 6], with say n=2 and makes this -> [3,2,1,2,3,4,5,6,5,4]
        // It just here cuz torchaudio does it, idk why else
        signal = PadReflect(signal, windowSize / 2);
        
        //make hanning window
        float[] hanningWindow = HanningWindow(windowSize);
        
        int numberChunks = (signal.Length - windowSize) / hopLength + 1;
        
        //create output matrix
        MathNet.Numerics.LinearAlgebra.Matrix<Complex> stftMatrix = new DenseMatrix(windowSize/2+1, numberChunks);
        
        var fft = new FastFourierTransform(nFFT);

        //loop over signal and look at it in chunks of window size, seperated by hop length
        for (int i = 0; i < numberChunks; i++)
        {
            if (i * hopLength + windowSize >= signal.Length)
                continue;
                    
            //create and copy current operating chunk
            float[] chunk = new float[windowSize];
            Array.Copy(signal, i * hopLength, chunk, 0, windowSize);
            
            //apply hannding window
            for (int j = 0; j < windowSize; j++)
            {
                chunk[j] *= hanningWindow[j];
            }

            if (nFFT < chunk.Length)
                Array.Resize(ref chunk, nFFT);
            
            //convert the chunk to complex dtype so it can be fft'd
            Complex[] chunkComplex = Array.ConvertAll(chunk, x => new Complex(x, 0));
            
            // do the fft (profiler stuff is just for unity performance measuring, has no performance impact when built)
            Profiler.BeginSample("fft");
            fft.Forward(chunkComplex);
            Profiler.EndSample();
            
            //add each chunk as a column of the output matrix
            for (int r = 0; r < stftMatrix.RowCount; r++)
            {
                stftMatrix[r, i] = chunkComplex[r];
            }
        }
        return stftMatrix;
    }

    /// <summary>
    /// Compute the short time fourier transform of the signal based on params from constructor.
    /// </summary>
    /// <param name="signal">Input signal, should be mapped between -1f and 1f. Can use Unity's AudioClip.GetData() for input.</param>
    /// <returns>Complex matrix representing STFT. Please look at docs for MathNet if datatype is confusing, basically a 2d array.</returns>
    public MathNet.Numerics.LinearAlgebra.Matrix<Complex> STFT(float[] signal)
    {
        if (nFFT > signal.Length)
            throw new ArgumentOutOfRangeException("nFFT must be less than or equal to signal length");
        
        // this just takes an array that looks like this: [1, 2, 3, 4, 5, 6], with say n=2 and makes this -> [3,2,1,2,3,4,5,6,5,4]
        // It just here cuz torchaudio does it, idk why else
        signal = PadReflect(signal, windowSize / 2);
        
        //loop over signal and look at it in chunks of window size, seperated by hop length
        for (int i = 0; i < numberChunks; i++)
        {
            if (i * hopLength + windowSize >= signal.Length)
                continue;
            
            //create complex array
            Complex[] chunkComplex = new Complex[windowSize];
            //apply hanning window, and convert chunk to complex array all in one loop (could be done in static method, but tbh im not using it)
            for (int j = i * hopLength; j < i * hopLength + windowSize; j++)
            {
                int zeroIndex = j - i * hopLength;
                chunkComplex[zeroIndex] = new Complex(hanningWindow[zeroIndex] * signal[j], 0);
            }

            if (nFFT < chunkComplex.Length)
                Array.Resize(ref chunkComplex, nFFT);
            
            // do the fft (profiler stuff is just for unity performance measuring, has no performance impact when built)
            Profiler.BeginSample("fft");
            fft.Forward(chunkComplex);
            Profiler.EndSample();
            
            //add each chunk as a column of the output matrix
            for (int r = 0; r < stftMatrix.RowCount; r++)
            {
                stftMatrix[r, i] = chunkComplex[r];
            }
        }
        return stftMatrix;
    }
    
    // meant to emulate np.pad(arr, (n, n), 'reflect')
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
