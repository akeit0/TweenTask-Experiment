using System;
using System.Buffers;
using Microsoft.Xna.Framework.Audio;

namespace MonoGameSample
{
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
        private readonly DynamicSoundEffectInstance DSEI;
        private readonly int SampleRate = 16000;
        private int TotalTime;

        public AudioSource()
        {
            DSEI = new(SampleRate, AudioChannels.Mono);
            DSEI.Volume = 0.4f;
            DSEI.IsLooped = false;
        }

        public void PlayWave(double freq, short durMS, WaveType Wt, float Volume)
        {
            DSEI.Stop();

            var bufferSize = DSEI.GetSampleSizeInBytes(TimeSpan.FromMilliseconds(durMS));
            
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                var size = bufferSize - 1;
                for (var i = 0; i < size; i += 2)
                {
                    var time = TotalTime / (double)SampleRate;

                    short currentSample = 0;
                    switch (Wt)
                    {
                        case WaveType.Sin:
                        {
                            currentSample = (short)(Math.Sin(2 * Math.PI * freq * time) * short.MaxValue * Volume);
                            break;
                        }
                        case WaveType.Tan:
                        {
                            currentSample = (short)(Math.Tan(2 * Math.PI * freq * time) * short.MaxValue * Volume);
                            break;
                        }
                        case WaveType.Square:
                        {
                            currentSample = (short)(Math.Sign(Math.Sin(2 * Math.PI * freq * time)) *
                                                    (double)short.MaxValue *
                                                    Volume);
                            break;
                        }
                        case WaveType.Noise:
                        {
                            currentSample = (short)(Rand.Next(-short.MaxValue, short.MaxValue) * Volume);
                            break;
                        }
                    }

                    buffer[i] = (byte)(currentSample & 0xFF);
                    buffer[i + 1] = (byte)(currentSample >> 8);
                    TotalTime += 2;
                }

                DSEI.SubmitBuffer(buffer, 0, bufferSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            
            DSEI.Play();
        }
    }
}