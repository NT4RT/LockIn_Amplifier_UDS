// By Bruce Randall  NT4RT      November 1, 2021
/*
 * Uses callback from NAudio  to kick off signal processing for whole process.  Set for 100mS
 */
using System;
using NAudio.Wave;
using Lockin_Amplifier1;
using UDS_Server;

// Below part of this project but not used by this namespace
// using System.Collections.Generic;
// using System.Text;
// using System.IO;
// using Setup;

namespace SignalWave_In
{
    class process
    {
        const bool Do_Screen = true;    // set false to disable console output.  Used for UDS Debug.  

        const int B1 = 0;               // Used to as index in Filter Coef and Filter Memory arrays
        const int B2 = 1;
        const int A2 = 2;
        const int In_Z  = 0;
        const int Out_Z = 1;
         
        // ********************* Below is interrupt routine for a block from ADC *****************
        // -------functions for IIR filter section ---------------------------------------------
        // call Out = DoIIR( Input, Z [Input, Output], Coef[B1, B2, A2], out NextZ) 

        static double DoIIR(double Inp, double[] Z, double[] Coef_IIR, out double NextZ)
        {
            double IIR_Out = Z[Out_Z] * Coef_IIR[A2] + Inp * Coef_IIR[B1] + Z[In_Z] * Coef_IIR[B2];
            NextZ = Inp;
            return IIR_Out;
        }

// ------------variables used by interupt.  Filter Coef based on 48,000 Hz sample rate.-----------
        public static double[] CoefLP1 = {0.2401, 0.2401, 0.5197 };    // B1, B2, A2.  5000Hz LPF
        static double[] MemLP1A = { 0.0, 0.0 };                 // In_Z, Out_Z
        static double[] MemLP1B = { 0.0, 0.0 };                 // In_Z, Out_Z

        public static double[] CoefHP1 = { 0.9967, -0.9967, 0.9935 }; // 50Hz HPF
        static double[] MemHP1A = { 0.0, 0.0 };
        static double[] MemHP1B = { 0.0, 0.0 };

        public static double[] CoefLP_Comp = { -1.0, 0.9264, 0.5197 };    //Comp 584Hz Analog LPF.  Break 5KHz
        static double[] MemLP_Comp = { 0.0, 0.0 };

        public static double[] CoefHP_Comp = { -1.0, 0.9316, 0.9987 };     //Comp 541Hz Analog HPF.  Break 10Hz
        static double[] MemHP_Comp = { 0.0, 0.0 };

        public static double[] RefCoefLP = { 0.2401, 0.2401, 0.5197 };     // B1, B2, A2.  5000Hz LPF
        static double[] RefMemLP  = { 0.0, 0.0 };                   // In_Z, Out_Z

        public static double[] RefCoefHP = { 0.9967, -0.9967, 0.9935 };    // 50Hz HPF
        static double[] RefMemHP = { 0.0, 0.0 };

        public static int PSD_Delay = 5;                        // 1st Delay in samples to make phase shift for PSD
        public static int PSD_Delay2 = 6;                       // 2nd Delay in samples to make phase shift for PSD

        // The following filter is not normally adjusted.  Avoids aliasing to 10Hz samp rate
        public static double[] OutCoefLP = { 0.0002618, 0.0, 0.9997382 };   // B1, B2, A2.  2.0Hz LPF
        static double[] OutMemLPA = { 0.0, 0.0 };                   // In_Z, Out_Z
        static double[] OutMemLPB = { 0.0, 0.0 };                   // In_Z, Out_Z

        // The following filter is done at a 10Hz sample Rate
        public static double[] OutCoefLP_LSR = { 0.03045, 0.03045, 0.93910 };     // B1, B2, A2.  0.1Hz LPF
        static double[] OutMemLP_LSR = { 0.0, 0.0 };                    // In_Z, Out_Z

        static double InputSig = 0;             // from ADC
        static double InputRef = 0;
        static double SigChan = 0;              // After filters
        static double RefChan = 0;

        static double RMS_sum = 0;              // for RMS Calcs
        static double RMS = 0;
        static int callCount = 0;               //Used locally to trigger ADC one time data sample at startup
        const int callCountTrig = 50;           // 100mS callCounts when sample taken
        const int callCountClose = 51;          // Calcount to close the file
        const int callCountMax = 52;            // CalCount stopes here after 1st time
        const int ADC_testsamp = 100;           // number of ADC samples to save

        static double TestSig1 = 0;
        static double TestSig2 = 0;

        static int PosEdgeCounter = 0;          // for 1st delay of reference signal
        static int NegEdgeCounter = 0;
        static bool Edge_Z = false;             // last sample state of reference + = true

        static int PosEdgeCounter2 = 0;         // for 2nd delay of reference signal
        static int NegEdgeCounter2 = 0;
        static bool Edge2_Z = false;            // last sample state of reference + = true
        static bool Edge2 = false;              // last sample state of reference + = true

        static bool Sync_det_State = false;     // present status of phase detector
        static double Sync_Det_Out = 0;
        static double Det_Filt_Out = 0;
        public static double LockIn_Out = 0;    // After Low sample rate LPF & Scale This variable is our actual output to file and screen !!!!

        public static int secondsPer = 2;       // seconds per pass Setup param from setup file
        static int passCount = 0;
        static int Z_Sec = 0;
        static int OutIndex = 1;                // sequential index for output file
 
// -------------- process callback interrupt data block -------------------------------------------------------------
        public static void wi_DataAvailable(object sender, WaveInEventArgs bruce) //bruce is structured now!
        {
            ++callCount;                                                // Control for ADC file at startup.
            if (callCount > callCountMax) callCount = callCountMax;     //Assure we take only one startup ADC sample
            if (callCount == callCountClose)
            {
                Program.ADCTestFile.Flush();
                Program.ADCTestFile.Close();
            }

            //Console.Write(callCount);
            //Console.Write(" ");
            //Console.Write(bruce.BytesRecorded / 4);
            //Console.Write(" ");

            RMS_sum = 0;
            // ******************************** Start of sample rate process ******************************
            for (int i = 0; i < bruce.BytesRecorded / 4; i++)       // do each sample in block.
            {  
                Int16 left = BitConverter.ToInt16(bruce.Buffer, i * 4);
                Int16 right = BitConverter.ToInt16(bruce.Buffer, (i * 4 + 2));
                // ------------------ have ADC data.  Now process. -------------------------------------
                // ---------------- Signal Channel filtering ----------------------------------------
                InputSig = left;                                  // sound board left has analog data.
                // call Out = DoIIR( Input, Z [Input, Output], Coef[B1, B2, A2], out NextZ) 
                MemLP1A[Out_Z] = DoIIR(InputSig      , MemLP1A, CoefLP1, out MemLP1A[In_Z]);    //Sig LPF1
                MemLP1B[Out_Z] = DoIIR(MemLP1A[Out_Z], MemLP1B, CoefLP1, out MemLP1B[In_Z]);    //Sig LPF2
                MemHP1A[Out_Z] = DoIIR(MemLP1B[Out_Z], MemHP1A, CoefHP1, out MemHP1A[In_Z]);    //Sig HPF1
                MemHP1B[Out_Z] = DoIIR(MemHP1A[Out_Z], MemHP1B, CoefHP1, out MemHP1B[In_Z]);    //Sig HPF2
                // Compensations for analog HP and LP Poles.
                MemHP_Comp[Out_Z] = DoIIR(MemHP1B[Out_Z], MemHP_Comp, CoefHP_Comp, out MemHP_Comp[In_Z]);    //Sig HP Comp
                MemLP_Comp[Out_Z] = DoIIR(MemHP_Comp[Out_Z], MemLP_Comp, CoefLP_Comp, out MemLP_Comp[In_Z]);    //Sig LP Comp

                SigChan = MemLP_Comp[Out_Z];
                //SigChan = 4000.0;           // #test code for phase shifter Comment OUT to work!!!

                // ---------------- Reference Channel filtering ---------------------------------------- 
                InputRef = right;                                  // sound board right has reference.
                // call Out = DoIIR( Input, Z [Input, Output], Coef[B1, B2, A2], out NextZ) 
                RefMemLP[Out_Z] = DoIIR(InputRef       , RefMemLP, RefCoefLP, out RefMemLP[In_Z]);    //Ref LPF1
                RefMemHP[Out_Z] = DoIIR(RefMemLP[Out_Z], RefMemHP, RefCoefHP, out RefMemHP[In_Z]);    //Sig HPF2
                RefChan = RefMemHP[Out_Z];

                // -------------------- Ref Chan.  Delay timer for Phase Shift -------------------------------
                if ((Edge_Z == false) && (RefChan > 0))     // Found a positive edge
                {
                    Edge_Z = true;
                    PosEdgeCounter = 0;
                }
                if ((Edge_Z == true) && (RefChan < 0))      // Found a negative edge 
                {
                    Edge_Z = false;
                    NegEdgeCounter = 0;
                }
                if (PosEdgeCounter++ == PSD_Delay)
                    Edge2 = true;
                if (NegEdgeCounter++ == PSD_Delay)
                    Edge2 = false;

                // -------------------- 2nd Delay timer for more Phase Shift -------------------------------------------
                // This allows phase to reach 360 degrees +, even if external reference
                if ((Edge2_Z == false) && (Edge2 == true))     // Found a positive edge
                {
                    Edge2_Z = true;
                    PosEdgeCounter2 = 0;
                }
                if ((Edge2_Z == true) && (Edge2 == false))      // Found a negative edge 
                {
                    Edge2_Z = false;
                    NegEdgeCounter2 = 0;
                }
                if (PosEdgeCounter2++ == PSD_Delay2)
                    Sync_det_State = true;
                if (NegEdgeCounter2++ == PSD_Delay2)
                    Sync_det_State = false;

                // -------------------- PSD detector -------------------------------------------------
                double Ref_PD;
                if (Sync_det_State == true)
                {
                    Sync_Det_Out = SigChan;
                    Ref_PD = 1500.0;            // for setup viewing
                }
                else
                {
                    Sync_Det_Out = -SigChan;
                    Ref_PD = -1500.0;
                }
                // ------------------------- Output Low Pass Filter.  Nominal 2Hz for anti-aliasing 10Hz SR.
                OutMemLPA[Out_Z] = DoIIR(Sync_Det_Out, OutMemLPA, OutCoefLP, out OutMemLPA[In_Z]);    //Out LPF1
                OutMemLPB[Out_Z] = DoIIR(OutMemLPA[Out_Z], OutMemLPB, OutCoefLP, out OutMemLPB[In_Z]);

                Det_Filt_Out = OutMemLPB[Out_Z];

                TestSig1 = SigChan;                 // Normal testpoint signal
                //TestSig1 = right;                   //# testpoint

                TestSig2 = Ref_PD;                  // normal testpoint PD Reference
                //TestSig2 = Sync_Det_Out;             //# testpoint


                //  ---------------------- put small sample of raw sample rate ADC data to file for diagnostic use -----------------------------------------
                if ((callCount == callCountTrig) && (i < ADC_testsamp))         // callCount allows settling of ADC hardware. 0.1 sec / count
                {
                    Program.ADCTestFile.Write(left.ToString() + ",");
                    Program.ADCTestFile.Write(TestSig1.ToString() + ",");
                    Program.ADCTestFile.Write(TestSig2.ToString());
                    Program.ADCTestFile.WriteLine();
                }
                RMS_sum += InputSig * InputSig;


            }  // ****************************** End of sample rate process **********************

            // following process is at callback rate of once per 100mSec or 10Hz  

            OutMemLP_LSR[Out_Z] = DoIIR(Det_Filt_Out, OutMemLP_LSR, OutCoefLP_LSR, out OutMemLP_LSR[In_Z]);    //Out LPF1
            LockIn_Out = OutMemLP_LSR[Out_Z] * Setup.SigOut.Scale;

            DateTime CurrTime = DateTime.Now;

            RMS_sum /= bruce.BytesRecorded / 4;
            RMS = Math.Sqrt(RMS_sum);               // calc RMS of block.  Use for rec calibrate.
                                                    // int mod = callCount % 8;
                                                    // if ( mod == 0)
                                                    // {
                                                    //    Console.Write("* ");
                                                    //    Console.WriteLine(RMS.ToString("F2"));
                                                    // }

            /* ***********************************************************************************
            * We are still in the once every 100mSec here.
            *************************************************************************************/
            DateTime dateDate = DateTime.Now;       // check for timing of File Output
            int timesec = dateDate.Second;

            dateDate = DateTime.Now;
            timesec = dateDate.Second;

            if (timesec != Z_Sec)                               // find seconds boundry for screen and file update ***
            {
                string cString;                                 // String has all console output data
                cString = Convert.ToString(timesec) + " " + RMS.ToString("F2") +" " + LockIn_Out.ToString("F2");

                passCount++;
                if (passCount >= secondsPer)                                    // Data sent to File and UDS
                {                                                               // Data to output file  and TCP socket in UDS PUSH  mode**
                    Program.DataOutFile.Write(OutIndex.ToString() + ", ");
                    Program.DataOutFile.Write("{0:G}", CurrTime + ",  ");       // Short date/long time mm/dd/yyyy hh:mm:ss
                    Program.DataOutFile.WriteLine(LockIn_Out.ToString("F2"));   // ***This is data to output over  TCP Also.
                    Program.DataOutFile.Flush();
                    cString += " csv";
                    passCount = 0;
                    OutIndex++;
                    if ((! UDS_process.PollFlag) && (UDS_process.UDS_Sta >= UDS_process.UDS_States.ready))   // YES check flags HERE
                    {
                        UDS_process.PushSendNow = true;                         // Tell UDS to send PUSHED data.
                        cString += " RSP";
                    }
                }
                if (Do_Screen) Console.WriteLine(cString);

 //               Console.WriteLine( "dumb = "+ UDS_process.dumb_counter +UDS_process.PollFlag+UDS_process.UDS_Sta + UDS_process.PushSendNow);     // trouble shooting only ####
            }
            Z_Sec = timesec;
        }

    }
}
