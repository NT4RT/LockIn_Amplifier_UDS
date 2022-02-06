/*************************************************************************************************
 * Contains code to handle RSP (Radio Skypipe) data transfer via UDS (User Data Source).      
 *      NT4RT  Bruce Randall  January 9, 2022.
 *      Email:  NT4RT@ARRL.net  WD4JQV@gmail.com.
 * This is setup as a SERVER with RSP program acting as a CLIENT.  Specifically for RSP UDS.
 * 
 * ******* This is NOT generic code. It gets data directly from namespace SignalWaveIn.  *******
 * 
 * It uses the Socket Class in the Microsoft .net for communications to Radio SkyPipe (RSP)
 * A state machine establishes communications per RSP specification.
 * 
**************************************************************************************************/
// #define Soc_Debug                // turns debug messages on for socket comm.
using System;                       // these can NOT contain common class names !!!
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;             // allow delay functions
using Setup;                        // Allows UDS IP address and port to be in XML File
using SignalWave_In;                // This is where we get data to send to UDS.

namespace UDS_Server
{
   public static class UDS_process                  // All in one class here
    {
        public static string IPAdrText = "127.0.0.1";  // loopback IP. UDS & RSP on same computer.
        public static int portNumber = 3456;           // Adr & Port overwriten by LI_config.xml
        static string UDS_responce = null;
        public static bool PollFlag = true;         // flag set for POLL mode, reset for we PUSH data mode
        public static bool PushSendNow = false;     // Set in SignalWave_In.  Data sent and flag reset in UDS_Server.
        public static bool I_got_caught = false;    // used in try - catch.  Set false before every try
        public static bool Soc_Rx_Dat = false;

        public enum UDS_States      // includes socket start up states.
        { soc_off,                  // nothing 
            soc_init,               // Socket has been set up.
            soc_assign,             // Socket has IP address
            soc_client,             // Scoket has a client connected
            INIT,                   // Received INIT from RSP & sent OK code
            STAT,                   // Received STAT from RSP & sent OK code
            GETC,                   // Received GETC from RSP & send channel count  
            ready                   // ready for UDS communications to RSP
        }
        public static UDS_States UDS_Sta = UDS_States.soc_off;

        public static Socket listener;
        public static Socket clientSocket;
        static string recStringData = null;     // receive data shared by many functions
        static int  UDS_send_err_count = 0;     // Counter for threshold

        /*****************************************************************************************
         *  public static void InitServer()
         *  Entry point  for socket initialization.  Called at startup of main programme.
         *  
         *  Warning: Uses Socket.Blocking = false to prevent hangups.  
         *  Must accomodate failed socket operations with try-catch
         ****************************************************************************************/
        public static void InitServer()
        {
            // Establish the local endpoint for the socket. 
            // Dns.GetHostName returns the name of the host running the application.
            StopServer();           // just in case server was running from a prior crash !!!!!!!!!!
                                    // 
                                    // IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                                    // IPAddress ipAddr = ipHost.AddressList[0];

            IPAddress ipAddr = IPAddress.Parse(IPAdrText);
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, portNumber);
#if Soc_Debug
            Console.WriteLine(localEndPoint);
            Console.WriteLine("Address: " + ipAddr.ToString());
#endif            
            // Creation TCP/IP Socket using Socket Class Constructor
            // 
            // clientSocket.Blocking = false;   // IMPORTANT:  This prevents socket operations from hanging up
            // We MUST handle exceptions try-catch on send, rcv, accept, & close of socket

            listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);    //
            listener.Blocking = false;
            I_got_caught = false;               // Set false before try
            try
            {   // Using Bind() method we associate a network address to the Server Socket.
                // All client that will connect to this Server Socket must know this network Address.

                listener.Bind(localEndPoint);

                // Using Listen() method we create the Client list that will want to connect to Server.

                listener.Listen(1);             // Number of clients allowed.  RSP access must be 1
            }
            catch (Exception e)
            {
                I_got_caught = true;
                Console.WriteLine(e.ToString());
                Console.WriteLine("Socket connect failed");
            }
            if (! I_got_caught)                 // sort of like a else for try-catch
            {
                UDS_Sta = UDS_States.soc_init;      // Updat state machine
                Console.WriteLine("UDS Socket Init Done OK.");
            }
            I_got_caught = false;
        }

        /***************************************************************************************** 
         *      public static void UDS_main()
         *   In Main program loop the process below is called OVER & OVER again. 
         *   This function handle all UDS activity except Initialization and ShutDown.
         *   This CAN NOT BE a do forever type loop itself !!!------------------------------------
         *   
         ****************************************************************************************/
        public static void UDS_main()
        {
            if (UDS_Sta >= UDS_States.soc_init)   // First see if Init Socket is complete
            {
                StartServer();
            }

            if ((UDS_Sta >= UDS_States.soc_client) && (Soc_Rx_Dat))
            {
#if Soc_Debug
                Console.WriteLine(" ------------------------------------");  
                Console.WriteLine(" Ready to Get_Do_Commands. Global buffer = {0}", recStringData);
#endif
                Get_Do_Commands(recStringData);    //  Get_Do_Commands needs a string input.

            }

            if (PushSendNow && (UDS_Sta >= UDS_States.soc_client))    
            {
#if Soc_Debug             
                Console.WriteLine("_--------------------------------^");
                Console.WriteLine("PUSH data now"); 
#endif
                PushSendNow = false;
                UDS_SendData();                // gets data from measurement process & send to RSP
            }
        }

        /*****************************************************************************************
         *  This does a lot of the processing to set up connection and get receive string 
         *  to get and process for commands for later.
         *  
         *  We don't get here unless Server Initialization worked OK.
         ****************************************************************************************/
        public static void StartServer()       // this is not called if socket init is not OK  
        {
 //           string recStringData = null;
            try
            {
                //      Using Accept() method the server will accept connection of client.
                byte[] buffer = new Byte[512];           // Data buffer, local for socket receive. 


                if (UDS_Sta == UDS_States.soc_init)     // only ACCEPT from init state
                {
                    // -----------start of try-catch-else --------------------------------------------
                    I_got_caught = false;                   // Set false before try
                    try
                    {
                        clientSocket = listener.Accept();   // Socket for a new connection.
                        clientSocket.Blocking = false;      //  not sure if this is needed.
                    }
                    catch
                    {
                        I_got_caught = true;
                        //    Console.WriteLine( "Accept failure");         // this message is bad idea
                        //                   UDS_Sta = UDS_States.soc_assign;
                    }
                    if (! I_got_caught)     // sort of like a else for try-catch
                    {
                        UDS_Sta = UDS_States.soc_client;           // ***  Client is now connected ***
                        Console.WriteLine(" Client Accepted");                    // keep this message
                    }
                    I_got_caught = false;
                    // ----------- end of try-catch-else ---------------------------------------------
                    //                    UDS_Sta = UDS_States.soc_client;
                }   // end of if statement

                // -------------------------------------------------------------------------------
                // Now read data from client
                if (UDS_Sta >= UDS_States.soc_client)                 // Assure we have a client!!      
                {
                    int numByte = 0;
                    I_got_caught = false;
                    try
                    {
                        numByte = clientSocket.Receive(buffer);     // this fails if no data now.
                    }
                    catch
                    {
                        I_got_caught = true;
                        //    Console.WriteLine("receive failure");
                    }
                    I_got_caught = false;

                    recStringData = Encoding.ASCII.GetString(buffer, 0, numByte);
                    //   WARNING:  ASCII is 7 bit so 0xff at end is turned into a 0x3f or ASCII "?"
                    //   UTF8 Encoding not used here to avoid possible change to multi byte characters 

                    if (numByte > 0)    Soc_Rx_Dat = true;
                    else Soc_Rx_Dat = false;

                }   // end of if (UDS_Sta >= UDS_States.soc_client)
            }       // end of Try
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }       // end of catch
        }           // End of StartServer()

        /*********************************************************************************************
        * Get_Do_Commands() finds up to MsgCount commands in buffer data from client. 
        * and processes them.  Code hacked up from C++ example from RadioSky. 
        *********************************************************************************************/
        public static void Get_Do_Commands(String MsgBuff)  //  Need to get a string from socket receive
        {
#if Soc_Debug
            Console.Write(" MsgBuff contents = ");
            Console.WriteLine(MsgBuff);                        // we get here
#endif
            const int MsgCountMax = 6;           // max number of messages stacked up.  Appears to be not needed???
            string[] Messages;
            Messages = new string[MsgCountMax] { "M0", "M1", "M2", "M3", "M4", "M5" };  // must manually adjust in {}

            String sR = " ";          // UDS responce string
            int MsgNum = 0;
            char TempS;         // used as test character
                                // process messages
                                // is there a message in the buffer from the client?
                                // parse message buffer to get individual commands
             for (int i = 0; i < MsgCountMax; i++) Messages[i] = "";     //clear all message strings
            int Len = MsgBuff.Length;                   // removed +1 from length in original C++ program
            char[] CharBuffer = new char[Len];

            // StrCopy(CharBuffer, MsgBuff.c_str()); original C++ line                                     
            MsgBuff.CopyTo(0, CharBuffer, 0, Len);  //   (SrcIndx, Dest,  DestIndx,  ChrCount ) //Copy char[] to string here *** 
#if Soc_Debug
            if (Len > 1) {      
                Console.Write( " Char Buff =");     // test code
                Console.WriteLine(CharBuffer);                                
            }
#endif
            // Skypipe commands separated by 0xFF.   Encoding.ASCII converts 0xFF to 0x3F or a "?" mark
            // See how many messages there are, and drop in buffers.  Process one at at time.

            for (int i = 0; i < Len; i++)
            {
                TempS = CharBuffer[i];
                if (TempS != '?') Messages[MsgNum] = Messages[MsgNum] + TempS;  // Add char to message
                else MsgNum++;                                                  // Next message
            }
            
            // now process all messages
            //
            for (int i = 0; i < MsgNum; i++)
            {
                Console.Write("-- Cmd Received = " + Messages[i] + ": Action = ");            

                switch (Messages[i])
                {
                    case "INIT":                    // command from Skypipe to initialize UDS
                        sR = "^^1001";               // tell RSP that UDS ready
                        SendString(sR);              
                        break;

                    case "POLL":
                        PollFlag = true;           // Set Poll flag for POLL mode.  No responce needed.
                        Console.Write("UDS POLL mode");
                        break;

                    case "PUSH":
                        PollFlag = false;           // Reset Poll flag for PUSH mode.  No responce needed.
                        Console.Write("UDS PUSH mode");
                        break;

                    case "GETC":
                        // Skypipe requesting the number of channels, 20131=1,  20132=2, 20133=3, etc.
                        sR = "^^20131";
                        SendString(sR);                      // adds chr<255> on end
                        break;

                    case "STAT":
                        sR = "^^1001";                       // means UDS ready
                        SendString(sR);                      
                        UDS_Sta = UDS_States.ready;         // allow data to be sent
                        break;

                    case "GETD":                // Skypipe is requesting data.  POLL
                        UDS_SendData();         // gets data from measurement process & send to RSP
                        break;

                    default: break;
                }   // end of switch (Messages[i])
                Console.WriteLine( sR );
            }       // end of for loop through all messages in buffer          
        }

        /* ***************************************************************************************
         *      public static void StopServer()
         * StopServer() disconnects UDS from the Sockets  
         *****************************************************************************************/
        public static void StopServer()                 // Stop server
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                listener.Shutdown(SocketShutdown.Both);
            }
            catch { }                                    // Prevent program hangup if UDS not in use

            try
            {
                clientSocket.Close();
                listener.Close();
            }
            catch { }                                    // Prevent program hangup if UDS not in use
 
            if (UDS_Sta >= UDS_States.soc_client) {      // Shutdown message only if UDS in use
                Console.WriteLine(" Socket close");
                Console.WriteLine("UDS Server Shutdown Done.");
            }
        }   // end of function StopServer

        /*****************************************************************************************
        *       static void SendString(string STR)  
        * This sends a text string back to Radio Sky Pipe.
        * RSP needs chr<255> on end of all sent to it.  UTF8 encodig failed & made a mess.
        * ASCII encoding is 7 bit.  Putting 0xFF on end of bytes after conversion worked. 
        *****************************************************************************************/
        static void SendString(string STR)
        {
            int nb = 0;                                     // nb Test Point for num bytes sent.
            STR = STR + "~";                                //  "~" placeholder for 0xFF
            byte[] bytes = Encoding.ASCII.GetBytes(STR);    // ASCII 7 bit fix 0xFF next.
            bytes[STR.Length-1]  = 0xFF;                    // Change ~ at end to 0xFF

            I_got_caught = false;
            try
            { 
                nb = clientSocket.Send(bytes);          // Actual send of data !!!
            }
            catch {                                     // This happens of RSP cahrt is stopped.
                I_got_caught = true;
                Console.WriteLine("clientSocket.Send error *************** ");      
                UDS_send_err_count++;
                if (UDS_send_err_count > 3) {
                    UDS_Sta = UDS_States.soc_init;      // This allows restart by RSP
                    UDS_send_err_count = 0;
                }
            if ( ! I_got_caught) {                      // data sent OK
                    UDS_send_err_count = 0;
                }
            }
            I_got_caught = false;

#if Soc_Debug                                   // test code to verify bytes sent
                                            
                Console.Write("SendString8 {0}  ", STR);   // verify 8 bit conversion.
                Console.WriteLine(" byte count = {0}", nb);

                Console.Write("Send Bytes =");
                for (int i = 0; i < STR.Length; i++) { 
                Console.Write(" " + bytes[i]);
                }   
                Console.WriteLine();
#endif
        }

        /****************************************************************************************
        *  static void UDS_SendData()
        * Sends lockin Amplifier Data using SendString() function.
        *****************************************************************************************/
        static void UDS_SendData()           // Uses SendString() to send data value
        {                                    // SendData Data "#01234 where 0 is channel ID and 1234 is data value. 
            UDS_responce = "#0" + process.LockIn_Out.ToString("F2");  //
            SendString(UDS_responce);
            UDS_responce = "^^3001";        // send time stamp code 3001 to time stamp data by RSP.
            SendString(UDS_responce);
        }
   }            // end of class UDS_process

}               // end of namespace UDS_Server

//  code samples for Socket programming
//   https://app.assembla.com/wiki/show/TheGame/Socket_Programming_in_CSharp is also of intersst
//   https://docs.microsoft.com/en-us/dotnet/framework/network-programming/asynchronous-server-socket-example
//   https://stackoverflow.com/questions/27539938/tcp-ip-client-socket-program-in-c-net-using-ip-address-and-port-number
