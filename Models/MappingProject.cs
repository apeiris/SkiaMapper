using System;
using System.Collections.Generic;

namespace SkiaMapper.Models {
    public enum ConnectionEndpointType { SourceNode, Functoid, DestinationNode }

    public class ConnectionEndpoint {
        public ConnectionEndpointType Type { get; set; }
        public string NodePath { get; set; } = string.Empty; // For Source/Destination tracking
        public Guid FunctoidInstanceId { get; set; }         // For Functoid instance tracking
        public int ArgumentIndex { get; set; }               // Which input slot if targetting a functoid
    }

    public class MappingConnection {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ConnectionEndpoint Source { get; set; } = new();
        public ConnectionEndpoint Target { get; set; } = new();
    }
}