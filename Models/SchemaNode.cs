using System.Collections.Generic;

namespace SkiaMapper.Models {
    public class SchemaNode {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public bool IsAttribute { get; set; }
        public bool IsExpanded { get; set; } = true;
        public List<SchemaNode> Children { get; set; } = new();

        // Layout Tracking for rendering/interaction coordinates
        public float LastRenderY { get; set; }
        public float Height { get; set; } = 24f;
    }
}