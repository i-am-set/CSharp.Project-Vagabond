using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;

namespace ProjectVagabond.Audio
{
    public static class SynthEngine
    {
        private const int SAMPLE_RATE = 44100;

        private class LayerParams
        {
            public int WaveType = 0;
            public float AttackTime = 0.1f;
            public float SustainTime = 0.1f;
            public float DecayTime = 0.2f;
            public float StartFrequency = 440f;
            public float MinFrequency = 0f;
            public float Slide = 0f;
            public float DeltaSlide = 0f;
            public float PitchSnap = 0f;
            public float DutyCycle = 0.5f;
            public float DutySweep = 0f;
            public float VibratoDepth = 0f;
            public float VibratoSpeed = 0f;
            public float Volume = 0.5f;
            public float Lpf = 22050f;
            public float LpfSweep = 0f;
            public float Res = 1.0f;
            public float Hpf = 0f;
            public float HpfSweep = 0f;
            public int Crush = 1;
            public float Distortion = 0f;
            public float Saturate = 0f;
            public bool Exponential = false;

            public float DelayTime = 0f;
            public float DelayFeedback = 0f;
            public float Detune = 0f;

            public int AttackSamples => (int)(AttackTime * SAMPLE_RATE);
            public int SustainSamples => (int)(SustainTime * SAMPLE_RATE);
            public int DecaySamples => (int)(DecayTime * SAMPLE_RATE);
            public int DrySamples => AttackSamples + SustainSamples + DecaySamples;

            // Extend total samples to allow delay echoes to ring out naturally
            public int TotalSamples => DrySamples + (DelayTime > 0 ? (int)(DelayTime * SAMPLE_RATE * (DelayFeedback > 0.1f ? 6 : 2)) : 0);
        }

        public static SoundEffect Generate(string configString)
        {
            string cleanConfig = configString.StartsWith("proc:", StringComparison.OrdinalIgnoreCase)
                ? configString.Substring(5)
                : configString;

            string[] layerStrings = cleanConfig.Split('|');
            List<LayerParams> layers = new List<LayerParams>();
            int maxSamples = 0;

            foreach (var layerStr in layerStrings)
            {
                var lp = new LayerParams();
                string[] parts = layerStr.Split(';');
                foreach (var part in parts)
                {
                    string[] kvp = part.Split('=');
                    if (kvp.Length != 2) continue;
                    string key = kvp[0].Trim().ToLowerInvariant();
                    if (!float.TryParse(kvp[1].Trim(), out float val)) continue;

                    switch (key)
                    {
                        case "wave": lp.WaveType = (int)val; break;
                        case "atk": lp.AttackTime = Math.Min(val, 5f); break;
                        case "sus": lp.SustainTime = Math.Min(val, 5f); break;
                        case "dec": lp.DecayTime = Math.Min(val, 5f); break;
                        case "freq": lp.StartFrequency = val; break;
                        case "minfreq": lp.MinFrequency = val; break;
                        case "slide": lp.Slide = val; break;
                        case "dslide": lp.DeltaSlide = val; break;
                        case "psnap": lp.PitchSnap = val; break;
                        case "duty": lp.DutyCycle = val; break;
                        case "dutysweep": lp.DutySweep = val; break;
                        case "vibdepth": lp.VibratoDepth = val; break;
                        case "vibspeed": lp.VibratoSpeed = val; break;
                        case "vol": lp.Volume = val; break;
                        case "lpf": lp.Lpf = val; break;
                        case "lpfsweep": lp.LpfSweep = val; break;
                        case "res": lp.Res = val; break;
                        case "hpf": lp.Hpf = val; break;
                        case "hpfsweep": lp.HpfSweep = val; break;
                        case "crush": lp.Crush = Math.Max(1, (int)val); break;
                        case "dist": lp.Distortion = val; break;
                        case "sat": lp.Saturate = val; break;
                        case "exp": lp.Exponential = val > 0; break;

                        // NEW: Parsers
                        case "delay": lp.DelayTime = val; break;
                        case "delfb": lp.DelayFeedback = val; break;
                        case "detune": lp.Detune = val; break;
                    }
                }
                layers.Add(lp);
                if (lp.TotalSamples > maxSamples) maxSamples = lp.TotalSamples;
            }

            if (maxSamples <= 0) maxSamples = 100;

            double[] mixBuffer = new double[maxSamples];
            Random rnd = new Random();

            foreach (var lp in layers)
            {
                double phase = 0.0;
                double phase2 = 0.0; // Secondary phase for detune/chorus
                double currentFreq = lp.StartFrequency;
                double currentSlide = lp.Slide;
                double currentDuty = lp.DutyCycle;
                double currentLpf = lp.Lpf;
                double currentHpf = lp.Hpf;
                double svfLow = 0.0, svfHigh = 0.0, svfBand = 0.0;
                double hpfState = 0.0, lastHpfIn = 0.0;
                double lastBrown = 0.0;
                double[] pink = new double[7];
                double heldSample = 0.0;

                // Delay Line Initialization
                int delayBufferSize = (int)(lp.DelayTime * SAMPLE_RATE);
                double[] delayBuffer = delayBufferSize > 0 ? new double[delayBufferSize] : null;
                int delayIndex = 0;

                for (int i = 0; i < lp.TotalSamples; i++)
                {
                    double time = (double)i / SAMPLE_RATE;

                    // Envelope Logic (Only applies to DrySamples, delay tail rings out naturally)
                    double envVol = 0.0;
                    if (i < lp.AttackSamples) envVol = (double)i / lp.AttackSamples;
                    else if (i < lp.AttackSamples + lp.SustainSamples) envVol = 1.0;
                    else if (i < lp.DrySamples)
                    {
                        double releasePhase = (double)(i - lp.AttackSamples - lp.SustainSamples) / lp.DecaySamples;
                        envVol = lp.Exponential ? Math.Pow(1.0 - releasePhase, 3.0) : 1.0 - releasePhase;
                    }

                    // Pitch Logic
                    currentSlide += lp.DeltaSlide * (1.0 / SAMPLE_RATE);
                    double slideDelta = currentSlide * (1.0 / SAMPLE_RATE);
                    currentFreq += lp.Exponential ? (currentFreq * slideDelta * 0.01) : slideDelta;

                    double snapOffset = lp.PitchSnap * Math.Max(0, 1.0 - (time * 20.0));
                    double vibFreq = currentFreq + snapOffset;

                    if (lp.VibratoDepth > 0) vibFreq += Math.Sin(time * lp.VibratoSpeed * Math.PI * 2.0) * lp.VibratoDepth;
                    if (vibFreq < lp.MinFrequency) vibFreq = lp.MinFrequency;

                    currentDuty += lp.DutySweep * (1.0 / SAMPLE_RATE);
                    currentDuty = Math.Clamp(currentDuty, 0.0, 1.0);

                    phase += (vibFreq * Math.PI * 2.0) / SAMPLE_RATE;
                    if (phase >= Math.PI * 2.0) phase -= Math.PI * 2.0;

                    double sample = 0.0;

                    // Only generate base oscillator if envelope is active (saves CPU in delay tail)
                    if (envVol > 0.001)
                    {
                        switch (lp.WaveType)
                        {
                            case 0: sample = (phase < Math.PI * 2.0 * currentDuty) ? 1.0 : -1.0; break; // Pulse
                            case 1: sample = 1.0 - (phase / Math.PI); break; // Saw
                            case 2: sample = Math.Sin(phase); break; // Sine
                            case 3: sample = (rnd.NextDouble() * 2.0) - 1.0; break; // White
                            case 4: sample = 2.0 * Math.Abs(2.0 * (phase / (Math.PI * 2.0)) - 1.0) - 1.0; break; // Tri
                            case 5: // Brown
                                double whiteB = (rnd.NextDouble() * 2.0) - 1.0;
                                lastBrown = (lastBrown + (0.05 * whiteB)) / 1.05;
                                sample = lastBrown * 4.5;
                                break;
                            case 6: // Pink
                                double whiteP = (rnd.NextDouble() * 2.0) - 1.0;
                                pink[0] = 0.99886 * pink[0] + whiteP * 0.0555179;
                                pink[1] = 0.99332 * pink[1] + whiteP * 0.0750759;
                                pink[2] = 0.96900 * pink[2] + whiteP * 0.1538520;
                                pink[3] = 0.86650 * pink[3] + whiteP * 0.3104856;
                                pink[4] = 0.55000 * pink[4] + whiteP * 0.5329522;
                                pink[5] = -0.7616 * pink[5] - whiteP * 0.0168980;
                                sample = (pink[0] + pink[1] + pink[2] + pink[3] + pink[4] + pink[5] + pink[6] + whiteP * 0.5362) * 0.11;
                                pink[6] = whiteP * 0.115926;
                                break;
                        }

                        // NEW: Detune / Unison Layer
                        if (lp.Detune > 0 && lp.WaveType < 5) // Skip noise for detune
                        {
                            double vibFreq2 = vibFreq * (1.0 + lp.Detune);
                            phase2 += (vibFreq2 * Math.PI * 2.0) / SAMPLE_RATE;
                            if (phase2 >= Math.PI * 2.0) phase2 -= Math.PI * 2.0;

                            double sample2 = 0.0;
                            switch (lp.WaveType)
                            {
                                case 0: sample2 = (phase2 < Math.PI * 2.0 * currentDuty) ? 1.0 : -1.0; break;
                                case 1: sample2 = 1.0 - (phase2 / Math.PI); break;
                                case 2: sample2 = Math.Sin(phase2); break;
                                case 4: sample2 = 2.0 * Math.Abs(2.0 * (phase2 / (Math.PI * 2.0)) - 1.0) - 1.0; break;
                            }
                            // Mix and slightly lower volume to compensate for doubling
                            sample = (sample + sample2) * 0.6;
                        }

                        if (lp.Crush > 1)
                        {
                            if (i % lp.Crush == 0) heldSample = sample;
                            sample = heldSample;
                        }

                        // Filters
                        currentLpf += lp.LpfSweep * (1.0 / SAMPLE_RATE);
                        double cutoff = Math.Clamp(currentLpf, 10.0, SAMPLE_RATE * 0.45);
                        if (cutoff < SAMPLE_RATE * 0.45)
                        {
                            double f = 2.0 * Math.Sin(Math.PI * cutoff / SAMPLE_RATE);
                            double q = 1.0 / Math.Max(0.1, lp.Res);
                            svfLow += f * svfBand;
                            svfHigh = sample - svfLow - q * svfBand;
                            svfBand += f * svfHigh;
                            sample = svfLow;
                        }

                        currentHpf += lp.HpfSweep * (1.0 / SAMPLE_RATE);
                        double hCutoff = Math.Clamp(currentHpf, 0.0, SAMPLE_RATE * 0.45);
                        if (hCutoff > 10.0)
                        {
                            double rc = 1.0 / (2.0 * Math.PI * hCutoff);
                            double dt = 1.0 / SAMPLE_RATE;
                            double alpha = rc / (rc + dt);
                            hpfState = alpha * (hpfState + sample - lastHpfIn);
                            lastHpfIn = sample;
                            sample = hpfState;
                        }

                        // Overdrive / Saturation
                        if (lp.Saturate > 0)
                        {
                            sample *= (1.0 + lp.Saturate);
                            sample = Math.Tanh(sample);
                        }

                        if (lp.Distortion > 0)
                        {
                            sample *= (1.0 + lp.Distortion);
                            sample = Math.Clamp(sample, -1.1, 1.1);
                            sample = (3.0 * sample - Math.Pow(sample, 3.0)) / 2.0;
                        }

                        sample *= envVol * lp.Volume;
                    }

                    if (delayBuffer != null)
                    {
                        double delayedSample = delayBuffer[delayIndex];
                        // Write current dry sample + feedback back into the buffer
                        delayBuffer[delayIndex] = sample + (delayedSample * lp.DelayFeedback);
                        // Mix delayed sample into the output (50% wet mix)
                        sample += delayedSample * 0.5;
                        delayIndex = (delayIndex + 1) % delayBufferSize;
                    }

                    mixBuffer[i] += sample;
                }
            }

            byte[] buffer = new byte[maxSamples * 2];
            for (int i = 0; i < maxSamples; i++)
            {
                double sample = Math.Clamp(mixBuffer[i], -1.0, 1.0);
                short shortSample = (short)(sample * short.MaxValue);
                buffer[i * 2] = (byte)(shortSample & 0xFF);
                buffer[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
            }

            return new SoundEffect(buffer, SAMPLE_RATE, AudioChannels.Mono);
        }
    }
}