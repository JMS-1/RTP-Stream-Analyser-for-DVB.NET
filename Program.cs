using System;
using System.Net;
using System.Net.Sockets;
using JMS.DVB;
using JMS.DVB.TS;


namespace TransportStreamSample
{
    /// <summary>
    /// Repräsentiert den Windows Prozess als Ganzes.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Alle einmaligen Aktualisierungen.
        /// </summary>
        static Program()
        {
            // Load DVB.NET runtime from installation directory
            RunTimeLoader.Startup();
        }

        /// <summary>
        /// Startet die Anwendung. Dieses Besipiel liest eine TS Datei ein.
        /// </summary>
        /// <param name="args">Der Empfangsport und der Name der TS Datei.</param>
        public static void Main( string[] args )
        {
            // Be safe
            try
            {
                // Create socket to receive RTP stream
                using (var analyser = new RtpTransportStreamAnalyser())
                using (var file = new DoubleBufferedFile( args[1], 1000000 ))
                using (var socket = new Socket( AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp ) { Blocking = true })
                {
                    // Configure
                    socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 10000000 );

                    // Bind it
                    socket.Bind( new IPEndPoint( IPAddress.IPv6Any, ushort.Parse( args[0] ) ) );

                    // Buffer to use
                    var buffer = new byte[10000];

                    // First send to file
                    Action<byte[], int, int> sink = file.Write;

                    // Then to analyse
                    sink += analyser.Feed;

                    // Result processor
                    AsyncCallback whenDone = null;

                    // Starter
                    Action asyncRead = () => socket.BeginReceive( buffer, 0, buffer.Length, SocketFlags.None, whenDone, null );

                    // Define result processor
                    whenDone =
                        result =>
                        {
                            // Be safe
                            try
                            {
                                // Finish
                                var bytes = socket.EndReceive( result );

                                // Try to dispatch
                                RtpPacketDispatcher.DispatchTSPayload( buffer, 0, bytes, sink );

                                // Fire next
                                asyncRead();
                            }
                            catch (ObjectDisposedException)
                            {
                            }
                        };

                    // Process
                    asyncRead();

                    // Wait for termination
                    Console.WriteLine( "Press ENTER to End" );
                    Console.ReadLine();

                    // Terminate connection
                    socket.Close();
                }
            }
            catch (Exception e)
            {
                // Report
                Console.WriteLine( "Error: {0}", e.Message );
            }

            // Done
            Console.ReadLine();
        }
    }
}