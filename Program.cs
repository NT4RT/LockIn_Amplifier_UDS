/*************************************************************************************************
 *  
 *              A Software Lock-In Amplifier.           By Bruce Randall  NT4RT
 *                              Email:  NT4RT@ARRL.net  WD4JQV@gmail.com.
 *              January 21, 2013.  May 9, 2013.  Original 
 *              January 11, 2022.  Added Radio SkyPipe UDS interface via TCP/IP Sockets.
 *      
 *      This program and the C# source files are considered public domain by the author.  It may
 *      be  modified, shared and used with no permissions from me.  It is requested that any 
 *      improvements by others be kept in the public domain.
 *      
 *      The C# compiler used is part of Microsoft Visual Studio (tm). It is available from
 *          https://visualstudio.microsoft.com/downloads/ 
 *      at no charge for non-commercial use.
 *      
 *      This uses the NAudio drivers for a computer soundboard as the A/D and D/A converters.  The
 *      versions 1.6 available in 2013 work fine.  The latest NAudio version is not compatable.
 *      Get this from:  
 *          https://github.com/naudio/NAudio and
 *          https://markheath.net/post/introducing-naudio-net-audio-toolkit
 *      
 *      WARNINGS:  This code was written by an individual with 4 bit microcomputer assembly 
 *      language and extensive FORTRAN programming in the early 1980's.
 *      It is a FORTRAN program written in C# and shows it.  Where are the .NE. and .EQ. ??
 *      
 *      Note that this file has different namespace and file name.cs.  Others are correct.  OOPS!
 *      
 *      Windows OS sounds should be turne OFF so they don't mix with our signals!
 *      
 * -----------------------------------------------------------------------------------------------     
 *  Program Inputs and outputs.
 *      Inputs:
 *          Console application. Keyboard.
 *          LI_Config.xml setup file.
 *          Lock-in Reference channel via sound board Right Chaannel Line In.
 *          Lock-in Signal channel via sound board Left Channel Line In.
 *      Outputs
 *          Console application.  Screen.
 *          Lock-in reference signal via sound board
 *              In Phase        Sound board Right Channel Line Out.
 *              Inverted Phase  Sound board Left Channel Line Out.
 *          User_Named.CSV file
 *          UDS interface to Radio SkyPipe
 * 
 *  This program impliments a "Lock-In Amplifier" for use with a phase switched interferometer 
 *  or Dicke switch radio telescope.  It is described on pages 5-1 through 5-13 of the 2013 SARA 
 *  conference proceeding.       
 *      
 * May 8, 2013 eliminate squarewave test file.  1 Sec update of display. * at file write.
 *
 * Nov 8 2021, Dec 22, 2021.  Start adding hooks for RSP (Radio Sky Pipe) UDS.  See UDS_Server.cs file
 *      The UDS in this program, acts as a "SERVER".  RSP will act as a "CLIENT" that requests connection and data.
 *      RSP generates all time stamps.  We only send data on every data available or request after RSP start established.
 *      Must respond to "INIT", PUSH", GETC" and "STAT" commands from RSP.
 *      Must send "^^1001", "^^2013n", "^^3001"and "#" with data commands to RSP.
 *      We must estblish a "Socket' connection for RSP to use.
 *      
 * Jan 7, 2022.  This version with the Radio SkyPipe UDS via TCP is working. ^ at UDS transfer.
 *      LI_config.xml must be version with new TCP IP parameters included.
 * 
**************************************************************************************************/
using System;                       // these namespaces can NOT contain common class names. 
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NAudio.Wave;
using System.IO;
using SquareWave_Out;
using SignalWave_In;
using UDS_Server;
using Setup;

namespace Lockin_Amplifier1         // Note that this DOES NOT match the file name per tradition !!!
{
    class Program
    {
        public static StreamWriter DataOutFile;          // data file for usage
        public static StreamWriter ADCTestFile;          // holds sample of ADC wave for testing use ( keep active! )
        // public static StreamWriter SQWTestFile;        // holds small bit of waves for testing use

// *********************** Start of Main Programme ***********************************************
        static void Main(string[] args)         // args could have outfile name??? not used now. 
        {
            Console.WriteLine("  Lock-In Amplifier for Radio Telescope   ...  With Radio Skypipe Interface");
            Console.WriteLine("             Bruce Randall  NT4RT  Version 2022_Mar_22 ");  
            Console.WriteLine();
            Console.WriteLine(" WARNING: Disconnect Sound Speakers before proceeding!!!!!!!");
            Console.WriteLine();
 //           Console.WriteLine(args);            // args currently not used

            bool KeyYes = false;                // true if OK to overwrite file
            bool FileOK = false;                // true if file is new file name
            string MyDataFileName = "";         // .CSV File for output data


            while (!(KeyYes || FileOK))                     // **** Get file name from User ******
            {
//                testkey = false;                          // wait to exit
                ConsoleKeyInfo KeyResult;
                Console.Write("Enter .CSV Data Output Filename.  Will add .CSV on end: ");   // we will force output file name to be a .csv (2021 0ct 31)
                MyDataFileName = Console.ReadLine();         // Get file name string from user
                if (! ((MyDataFileName.Contains(".csv")) ^ (MyDataFileName.Contains(".CSV"))))
                {
                    MyDataFileName = MyDataFileName + ".csv";
                }
                Console.Write(" File Name Is: ");           // This needs 2 lines???  Write then WriteLine 
                Console.WriteLine(MyDataFileName);
                if (File.Exists(MyDataFileName))
                {
                    Console.Write(" File Exists.  OverWrite (Y/N) ");
                    FileOK = false;
                    KeyResult = Console.ReadKey();
                    Console.WriteLine();
                    if (((KeyResult.KeyChar) == 'Y') || ((KeyResult.KeyChar) == 'y'))
                        KeyYes = true;
                }
                else FileOK = true;                         // file name not used before
            }                                               // **** End of  Get file name from User ***********************

            Setup.Startup.XML_Setup();                      // Get setup data from .XML file 
            Setup.Startup.SetupPrep();                      // Calcs IIR filter Coef & other stuff from setup data 
            Console.WriteLine(" Setup finished");
            UDS_process.InitServer();                       // Initialize Socket for RSP UDS.  Must be after XML_Setup.

            DataOutFile = new StreamWriter(MyDataFileName);
            ADCTestFile = new StreamWriter("ADCTestFile.CSV");
            ADCTestFile.WriteLine("Rec Sig, SigChan, PSD Ref, ");
            //SQWTestFile = new StreamWriter("SqWavTestFile.CSV");  //used in early testing
            //SQWTestFile.WriteLine("Count,Wave");

            DateTime CurrTime = DateTime.Now;
            // Console.WriteLine("{0:G}", CurrTime);       // Short date/long time mm/dd/yyyy hh:mm:ss

            // ------  Header for output file -----------------------------------------------------
            if (Setup.OutFile.Header)                   // If true add header lines to output file
            {
                string line;
                System.IO.StreamReader MyInFile =
                    new System.IO.StreamReader("Header.txt");
                while ((line = MyInFile.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                    DataOutFile.WriteLine(line);
                }
            }
            else DataOutFile.WriteLine("Index, Date/Time, Current Value");


// ------ Code to make square wave output happen            
            SquareWaveProvider32 squareWaveProvider = new SquareWaveProvider32();
            squareWaveProvider.SetWaveFormat(Setup.ADC_Set.SampleRate, 2);      // Stereo for 2 pahse square wave out
            squareWaveProvider.SampHalfCycle = Setup.RefGen.Samp_Half_Cycle;    // must be integer
            squareWaveProvider.SampSlew = Setup.RefGen.Samp_Slew;               // must be integer
            squareWaveProvider.Amplitude = Setup.RefGen.Level;                  // 32 bit float.  NOT a double!!

            WaveOut waveOut = new WaveOut();
            waveOut.Init(squareWaveProvider);
            waveOut.Play();

// ------ Code to take in data from sound board --------------------------------------------------
            NAudio.Wave.WaveInEvent wi = new WaveInEvent();
            wi.WaveFormat = new WaveFormat(Setup.ADC_Set.SampleRate, 2);
            wi.DataAvailable += new EventHandler<WaveInEventArgs>(process.wi_DataAvailable);
            wi.BufferMilliseconds = 100;
            Console.WriteLine("StartRecording");
            wi.StartRecording();
// ---------- End of startup process ------------------------------------------------------------
            
/* ***********************************************************************************************
 * 
 *     All Measurement, DSP, Recording and Communications is run on "Callback" Interupts.  ------
 *     This below is a "Do Forecver" loop.  broken only by user <Escape> key or a crash.
 * 
 ************************************************************************************************/
            Console.WriteLine("---- Hold down the 'escape' key to exit. ---- ");
            int delay = 10;                         // in mSec.  

            while (true)    // *** This is "DO FOREVER"   loop.  All other stuff done in CallBack. 
                            // All data processing and recording is done in 100mS callback))
            {
                if (Console.KeyAvailable)
                { 
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) 
                        break; 
                }
                UDS_process.UDS_main();     // UDS processing slid in here
                Thread.Sleep(delay);        // Delay in main loop
            }

// ---------------------  Start of ShutDown code -------------------------------------------------
            Console.WriteLine("StopRecording");     // start process to close program

            wi.StopRecording();
            waveOut.Stop();
            waveOut.Dispose();
            waveOut = null;

            DataOutFile.Flush();        // start process to close program
            DataOutFile.Close();
            UDS_process.StopServer();   // stop UDS processs

            Console.WriteLine("     The END !!! ");
            Console.WriteLine("---- <CR> to stop ----");
            Console.ReadLine();         // hold until <return> key so we can read screen.
             // ADCTestFile.Flush();    // Flushing and close in namespace SignalWave_In  !!!
             // ADCTestFile.Close();

             //SQWTestFile.Flush();     // uncomment when file needed for testing only.
             //SQWTestFile.Close();
        }
    }
}
// ****************************************************************************************************************************
