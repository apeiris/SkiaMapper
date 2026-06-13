using SkiaSharp;
using System.Xml.Serialization;

namespace SkiaMapper.Models {
    [XmlRoot("BuiltInFunctoids")]
    public class FunctoidContainer {
        [XmlArray("Categories")]
        [XmlArrayItem("Category")]
        public List<FunctoidCategory> Categories { get; set; } = new();

        [XmlElement("Functoid")]
        public List<FunctoidDefinition> Functoids { get; set; } = new();
    }

    public class FunctoidCategory {
        [XmlAttribute("name")] public string Name { get; set; } = string.Empty;
        [XmlAttribute("Id")] public int Id { get; set; }
        [XmlAttribute("Color")] public string Color { get; set; } = "LightGray";
        public bool IsExpanded { get; set; } = true;

        // Tracks where the category header line hit box sits on the canvas layout
        public SKRect LastRenderedHeaderBounds { get; set; }
    }

    public class FunctoidDefinition {
        [XmlAttribute("name")] public string Name { get; set; } = string.Empty;
        [XmlAttribute("catId")] public int CategoryId { get; set; }
        [XmlAttribute("Id")] public int Id { get; set; }

        public string MethodNameFormat { get; set; } = string.Empty;
        public string JoinOperator { get; set; } = string.Empty;
        public string ScriptTemplate { get; set; } = string.Empty;
    }

    // Instance of a Functoid dropped onto the center grid canvas
    public class FunctoidInstance {
        public Guid Id { get; set; } = Guid.NewGuid();
        public FunctoidDefinition Definition { get; set; } = new();
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 100f;
        public float Height { get; set; } = 40f;
    }
}