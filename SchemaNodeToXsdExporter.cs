using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using SkiaMapper.Models;

public static class SchemaNodeToXsdExporter {
    public static void ExportSchemaNodeToXsd(SchemaNode rootNode, string xsdOutputPath) {
        if (rootNode == null) throw new ArgumentNullException(nameof(rootNode));

        XmlSchema schema = new XmlSchema {
            Id = "GeneratedFromSchemaNode",
            ElementFormDefault = XmlSchemaForm.Qualified
        };

        XmlSchemaElement rootElement = ConvertNodeToXsdElement(rootNode);
        schema.Items.Add(rootElement);

        // Compile and write the schema to disk
        schema.Compile(null);

        using (FileStream fs = new FileStream(xsdOutputPath, FileMode.Create, FileAccess.Write)) {
            schema.Write(fs);
        }
    }

    private static XmlSchemaElement ConvertNodeToXsdElement(SchemaNode node) {
        string cleanName = SanitizeXmlName(node.Name);

        XmlSchemaElement xsdElement = new XmlSchemaElement {
            Name = cleanName,
            SchemaTypeName = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema")
        };

        if (node.Children != null && node.Children.Count > 0) {
            XmlSchemaComplexType complexType = new XmlSchemaComplexType();
            XmlSchemaSequence sequence = new XmlSchemaSequence();

            foreach (var child in node.Children) {
                string childCleanName = SanitizeXmlName(child.Name);

                if (child.IsAttribute) {
                    XmlSchemaAttribute xsdAttr = new XmlSchemaAttribute {
                        Name = childCleanName,
                        SchemaTypeName = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema")
                    };
                    complexType.Attributes.Add(xsdAttr);
                } else {
                    sequence.Items.Add(ConvertNodeToXsdElement(child));
                }
            }

            if (sequence.Items.Count > 0) {
                complexType.Particle = sequence;
            }

            xsdElement.SchemaType = complexType;
            xsdElement.SchemaTypeName = null;
        }

        return xsdElement;
    }

    private static string SanitizeXmlName(string name) {
        if (string.IsNullOrEmpty(name)) return "Node";

        // Remove leading '@' if it's an attribute name
        if (name.StartsWith("@")) {
            name = name.Substring(1);
        }

        // Convert UI labels like "Loop [N1]" into valid XML names like "Loop_N1"
        name = name.Replace("[", "_").Replace("]", "").Replace(" ", "_");

        // Ensure it's a valid XML name by stripping any remaining invalid characters
        return Regex.Replace(name, @"[^\w.-]", "_");
    }
}