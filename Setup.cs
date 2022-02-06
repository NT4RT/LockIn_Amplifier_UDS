/************************************************************************************************* 
 * By Bruce Randall  NT4RT      January 21, 2013.  Dec 09, 2021. Dec 12, 2021. Dec 25,2021.
 * Reads in data from LI_config.xml file.
 * From this data, filter coefficients and other DSP stuff is calculated.
 * 
 * ****  XML File <xml_sho value="false"/> is set to true to sho xml contnts on console screen at startup ****
 * 
**************************************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using UDS_Server;
 // using Lockin_Amplifier1;
 // using SquareWave_Out;

namespace Setup
{
    public class ADC_Set            // Overwriten before use by setup XML file.
    {
        public static int SampleRate = 48000;     // Sound board setting
    }

    public class RefGen           // Overwriten before use by setup XML file.
    {
        public static double Freq = 520.0;      // Square Wave frequency in Hz. 
        public static double Slew = 0.1;        // part of wave used in slewing.
        public static float Level = 0.05f;      // 1.0 = full scale.
        public static int Samp_Half_Cycle;      // Calculated from RefGen.Freq & ADC_Set.SampleRate.
        public static int Samp_Slew;            // Calculated from Slew.
    }
    public class RefIn           // Overwriten before use by setup XML file.
    {
        public static double HPF =   50.0;     // High pass corner in Hz.
        public static double LPF = 5000.0;    // Low pass corner in Hz.
        public static double Phase = 45.0;    // Phase delay in degrees ...... not complete yet ###
        public static double XFreq = 520.0;    // Freq for Phase Calc. if 0.0 use Ref Gen
    }
    public class SigIn           // Overwriten before use by setup XML file.
    {
        public static double HPF =  20.0;           // High pass corner in Hz.
        public static double LPF = 10000.0;         // Low pass corner in Hz.
        public static double LP_Comp = 584.0;       // Compensat Analog Low pass. Corner in Hz.
        public static double LP_Comp_Bk = 5000.0;   // Compensat Analog Low pass. Break in Hz.
        public static double HP_Comp = 541.0;       // Compensat Analog Hi pass. Corner in Hz.
        public static double HP_Comp_Bk = 10.0;     // Compensat Analog Hi pass. Break in Hz.
    }
    public class SigOut                             // Overwriten before use by setup XML file.
    {
        public static double LPF = 0.1;             // Low pass corner in Hz.      
        public static double Scale = 5.0;           // Multiply output by this for user output
    }
    public class Clock           // Overwriten before use by setup XML file.
    {
        public static int timing = 5;       // Number of 1 second ticks between out samples
    }
    public class OutFile           // Overwriten before use by setup XML file.
    {
        public static bool Header = true;    // true adds Measurement Computing header to .csv file
    }
    public class UDS_Port
    { 
        public static int port_address = 5900; // ### new value added for UDS.  XML file will overwrite.
    }  

    // ****** Code to Read XML file, generate filter coeficients & install them in signal processing section. **************
    public class Startup
    {
        public static void XML_Setup()      // gets setup parameters from XML file
        {                                   // put in setups above.     
            bool ShoXML = false;            // true to sho XML read progress on console. OVERWRITTEN BY XML FILE ENTRY !!!!!
            string STR;                     //  general use
            bool B1, B2, B3;

            XmlReader SetupFile;
 //           Console.WriteLine(" reading .XML file.");  // no need to.  we display version below

            // Get settings from XML filetemp

            SetupFile = XmlReader.Create("LI_config.xml");

            SetupFile.ReadToFollowing("xml_ver");
            SetupFile.MoveToAttribute("value");
            STR = SetupFile.ReadContentAsString();
            Console.WriteLine();
            Console.WriteLine(STR);                         // Always put file version on the screee
            Console.WriteLine();

            SetupFile.ReadToFollowing("xml_sho");
            SetupFile.MoveToAttribute("value");
            ShoXML = SetupFile.ReadContentAsBoolean();

            SetupFile.ReadToFollowing("ADC_Set");
            SetupFile.ReadToDescendant("SampRate");
            SetupFile.MoveToAttribute("value");
            ADC_Set.SampleRate = SetupFile.ReadContentAsInt();
            if (ShoXML) Console.Write("Sample Rate ={0}", ADC_Set.SampleRate);
            if (ShoXML) Console.WriteLine();

            SetupFile.ReadToFollowing("RefGen");
            SetupFile.ReadToDescendant("Freq");
            SetupFile.MoveToAttribute("value");
            RefGen.Freq = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.Write("RefGen =");
            if (ShoXML) Console.WriteLine(RefGen.Freq);

            SetupFile.ReadToNextSibling("Slew");
            SetupFile.MoveToAttribute("value");
            RefGen.Slew = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(RefGen.Slew);

            SetupFile.ReadToNextSibling("Level");
            SetupFile.MoveToAttribute("value");
            RefGen.Level = SetupFile.ReadContentAsFloat();
            if (ShoXML) Console.WriteLine(RefGen.Level);

            if (ShoXML) Console.WriteLine("RefIn ---------------");
            SetupFile.ReadToFollowing("RefIn");
            SetupFile.ReadToDescendant("HPF");
            SetupFile.MoveToAttribute("value");
            RefIn.HPF = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(RefIn.HPF);

            SetupFile.ReadToNextSibling("LPF");
            SetupFile.MoveToAttribute("value");
            RefIn.LPF = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(RefIn.LPF);

            SetupFile.ReadToNextSibling("Phase");
            SetupFile.MoveToAttribute("value");
            RefIn.Phase = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(RefIn.Phase);

            SetupFile.ReadToNextSibling("XFreq");
            SetupFile.MoveToAttribute("value");
            RefIn.XFreq = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(RefIn.XFreq);

            if (ShoXML) Console.WriteLine("SigIn ---------------");
            SetupFile.ReadToFollowing("SigIn");
            SetupFile.ReadToDescendant("HPF");
            SetupFile.MoveToAttribute("value");
            SigIn.HPF = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.HPF);

            SetupFile.ReadToNextSibling("LPF");
            SetupFile.MoveToAttribute("value");
            SigIn.LPF = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.LPF);

            SetupFile.ReadToNextSibling("LP_Comp");
            SetupFile.MoveToAttribute("value");
            SigIn.LP_Comp = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.LP_Comp);

            SetupFile.ReadToNextSibling("LP_Comp_BK");
            SetupFile.MoveToAttribute("value");
            SigIn.LP_Comp_Bk = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.LP_Comp_Bk);

            SetupFile.ReadToNextSibling("HP_Comp");
            SetupFile.MoveToAttribute("value"); 
            SigIn.HP_Comp = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.HP_Comp);

            SetupFile.ReadToNextSibling("HP_Comp_BK");
            SetupFile.MoveToAttribute("value");
            SigIn.HP_Comp_Bk = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigIn.HP_Comp_Bk);

            if (ShoXML) Console.WriteLine("SigOut ---------------");
            SetupFile.ReadToFollowing("SigOut");

            SetupFile.ReadToDescendant("LPF");
            SetupFile.MoveToAttribute("value");
            SigOut.LPF = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigOut.LPF);

            SetupFile.ReadToNextSibling("Scale");
            SetupFile.MoveToAttribute("value");
            SigOut.Scale = SetupFile.ReadContentAsDouble();
            if (ShoXML) Console.WriteLine(SigOut.Scale);
            
            SetupFile.ReadToFollowing("Clock");
            SetupFile.ReadToDescendant("timing");
            SetupFile.MoveToAttribute("value");                         // seconds between updates of output
            Clock.timing = SetupFile.ReadContentAsInt();
            if (ShoXML)  {
                Console.WriteLine("Clock ---------------");             // output timing control
                Console.WriteLine(Clock.timing);
            };

            B1 = SetupFile.ReadToFollowing("OutFile");              
            B2 = SetupFile.ReadToDescendant("Header");
            B3 = SetupFile.MoveToAttribute("value");
            // Console.Write(B1); Console.Write(B2); Console.WriteLine(B3);    //test code 
            OutFile.Header = SetupFile.ReadContentAsBoolean();
            Console.WriteLine("OutFile MC header = {0}", OutFile.Header);   // true/false for header present

            B1 = SetupFile.ReadToFollowing("UDS_Port");              
            B2 = SetupFile.ReadToDescendant("IP_Address");
            B3 = SetupFile.MoveToAttribute("value");
            //  Console.WriteLine("IPA="+B1+B2+B3);                         // Test Code
            UDS_process.IPAdrText = SetupFile.ReadContentAsString();
            //    Console.WriteLine(" IP_addr    {0}   read XML port address next ------------------", UDS_process.IPAdrText);
            B2 = SetupFile.ReadToNextSibling("port_Number");
            B3 = SetupFile.MoveToAttribute("value");
            UDS_process.portNumber = SetupFile.ReadContentAsInt();

            if (ShoXML) {               // ShoXML or true here
                Console.WriteLine("UDS_IP_Adr {0}", UDS_process.IPAdrText);         // Text for IP address
                Console.WriteLine("UDS_port   {0}", UDS_process.portNumber);        // integer port number
            };

            SetupFile.Close();
            } 
            // ******************************************* end of setup file read ************************************

            public static void SetupPrep()  // calcs coeficients from input frequency data
        {
            Console.WriteLine(" preparing setup parameters.");
            double SRate = Setup.ADC_Set.SampleRate;

            LPF_Coef_Calc(SigIn.LPF, SRate, out SignalWave_In.process.CoefLP1);
            HPF_Coef_Calc(SigIn.HPF, SRate, out SignalWave_In.process.CoefHP1);

            Shelf_Coef_Calc(SigIn.LP_Comp_Bk, Setup.SigIn.LP_Comp, SRate, out SignalWave_In.process.CoefLP_Comp);
            Shelf_Coef_Calc(SigIn.HP_Comp_Bk, Setup.SigIn.HP_Comp, SRate, out SignalWave_In.process.CoefHP_Comp);

            LPF_Coef_Calc(RefIn.LPF, SRate, out SignalWave_In.process.RefCoefLP);
            HPF_Coef_Calc(RefIn.HPF, SRate, out SignalWave_In.process.RefCoefHP);
 
            // following filter is done at the 10Hz block rate sample rate
            LPF_Coef_Calc(SigOut.LPF, 10.0, out SignalWave_In.process.OutCoefLP_LSR);

            // Setup output sample Rate.  Units is seconds between samples
            SignalWave_In.process.secondsPer = Clock.timing; 

            // Calc Samples per setup for square wave gen
            RefGen.Samp_Half_Cycle = (Int16)Math.Round(ADC_Set.SampleRate / RefGen.Freq / 2); // must be integer
            RefGen.Samp_Slew = (Int16)Math.Round(RefGen.Slew * RefGen.Samp_Half_Cycle);
            
            // Calculate delay timers for phase adjustment here.
            double Adj_Phase;
            double Sel_Freq;
            double Exact_Freq = SRate / (RefGen.Samp_Half_Cycle * 2.0);      //actual freq we run it after 
            if (RefIn.XFreq == 0.0)
                Sel_Freq = Exact_Freq;
            else Sel_Freq = RefIn.XFreq;
            Adj_Phase = RefIn.Phase;
            if (Adj_Phase < 0.0) Adj_Phase += 360.0;
            if (Adj_Phase > 360.0) Adj_Phase -= 360.0;
            int total_SR_Dly = (int)(Math.Round(Adj_Phase / 360.0 * SRate / Sel_Freq));

            SignalWave_In.process.PSD_Delay = total_SR_Dly / 2;
            SignalWave_In.process.PSD_Delay2 = total_SR_Dly - SignalWave_In.process.PSD_Delay;
            //Console.WriteLine("PSD");   // ###
            //Console.WriteLine(Exact_Freq);
            //Console.WriteLine(Adj_Phase);
            //Console.WriteLine(SignalWave_In.process.PSD_Delay);
            //Console.WriteLine(SignalWave_In.process.PSD_Delay2);
        }

        static void LPF_Coef_Calc(double FP, double SampFr, out double[] coef)
        {
            coef = new double[3];
            coef[2] = Math.Exp(-2 * Math.PI * FP / SampFr); //A2
            coef[1] = (1.0 - coef[2]) / 2;                  //B2
            coef[0] = coef[1];                              //B1
        }

        static void HPF_Coef_Calc(double FP, double SampFr, out double[] coef)
        {
            coef = new double[3];
            coef[2] = Math.Exp(-2 * Math.PI * FP / SampFr); //A2
            coef[0] = 1.0 - (1.0 - coef[2]) / 2;            //B1
            coef[1] = -1 * coef[0];                         //B2
        }

        // This is used to compensate for anolog poles in radio frequency responce.
        // FZ is placed at the analog -pole frequency to cancil it.
        // FP will make a break in the compensation to avoid instability or too much noise gain.
        //      Low Pass analog pole:   Place FP higher than the analog pole by 3 to 10x.
        //                              Gain needs to be normallized using double Norm.
        //      High Pass analog pole:  Place FP lower than the analog pole by 3 to 10x.
        //                              Gain does NOT need to be nomalized.  Use 1.0.
        // In Code if FP > FZ, normalize is enabled and B1 and B2 are scaled.
        static void Shelf_Coef_Calc(double FP, double FZ, double SampFr, out double[] coef)
        {
            coef = new double[3];
            double A2 = Math.Exp(-2 * Math.PI * FP / SampFr); 
            double B1 = -1.0;
            double B2 = Math.Exp(-2 * Math.PI * FZ / SampFr);
            double Norm = (1 - A2) / (B1 + B2);
            if (FP < FZ) Norm = -1.0;
            coef[0] = B1 * Norm;
            coef[1] = B2 * Norm;
            coef[2] = A2;
        }

    }
}
 