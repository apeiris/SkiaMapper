using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SkiaSharp;
using System;
using System.Collections.Generic;
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

    // Layout tracking bounding box for UI hit-testing
    [XmlIgnore]
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

    // --- NEW: Tracks whether Roslyn found a 'params' array modifier ---
    [XmlAttribute("isVariable")]
    public bool IsVariable { get; set; } = false;
}

public class FunctoidInstance {
    public Guid Id { get; set; } = Guid.NewGuid();
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public FunctoidDefinition Definition { get; set; }

    // Dynamic scripting overrides
    public string? CustomScriptBody { get; set; }
    public string? CustomMethodName { get; set; }

    [XmlIgnore]
    public bool IsScriptCustomized => Definition != null &&
                                     !string.IsNullOrEmpty(CustomScriptBody) &&
                                     CustomScriptBody.Trim() != Definition.ScriptTemplate?.Trim();

    [XmlIgnore]
    public string XslVariableName {
        get {
            string baseName = null;

            // 1. Try extracting the method name directly from the custom script body via Roslyn
            if (!string.IsNullOrEmpty(CustomScriptBody)) {
                try {
                    var tree = CSharpSyntaxTree.ParseText(CustomScriptBody);
                    var methodDecl = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (methodDecl != null) {
                        baseName = methodDecl.Identifier.Text;
                    }
                } catch {
                    // Fallback gracefully if parsing fails
                }
            }

            // 2. Fall back to CustomMethodName if explicitly set
            if (string.IsNullOrEmpty(baseName) && !string.IsNullOrEmpty(CustomMethodName)) {
                baseName = CustomMethodName;
            }

            // 3. Fall back to Definition Name or default "Transform"
            if (string.IsNullOrEmpty(baseName)) {
                baseName = Definition?.Name?.Replace(" ", "") ?? "Transform";
            }

            // Ensure the name is a valid identifier (starts with a letter, alphanumeric only)
            string sanitized = new string(baseName.Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0])) {
                sanitized = "Transform" + sanitized;
            }

            return $"{sanitized}_{Id.ToString("N").Substring(0, 8)}";
        }
    }
}
