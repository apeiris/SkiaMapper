using System.Collections.Generic;
using System.Xml.Serialization;

namespace SkiaMapper.Models;

public class SchemaNode {
    public string Name { get; set; } = string.Empty;
    public bool IsAttribute { get; set; }
    public bool IsExpanded { get; set; } = true;
    public List<SchemaNode> Children { get; set; } = new();
    [XmlIgnore]
    public int Depth { get; set; }

    // Canvas coordinate caches optimized for UI drawing routines
    [XmlIgnore]
    public float LastRenderedY { get; set; }

    [XmlIgnore]
    public float LastRenderedHeight { get; set; } = 20f;
}
