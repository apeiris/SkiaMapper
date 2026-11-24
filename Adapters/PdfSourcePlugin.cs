using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using iText.Kernel.Pdf;
using iText.Forms;
using iText.Forms.Fields;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SkiaMapper.Models;

namespace SkiaMapper.Adapters {
    public class PdfSourcePlugin : ISourceDocumentPlugin {
        public string FormatName => "PDF Document";
        public string FileFilter => "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*";

        public bool CanHandle(string filePath) {
            if (string.IsNullOrEmpty(filePath)) return false;
            return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
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

            // 1. Interactive AcroForm Fields (if present)
            PdfAcroForm acroForm = PdfAcroForm.GetAcroForm(pdfDoc, false);
            if (acroForm != null) {
                IDictionary<string, PdfFormField> fields = acroForm.GetAllFormFields();
                if (fields != null && fields.Count > 0) {
                    var formNode = new SchemaNode {
                        Name = "AcroForm Fields",
                        IsExpanded = true,
                        Children = new List<SchemaNode>()
                    };
                    foreach (var kvp in fields) {
                        string val = kvp.Value.GetValueAsString() ?? string.Empty;
                        formNode.Children.Add(new SchemaNode {
                            Name = string.IsNullOrEmpty(val) ? kvp.Key : $"{kvp.Key}: {val}",
                            IsExpanded = false
                        });
                    }
                    rootNode.Children.Add(formNode);
                }
            }

            // 2. Actual Document Content (Grouped into X12 Loops & Blocks)
            int totalPages = pdfDoc.GetNumberOfPages();
            var contentNode = new SchemaNode {
                Name = "Document Content",
                IsExpanded = true,
                Children = new List<SchemaNode>()
            };

            for (int pageNum = 1; pageNum <= totalPages; pageNum++) {
                var page = pdfDoc.GetPage(pageNum);
                var listener = new CustomTextListener();
                PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                var pageNode = new SchemaNode {
                    Name = $"Page {pageNum}",
                    IsExpanded = true,
                    Children = new List<SchemaNode>()
                };

                // Group chunks by vertical proximity (Y-coordinate bands) to detect table rows
                var rowGroups = listener.Chunks
                    .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                    .GroupBy(c => Math.Round(c.Y / 12) * 12);

                int lineItemIndex = 1;
                foreach (var group in rowGroups.OrderByDescending(g => g.Key)) {
                    bool isTableRow = group.Count() >= 3; // Heuristic for line item table rows

                    if (isTableRow) {
                        var loopNode = new SchemaNode {
                            Name = $"Loop [PO1] (Seq: {lineItemIndex++})",
                            IsExpanded = false,
                            Children = new List<SchemaNode>()
                        };

                        foreach (var chunk in group.OrderBy(c => c.X)) {
                            loopNode.Children.Add(new SchemaNode {
                                Name = "Block",
                                IsExpanded = false,
                                Children = new List<SchemaNode> {
                                    new() { Name = $"@Text: {chunk.Text.Trim()}" },
                                    new() { Name = $"@X: {chunk.X}" },
                                    new() { Name = $"@Y: {chunk.Y}" }
                                }
                            });
                        }
                        pageNode.Children.Add(loopNode);
                    } else {
                        foreach (var chunk in group) {
                            pageNode.Children.Add(new SchemaNode {
                                Name = "Block",
                                IsExpanded = false,
                                Children = new List<SchemaNode> {
                                    new() { Name = $"@Text: {chunk.Text.Trim()}" },
                                    new() { Name = $"@X: {chunk.X}" },
                                    new() { Name = $"@Y: {chunk.Y}" }
                                }
                            });
                        }
                    }
                }

                if (pageNode.Children.Count > 0) {
                    contentNode.Children.Add(pageNode);
                }
            }

            if (contentNode.Children.Count > 0) {
                rootNode.Children.Add(contentNode);
            }

            if (rootNode.Children.Count == 0) {
                rootNode.Children.Add(new SchemaNode {
                    Name = "[No Content Found in PDF]",
                    IsExpanded = false
                });
            }

            return rootNode;
        }

        public static string GetPdfContentAsXmlString(string filePath) {
            using var pdfReader = new PdfReader(filePath);
            using var pdfDoc = new PdfDocument(pdfReader);

            var xmlRoot = new XElement("PdfDocument");
            var contentElement = new XElement("DocumentContent");

            int totalPages = pdfDoc.GetNumberOfPages();
            for (int pageNum = 1; pageNum <= totalPages; pageNum++) {
                var page = pdfDoc.GetPage(pageNum);
                var listener = new CustomTextListener();
                PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                var pageElement = new XElement("Page", new XAttribute("Number", pageNum));

                // Group chunks by Y coordinate ranges to isolate tabular rows
                var rowGroups = listener.Chunks
                    .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                    .GroupBy(c => Math.Round(c.Y / 12) * 12); // Fixed syntax error (* 12)

                int lineItemIndex = 1;
                foreach (var group in rowGroups.OrderByDescending(g => g.Key)) {
                    bool isTableRow = group.Count() >= 3; // Heuristic for item row columns

                    if (isTableRow) {
                        // Construct an X12-style PO1 Loop node
                        var po1Loop = new XElement("Loop", new XAttribute("LoopId", "PO1"));
                        po1Loop.Add(new XAttribute("Sequence", lineItemIndex++));
                        foreach (var chunk in group.OrderBy(c => c.X)) {
                            po1Loop.Add(new XElement("Block",
                                new XElement("Text", chunk.Text.Trim()),
                                new XElement("X", chunk.X),
                                new XElement("Y", chunk.Y)
                            ));
                        }
                        pageElement.Add(po1Loop);
                    } else {
                        // Standard single blocks (headers, metadata, totals)
                        foreach (var chunk in group) {
                            pageElement.Add(new XElement("Block",
                                new XElement("Text", chunk.Text.Trim()),
                                new XElement("X", chunk.X),
                                new XElement("Y", chunk.Y)
                            ));
                        }
                    }
                }
                contentElement.Add(pageElement);
            }
            xmlRoot.Add(contentElement);

            var xDoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xmlRoot);
            return xDoc.ToString();
        }

        public class PdfTextBlock {
            public string Text { get; set; } = string.Empty;
            public double X { get; set; }
            public double Y { get; set; }
        }

        public class CustomTextListener : ITextExtractionStrategy {
            public List<PdfTextBlock> Chunks { get; } = new();
            public ICollection<EventType> GetSupportedEvents() {
                return new List<EventType>() { EventType.RENDER_TEXT };
            }
            public void EventOccurred(IEventData data, EventType type) {
                if (type != EventType.RENDER_TEXT) return;
                if (data is not TextRenderInfo renderInfo) return;
                var text = renderInfo.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;
                var rect = renderInfo.GetDescentLine().GetBoundingRectangle();

                Chunks.Add(new PdfTextBlock {
                    Text = text,
                    X = rect.GetX(),
                    Y = rect.GetY()
                });
            }
            public string GetResultantText() => string.Join("", Chunks.Select(c => c.Text));
        }
    }
}