using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using SkiaMapper.Models;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

namespace SkiaMapper.Adapters {
    public class PdfSourcePlugin : ISourceDocumentPlugin {
        public string FormatName => "PDF Document";
        public string FileFilter => "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*";

        public bool CanHandle(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return false;
            return Path.GetExtension(filePath).Equals(".pdf", System.StringComparison.OrdinalIgnoreCase);
        }

        public SchemaNode ParseSchema(string filePath) {
            var rootNode = new SchemaNode {
                Name = Path.GetFileName(filePath),
                IsExpanded = true,
                Children = new List<SchemaNode>()
            };

            if (!File.Exists(filePath)) return rootNode;

            using var pdfReader = new PdfReader(filePath);
            using var pdfDoc = new PdfDocument(pdfReader);

            // Extract interactive AcroForm fields using iText 9.x API
            PdfAcroForm acroForm = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (acroForm != null) {
                IDictionary<string, PdfFormField> fields = acroForm.GetAllFormFields();
                if (fields != null && fields.Count > 0) {
                    var formFieldsGroup = new SchemaNode {
                        Name = "AcroForm Fields",
                        IsExpanded = true,
                        Children = new List<SchemaNode>()
                    };

                    foreach (var kvp in fields) {
                        string fieldName = kvp.Key;
                        PdfFormField field = kvp.Value;

                        var fieldNode = new SchemaNode {
                            Name = fieldName,
                            IsExpanded = false,
                            Children = new List<SchemaNode> {
                                new() { Name = $"@Type: {field.GetFormType()?.ToString() ?? "Field"}" }
                            }
                        };
                        formFieldsGroup.Children.Add(fieldNode);
                    }
                    rootNode.Children.Add(formFieldsGroup);
                }
            }

            if (rootNode.Children.Count == 0) {
                rootNode.Children.Add(new SchemaNode {
                    Name = "[No Interactive AcroForm Fields Found]",
                    IsExpanded = false
                });
            }

            return rootNode;
        }
    }
}