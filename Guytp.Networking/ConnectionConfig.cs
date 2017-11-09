using System;

namespace Guytp.Networking
{
    /// <summary>
    /// This class defines the configuration of a single network connecetion.
    /// </summary>
    public class ConnectionConfig
    {
        #region Properties
        /// <summary>
        /// Gets or sets whether or not to initiate ping checks on a quiet connection.
        /// </summary>
        public bool PingCheckEnabled { get; set; }

        /// <summary>
        /// Gets or sets whether or not to respond to ping checks.
        /// </summary>
        public bool PingCheckResponsesEnabled { get; set; }

        /// <summary>
        /// Gets or sets how long to wait for a response to a ping before closing the connection.
        /// </summary>
        public TimeSpan PingResponseTime { get; set; }

        /// <summary>
        /// Gets or sets how long the inactivity can have been on a connection before a ping check is required.
        /// </summary>
        public TimeSpan PingCheckInactivityTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of inbound packets.
        /// </summary>
        public int MaximumInboundPacketSize { get; set; }

        /// <summary>
        /// Gets the maximum number of messages that can sit in a clients inbound queue before the connection is invalidated.
        /// </summary>
        public int MaximumInboundQueueSize { get; set; }

        /// <summary>
        /// Gets the maximum number of messages that can sit in a clients outbound queue before the connection is invalidated.
        /// </summary>
        public int MaximumOutboundQueueSize { get; set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Create a new instance of this class.
        /// </summary>
        public ConnectionConfig()
        {
            PingCheckEnabled = true;
            PingCheckResponsesEnabled = true;
            PingResponseTime = TimeSpan.FromSeconds(5);
            PingCheckInactivityTime = TimeSpan.FromSeconds(10);
            MaximumInboundPacketSize = 1000000;
            MaximumInboundQueueSize = 10;
            MaximumOutboundQueueSize = 100;
        }
        #endregion
    }
}