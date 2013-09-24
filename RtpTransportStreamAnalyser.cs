extern alias legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using JMS.DVB.SI;
using JMS.DVB.TS;
using JMS.DVB.TS.TSBuilders;
using oldSIAPI = legacy.JMS.DVB.EPG;
using oldTables = legacy.JMS.DVB.EPG.Tables;


namespace TransportStreamSample
{
    /// <summary>
    /// Die Komponente zur Analyse eines sat&gt;ip TS Datenstroms.
    /// </summary>
    public class RtpTransportStreamAnalyser : IDisposable
    {
        /// <summary>
        /// Erstellt eine neue Analyseeinheit.
        /// </summary>
        private TSParser m_parser = new TSParser { FillStatistics = true };

        /// <summary>
        /// Die Anzahl der Tabellen mit Daten zur Programmzeitschrift.
        /// </summary>
        private long m_numberOfGuideTables;

        /// <summary>
        /// Die Anzahl der Sendungsinformationen.
        /// </summary>
        private long m_numberOfGuideEntries;

        /// <summary>
        /// Die Anzahl der verarbeiteten PAT.
        /// </summary>
        private long m_patProcessed;

        /// <summary>
        /// Die zuletzt beobachtete PAT.
        /// </summary>
        private PAT m_lastPAT;

        /// <summary>
        /// Die Liste der zuletzt beobachteten PMT.
        /// </summary>
        private Dictionary<ushort, oldTables.PMT> m_lastPMTs = new Dictionary<ushort, oldTables.PMT>();

        /// <summary>
        /// Die zuletzt erhaltende Diensttabelle.
        /// </summary>
        private oldTables.SDT m_lastSDT;

        /// <summary>
        /// Die Anzahl der verarbeiteten Diensttabellen.
        /// </summary>
        private long m_sdtProcessed;

        /// <summary>
        /// Die Anzahl der verarbeiteten Netzwerkinformationen.
        /// </summary>
        private long m_nitProcessed;

        /// <summary>
        /// Die letzten Netzwerkinformationen für die aktuelle Empfangsgruppe.
        /// </summary>
        private oldTables.NIT m_lastNIT;

        /// <summary>
        /// Erstellt eine neue Analysekomponente.
        /// </summary>
        public RtpTransportStreamAnalyser()
        {
            // Register parsers
            Register<EIT>( ProcessGuide );
            Register<PAT>( ProcessAssociationTable );
            Register<SDT>( ProcessServiceTable );
            Register<NIT>( ProcessNetworkInformation );
        }

        /// <summary>
        /// Verarbeitet die Netzwerkinformation, die üblicherweise nur zum Sendersuchlauf benötigt wird.
        /// </summary>
        /// <param name="table">Ein Satz an Informationen.</param>
        private void ProcessNetworkInformation( NIT table )
        {
            // Count
            m_nitProcessed++;

            // Not us
            if (!table.ForCurrentGroup)
                return;

            // Must use legacy implementation
            var nit = table.Table;
            if (!nit.IsValid)
                return;

            // Remember
            m_lastNIT = nit;
        }

        /// <summary>
        /// Bearbeitet eine Diensttabelle.
        /// </summary>
        /// <param name="table">Die zu verarbeitende Tabelle.</param>
        private void ProcessServiceTable( SDT table )
        {
            // Count
            m_sdtProcessed++;

            // Must use legacy implementation
            var sdt = table.Table;
            if (!sdt.IsValid)
                return;

            // Remember
            m_lastSDT = sdt;
        }

        /// <summary>
        /// Meldet die Verarbeitung einer Tabelle an.
        /// </summary>
        /// <typeparam name="TTableType">Die Art der Tabelle.</typeparam>
        /// <param name="processor">Die Verarbeitungsmethode.</param>
        private void Register<TTableType>( Action<TTableType> processor ) where TTableType : WellKnownTable
        {
            // Forward
            Register( WellKnownTable.GetWellKnownStream( typeof( TTableType ) ), processor );
        }

        /// <summary>
        /// Meldet die Verarbeitung einer Tabelle an.
        /// </summary>
        /// <typeparam name="TTableType">Die Art der Tabelle.</typeparam>
        /// <param name="pid">Der zu verwendende Datenstrom.</param>
        /// <param name="processor">Die Verarbeitungsmethode.</param>
        private void Register<TTableType>( ushort pid, Action<TTableType> processor ) where TTableType : Table
        {
            // Forward
            m_parser.RegisterCustomFilter( pid, new SIBuilder( m_parser, TableParser.Create( processor ).AddPayload ) );
        }

        /// <summary>
        /// Verarbeitet die Liste der Dienste auf dem aktuellen Transportstrom.
        /// </summary>
        /// <param name="table">Die Liste der Dienste.</param>
        private void ProcessAssociationTable( PAT table )
        {
            // Count
            m_patProcessed++;

            // Remember
            m_lastPAT = table;

            // Read the mapping
            var mapping = new HashSet<ushort>( table.Services );

            // Reset 
            foreach (var terminated in m_lastPMTs.Keys.Where( key => !mapping.Contains( key ) ).ToArray())
                m_parser.RemoveFilter( terminated );

            // Add           
            foreach (var active in mapping.Where( key => !m_lastPMTs.ContainsKey( key ) ))
                Register<PMT>( table[active].Value, ProcessMappingTable );
        }

        /// <summary>
        /// Die Übersicht über eine Quelle.
        /// </summary>
        /// <param name="table">Die Details zur Quelle.</param>
        private void ProcessMappingTable( PMT table )
        {
            // Use earlier DVB.NET imnplementation
            var pmt = table.Table;
            if (!pmt.IsValid)
                return;

            // Register in map
            m_lastPMTs[pmt.ProgramNumber] = pmt;
        }

        /// <summary>
        /// Verarbeitet einen Eintrag aus der Programmzeitschrift.
        /// </summary>
        /// <param name="guideItem">Eine vollständige Tabelle.</param>
        private void ProcessGuide( EIT guideItem )
        {
            // Count outer
            m_numberOfGuideTables++;

            // Count inner
            m_numberOfGuideEntries += guideItem.Events.LongCount();
        }

        /// <summary>
        /// Überträgt Daten zur Analyse.
        /// </summary>
        /// <param name="buffer">Der Speicher mit den Daten.</param>
        /// <param name="offset">Die Position des ersten Nutzbytes.</param>
        /// <param name="length">Die Anzahl der Nutzbytes.</param>
        public void Feed( byte[] buffer, int offset, int length )
        {
            // Forward
            m_parser.AddPayload( buffer, offset, length );
        }

        /// <summary>
        /// Beendet die Nutzung dieser Komponente endgültig.
        /// </summary>
        public void Dispose()
        {
            // Report private overall statistics
            Console.WriteLine
                (
                    "Program Guide: {0:N0} Tables, {1:N0} Entries\nAssociation Tables: {2:N0}\nService Tables: {4:N0}\nPrograms: {3:N0}\nNetwork Tables: {5:N0}",
                    m_numberOfGuideTables,
                    m_numberOfGuideEntries,
                    m_patProcessed,
                    m_lastPMTs.Count,
                    m_sdtProcessed,
                    m_nitProcessed
                );

            // Network
            if (m_lastNIT != null)
                Console.WriteLine( "Overall: {0:N0} Source Groups / Transponders", m_lastNIT.NetworkEntries.Length );

            // Separate
            Console.WriteLine( "Service Descriptions:" );

            // Report service details
            if (m_lastSDT != null)
                foreach (var service in m_lastSDT.Services)
                {
                    // Load the service descriptor
                    var info = oldSIAPI.DescriptorExtensions.Find<oldSIAPI.Descriptors.Service>( service.Descriptors );

                    // Report
                    Console.WriteLine
                        (
                            "\tService {0} (0x{0:X4}): {1} {3} [{2}]",
                            service.ServiceIdentifier,
                            info.ServiceType,
                            info.ProviderName,
                            info.ServiceName
                        );
                }

            // Separate
            Console.WriteLine( "Service Details:" );

            // Report program details
            foreach (var program in m_lastPMTs.Values)
                Console.WriteLine
                    (
                        "\tService {0} (0x{0:X4}): {1}",
                        program.ProgramNumber,
                        string.Join( ", ", program.ProgramEntries.Select( e => e.StreamType.ToString() ) )
                    );

            // Request raw statistics
            Console.WriteLine
                (
                    "Received = {0:N0} Bytes / {1:N0} Packets / {2:N0} Callbacks / {9:N0} PAT\nSkipped = {3:N0} Bytes\nScrambled: {4:N0} Packets\nCorrupted: {5:N0} Packets, {6:N0} Streams, {7:N0} Tables\nResynchronisation: {8:N0} Times",
                    m_parser.BytesReceived,
                    m_parser.PacketsReceived,
                    m_parser.Callbacks,
                    m_parser.BytesSkipped,
                    m_parser.Scrambled,
                    m_parser.TransmissionErrors,
                    m_parser.CorruptedStream,
                    m_parser.CorruptedTable,
                    m_parser.Resynchronized,
                    m_parser.ValidPATCount
                );

            // Details for raw statistic
            foreach (var detailStatistics in m_parser.PacketStatistics)
                Console.WriteLine( "\tPID {0} (0x{0:X4}) {1:N0} Packets", detailStatistics.Key, detailStatistics.Value );

            // Get rid of parser
            using (m_parser)
                m_parser = null;
        }
    }
}
