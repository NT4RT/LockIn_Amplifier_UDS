/*************************************************************************************************
*      By Bruce Randall  NT4RT      January 18, 2013
* Sound output of Stereo Square Wave.  Edges slew controlled.  
* Feb 3, 2013      Outputs 2 invert of output 1 for push-pull switch drive.
************************************************************************************************/
using System;
using NAudio.Wave;

// Below part of project but not used by this namespace
// using System.Collections.Generic;
// using System.Text;      
// using System.IO;         
// using Setup;             
// using Lockin_Amplifier1; 

namespace SquareWave_Out   
{
    public abstract class WaveProvider32 : IWaveProvider
    {
        private WaveFormat waveFormat;

        public WaveProvider32() : this(8000, 2)
        {
        }

        public WaveProvider32(int sampleRate, int channels)
        {
            SetWaveFormat(sampleRate, channels);
        }

        public void SetWaveFormat(int sampleRate, int channels)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            WaveBuffer waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;                //  from 4 bytes make a float 
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);  // Why Read(... ? why 4?
            return samplesRead * 4;                         // from 4 bytes make a float
        }

        public abstract int Read(float[] buffer, int offset, int sampleCount);

        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }
    }

//****************************************

    public class SquareWaveProvider32 : WaveProvider32
    {
        public SquareWaveProvider32()
        {
            SampHalfCycle = 99;     // settings overidden by calling program
            SampSlew = 9;
            Amplitude = 0.05f;      // let's not hurt our ears
            WvCount = 0;
        }
   
        public int SampHalfCycle;
        public int SampSlew;        // num samp in edge
        public float Amplitude;
        int WvCount;
        //int TestCount = 0;

        public override int Read(float[] buffer, int offset, int sampleCount)
        {     
            float sqW = 0;
            int sampleRate = WaveFormat.SampleRate;
            float step = 2 * Amplitude / SampSlew;
            //Console.Write("*");
            //Console.Write(sampleCount.ToString());    // count include right and left.  = 2 x num samples
            for (int n = 0; n < sampleCount; n = n+2)   //count out block.  use n = n+1 for mono
            {
                if (WvCount >= 2*SampHalfCycle)
                    WvCount = 0;
                if (WvCount >= SampHalfCycle)           // 2nd half cycle
                    sqW = Amplitude;
                else
                    sqW = -1 * Amplitude;               // 1st half cycle

                if ((WvCount >= 0) && (WvCount < SampSlew))  //if slewing - to +
                    sqW = -step * WvCount + Amplitude;
                if ((WvCount >= SampHalfCycle) && (WvCount < (SampHalfCycle+SampSlew)))  //if slewing + to -
                    sqW = (WvCount - SampHalfCycle) * step - Amplitude;
                //if (TestCount < 500)                        // count samples for diag file
                //{
                //    TestCount++;
                //    Program.SQWTestFile.WriteLine(WvCount.ToString() + ", " + (sqW * 100).ToString());
                //}
                WvCount++;
                buffer[n + offset] = sqW;
                buffer[n + offset + 1] = -1.0f * sqW;   // Value for 2nd AF Channel.  Delete line for mono
            }
            return sampleCount;
        }
    }
}