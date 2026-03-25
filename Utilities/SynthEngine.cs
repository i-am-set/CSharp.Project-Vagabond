using Microsoft.Xna.Framework.Audio;
using System;

namespace ProjectVagabond.Audio
{
    public static class SynthEngine
    {
        private const int SAMPLE_RATE = 44100;
        public static SoundEffect Generate(string configString)
        {
            int waveType = 0; // 0=Square, 1=Sawtooth, 2=Sine, 3=WhiteNoise, 4=Triangle, 5=BrownNoise, 6=PinkNoise
            float attackTime = 0.1f;
            float sustainTime = 0.1f;
            float decayTime = 0.2f;
            float startFrequency = 440f;
            float minFrequency = 0f;
            float slide = 0f;
            float deltaSlide = 0f;
            float dutyCycle = 0.5f;
            float dutySweep = 0f;
            float vibratoDepth = 0f;
            float vibratoSpeed = 0f;
            float volume = 0.5f;

            // DSP Parameters
            float lpf = 22050f;
            float lpfSweep = 0f;
            float res = 1.0f;
            float hpf = 0f;
            float hpfSweep = 0f;
            int crush = 1;

            // Advanced Features
            float distortion = 0f;
            bool exponential = false;

            string[] parts = configString.Replace("proc:", "").Split(';');
            foreach (var part in parts)
            {
                string[] kvp = part.Split('=');
                if (kvp.Length != 2) continue;
                string key = kvp[0].Trim().ToLowerInvariant();
                if (!float.TryParse(kvp[1].Trim(), out float val)) continue;

                switch (key)
                {
                    case "wave": waveType = (int)val; break;
                    case "atk": attackTime = val; break;
                    case "sus": sustainTime = val; break;
                    case "dec": decayTime = val; break;
                    case "freq": startFrequency = val; break;
                    case "minfreq": minFrequency = val; break;
                    case "slide": slide = val; break;
                    case "dslide": deltaSlide = val; break;
                    case "duty": dutyCycle = val; break;
                    case "dutysweep": dutySweep = val; break;
                    case "vibdepth": vibratoDepth = val; break;
                    case "vibspeed": vibratoSpeed = val; break;
                    case "vol": volume = val; break;
                    case "lpf": lpf = val; break;
                    case "lpfsweep": lpfSweep = val; break;
                    case "res": res = val; break;
                    case "hpf": hpf = val; break;
                    case "hpfsweep": hpfSweep = val; break;
                    case "crush": crush = Math.Max(1, (int)val); break;
                    case "dist": distortion = val; break;
                    case "exp": exponential = val > 0; break;
                }
            }

            int attackSamples = (int)(attackTime * SAMPLE_RATE);
            int sustainSamples = (int)(sustainTime * SAMPLE_RATE);
            int decaySamples = (int)(decayTime * SAMPLE_RATE);
            int totalSamples = attackSamples + sustainSamples + decaySamples;

            if (totalSamples <= 0) totalSamples = 100;

            byte[] buffer = new byte[totalSamples * 2];
            Random rnd = new Random();

            double phase = 0.0;
            double currentFreq = startFrequency;
            double currentSlide = slide;
            double currentDuty = dutyCycle;
            double currentLpf = lpf;
            double currentHpf = hpf;
            double svfLow = 0.0, svfHigh = 0.0, svfBand = 0.0;
            double hpfState = 0.0, lastHpfIn = 0.0;
            double lastBrown = 0.0;
            double[] pink = new double[7];
            double heldSample = 0.0;

            for (int i = 0; i < totalSamples; i++)
            {
                double time = (double)i / SAMPLE_RATE;

                // Envelope Logic
                double envVol = 0.0;
                if (i < attackSamples)
                {
                    envVol = (double)i / attackSamples;
                }
                else if (i < attackSamples + sustainSamples)
                {
                    envVol = 1.0;
                }
                else
                {
                    double releasePhase = (double)(i - attackSamples - sustainSamples) / decaySamples;
                    envVol = exponential ? Math.Pow(1.0 - releasePhase, 3.0) : 1.0 - releasePhase;
                }

                currentSlide += deltaSlide * (1.0 / SAMPLE_RATE);
                currentFreq += currentSlide * (1.0 / SAMPLE_RATE);
                if (currentFreq < minFrequency) currentFreq = minFrequency;

                double vibFreq = currentFreq;
                if (vibratoDepth > 0) vibFreq += Math.Sin(time * vibratoSpeed * Math.PI * 2.0) * vibratoDepth;

                currentDuty += dutySweep * (1.0 / SAMPLE_RATE);
                currentDuty = Math.Clamp(currentDuty, 0.0, 1.0);

                phase += (vibFreq * Math.PI * 2.0) / SAMPLE_RATE;
                if (phase >= Math.PI * 2.0) phase -= Math.PI * 2.0;

                double sample = 0.0;

                switch (waveType)
                {
                    case 0: sample = (phase < Math.PI * 2.0 * currentDuty) ? 1.0 : -1.0; break;
                    case 1: sample = 1.0 - (phase / Math.PI); break;
                    case 2: sample = Math.Sin(phase); break;
                    case 3: sample = (rnd.NextDouble() * 2.0) - 1.0; break;
                    case 4: sample = 2.0 * Math.Abs(2.0 * (phase / (Math.PI * 2.0)) - 1.0) - 1.0; break;
                    case 5: // Brown
                        double whiteB = (rnd.NextDouble() * 2.0) - 1.0;
                        lastBrown = (lastBrown + (0.02 * whiteB)) / 1.02;
                        sample = lastBrown * 3.5;
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

                if (crush > 1)
                {
                    if (i % crush == 0) heldSample = sample;
                    sample = heldSample;
                }

                currentLpf += lpfSweep * (1.0 / SAMPLE_RATE);
                double cutoff = Math.Clamp(currentLpf, 10.0, SAMPLE_RATE * 0.45);
                if (cutoff < SAMPLE_RATE * 0.45)
                {
                    double f = 2.0 * Math.Sin(Math.PI * cutoff / SAMPLE_RATE);
                    double q = 1.0 / Math.Max(0.1, res);
                    svfLow += f * svfBand;
                    svfHigh = sample - svfLow - q * svfBand;
                    svfBand += f * svfHigh;
                    sample = svfLow;
                }

                currentHpf += hpfSweep * (1.0 / SAMPLE_RATE);
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

                if (distortion > 0)
                {
                    sample *= (1.0 + distortion);
                    sample = Math.Clamp(sample, -1.1, 1.1);
                    sample = (3.0 * sample - Math.Pow(sample, 3.0)) / 2.0;
                }

                sample *= envVol * volume;
                sample = Math.Clamp(sample, -1.0, 1.0);

                short shortSample = (short)(sample * short.MaxValue);
                buffer[i * 2] = (byte)(shortSample & 0xFF);
                buffer[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
            }

            return new SoundEffect(buffer, SAMPLE_RATE, AudioChannels.Mono);
        }
    }
}