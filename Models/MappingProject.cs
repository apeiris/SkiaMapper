using System;
using System.Collections.Generic;

namespace SkiaMapper.Models {
    public enum ConnectionEndpointType {
        SourceNode,
        Functoid,
        DestinationNode,
        CanvasFunctoid // <-- Added for Canvas Engine alignment
    }

    public class ConnectionEndpoint {
        public ConnectionEndpointType Type { get; set; }
        public string NodePath { get; set; } = string.Empty;

        // Changed to Guid? so it can sit cleanly as null when linking regular nodes
        public Guid? FunctoidInstanceId { get; set; }

        public int ArgumentIndex { get; set; }

        // Added alias property to fix Line 485 instantly without breaking ArgumentIndex
        public int InputIndex {
            get => ArgumentIndex;
            set => ArgumentIndex = value;
        }
    }

    public class MappingConnection {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ConnectionEndpoint Source { get; set; } = new();
        public ConnectionEndpoint Target { get; set; } = new();
    }
}