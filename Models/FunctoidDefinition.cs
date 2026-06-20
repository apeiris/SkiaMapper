using SkiaSharp;
using System.Xml.Serialization;

namespace SkiaMapper.Models; 
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
    [XmlAttribute("Id")] public int Id { get; set; }
    [XmlAttribute("name")] public string Name { get; set; } = string.Empty;
    [XmlAttribute("catId")] public int CategoryId { get; set; }


    public string MethodNameFormat { get; set; } = string.Empty;
    public string JoinOperator { get; set; } = string.Empty;
    public string ScriptTemplate { get; set; } = string.Empty;
    public int InputParametersCount { get; set; } = 1;
}

public enum ParameterSourceType {
    Unlinked,
    SourceSchemaNode,
    FunctoidOutput
}
public class FunctoidParameter {
    public int Index { get; set; }
    public ParameterSourceType SourceType { get; set; } = ParameterSourceType.Unlinked;

    // Filled if linked to an inbound XML Schema element/attribute
    public string? SourceNodePath { get; set; }

    // Filled if linked to the output of a preceding functoid block
    public Guid? SourceFunctoidId { get; set; }
}
public class FunctoidInstance {
    public Guid Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public FunctoidDefinition Definition { get; set; }

    // Tracks live user alterations on the canvas
    public string? CustomScriptBody { get; set; }
    public string? CustomMethodName { get; set; }
    public bool IsScriptCustomized =>    Definition != null &&  !string.IsNullOrEmpty(CustomScriptBody) &&     CustomScriptBody.Trim() != Definition.ScriptTemplate?.Trim();
    [XmlIgnore]
    public Dictionary<int, FunctoidParameter> ConnectedParameters { get; set; } = new();// Holds connection assignments keyed by their parameter position index.
    /// <summary>
    /// Proxy array used strictly by the XmlSerializer framework. 
    /// It converts the internal dictionary into a simple flat array structure automatically.
    /// </summary>
    [XmlArray("ConnectedParameters")]
    [XmlArrayItem("Parameter")]
    public FunctoidParameter[] ConnectedParametersProxy {
        get {
            // Flatten the internal runtime dictionary to a serializable flat array
            var items = new List<FunctoidParameter>();
            foreach (var kvp in ConnectedParameters) {
                // Ensure the index matches the dictionary key assignment
                kvp.Value.Index = kvp.Key;
                items.Add(kvp.Value);
            }
            return items.ToArray();
        }
        set {
            // Rehydrate the flat XML array back into the high-performance dictionary lookup
            ConnectedParameters = new Dictionary<int, FunctoidParameter>();
            if (value != null) {
                foreach (var param in value) {
                    ConnectedParameters[param.Index] = param;
                }
            }
        }
    }
}
