using System;
using System.Buffers;
using Microsoft.Xna.Framework.Audio;

namespace MonoGameSample;

public enum WaveType
{
    Sin,
    Tan,
    Square,
    Noise
}

public class AudioSource
{
    private static readonly Random Rand = new();
    private readonly DynamicSoundEffectInstance dsei;
    private readonly int sampleRate = 16000;
    private int totalTime;

    public AudioSource()
    {
        dsei = new(sampleRate, AudioChannels.Mono);
        dsei.Volume = 0.4f;
        dsei.IsLooped = false;
    }

    public void PlayWave(double freq, short durMs, WaveType wt, float volume)
    {
        dsei.Stop();

        var bufferSize = dsei.GetSampleSizeInBytes(TimeSpan.FromMilliseconds(durMs));
            
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var size = bufferSize - 1;
            for (var i = 0; i < size; i += 2)
            {
                var time = totalTime / (double)sampleRate;

                short currentSample = 0;
                switch (wt)
                {
                    case WaveType.Sin:
                    {
                        currentSample = (short)(Math.Sin(2 * Math.PI * freq * time) * short.MaxValue * volume);
                        break;
                    }
                    case WaveType.Tan:
                    {
                        currentSample = (short)(Math.Tan(2 * Math.PI * freq * time) * short.MaxValue * volume);
                        break;
                    }
                    case WaveType.Square:
                    {
                        currentSample = (short)(Math.Sign(Math.Sin(2 * Math.PI * freq * time)) *
                                                (double)short.MaxValue *
                                                volume);
                        break;
                    }
                    case WaveType.Noise:
                    {
                        currentSample = (short)(Rand.Next(-short.MaxValue, short.MaxValue) * volume);
                        break;
                    }
                }

                buffer[i] = (byte)(currentSample & 0xFF);
                buffer[i + 1] = (byte)(currentSample >> 8);
                totalTime += 2;
            }

            dsei.SubmitBuffer(buffer, 0, bufferSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
            
        dsei.Play();
    }
}