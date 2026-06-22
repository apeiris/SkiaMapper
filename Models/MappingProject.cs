using System;

namespace SkiaMapper.Models {
    public enum ConnectionEndpointType {
        SourceNode,
        Functoid,
        DestinationNode
    }

    public class ConnectionEndpoint {
        public ConnectionEndpointType Type { get; set; }
        public string NodePath { get; set; } = string.Empty;

        // Nullable Guid used when connecting canvas functoid instances
        public Guid? FunctoidInstanceId { get; set; }

        // Standardized name across the canvas workspace layout engine
        public int InputIndex { get; set; }
    }

    public class MappingConnection {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ConnectionEndpoint Source { get; set; } = new();
        public ConnectionEndpoint Target { get; set; } = new();
    }
}