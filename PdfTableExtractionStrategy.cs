using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.VisualBasic.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using static SkiaMapper.Adapters.PdfSourcePlugin; // Ensure it references PdfTextBlock

public class PdfTableExtractionStrategy : ITextExtractionStrategy {
    private readonly float _tableStartY;
    private readonly float _tableEndY;
    private readonly float _rowTolerance;

    private bool started = false;
    private bool stopped = false;

    private readonly List<PdfTextBlock> chunks = new List<PdfTextBlock>();
   // private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public PdfTableExtractionStrategy(float tableStartY, float tableEndY, float rowTolerance = 6.0f) {
        _tableStartY = tableStartY;
        _tableEndY = tableEndY;
        _rowTolerance = rowTolerance;
    }

    public void EventOccurred(IEventData data, EventType type) {
        if (stopped) return;
        if (type == EventType.RENDER_TEXT) {
            TextRenderInfo tri = (TextRenderInfo)data;
            var text = tri.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            var rect = tri.GetDescentLine().GetBoundingRectangle();
            float curX = rect.GetX();
            float curY = rect.GetY();

            // 1. Wait until we enter the table region (scanning downwards from top Y)
            if (!started) {
                if (curY <= _tableStartY && curY >= _tableEndY) {
                    started = true;
                } else if (curY > _tableStartY) {
                    return; // Above table
                }
            }

            // 2. Stop if we pass below the table region into totals/footer
            if (started && curY < _tableEndY) {
                stopped = true;
                return;
            }

            chunks.Add(new PdfTextBlock {
                Text = text.Trim(),
                X = curX,
                Y = curY
            });
        }
    }

    /// <summary>
    /// Groups captured table chunks into rows and generates X12-style Loop XElements (<Loop LoopId="PO1">).
    /// </summary>
    public List<XElement> GetTableLoopsAsXml() {
        if (chunks.Count == 0) return new List<XElement>();

        // Group chunks by Y coordinate using binning/tolerance to capture multi-word columns on the same row
        var rowGroups = chunks
            .GroupBy(c => Math.Round(c.Y / _rowTolerance) * _rowTolerance)
            .OrderByDescending(g => g.Key); // Top to bottom of page

        var loopElements = new List<XElement>();
        int sequenceNum = 1;

        foreach (var rowGroup in rowGroups) {
            // Sort chunks Left-to-Right for columns
            var sortedRowChunks = rowGroup.OrderBy(c => c.X).ToList();

            // Skip table header row if captured
            string fullRowText = string.Join(" ", sortedRowChunks.Select(c => c.Text));
            if (fullRowText.Contains("Item") && fullRowText.Contains("Code") && fullRowText.Contains("Qty")) {
                continue;
            }

            var po1Loop = new XElement("Loop",
                new XAttribute("LoopId", "PO1"),
                new XAttribute("Sequence", sequenceNum++)
            );

            // Add each column chunk as a child Block element
            foreach (var chunk in sortedRowChunks) {
                po1Loop.Add(new XElement("Block",
                    new XElement("Text", chunk.Text),
                    new XElement("X", chunk.X),
                    new XElement("Y", chunk.Y)
                ));
            }

            loopElements.Add(po1Loop);
        }

        return loopElements;
    }

    public string GetResultantText() {
        var loops = GetTableLoopsAsXml();
        StringBuilder sb = new StringBuilder();
        foreach (var loop in loops) {
            sb.AppendLine(loop.ToString());
        }
        return sb.ToString();
    }

    public ICollection<EventType> GetSupportedEvents() => null;
    public void BeginTextBlock() { }
    public void EndTextBlock() { }
    public void RenderImage(ImageRenderInfo data) { }
    public void RenderText(TextRenderInfo renderInfo) { }
}