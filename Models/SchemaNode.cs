using System.Collections.Generic;
using System.Xml.Serialization;
using SkiaSharp;

namespace SkiaMapper.Models {
    public class SchemaNode {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public bool IsAttribute { get; set; }
        public bool IsExpanded { get; set; } = true;
        public List<SchemaNode> Children { get; set; } = new();

        // Layout Tracking for rendering/interaction coordinates
        [XmlIgnore]
        public float LastRenderedY { get; set; }

        [XmlIgnore]
        public float LastRenderedHeight { get; set; } = 20f;
    }
}