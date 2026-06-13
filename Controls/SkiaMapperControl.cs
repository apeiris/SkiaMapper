using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SkiaMapper.Controls {
    public class SkiaMapperControl : SKControl {

        // Panel Dimension Metrics (Splitters)
        private float leftTreeWidth = 280f;
        private float rightTreeWidth = 280f;
        private float logPanelHeight = 120f;
        private const float SplitterWidth = 5f;

        // Tool Palette Window Metrics
        private SKRect paletteBounds = new SKRect(310f, 30f, 550f, 450f);
        private const float PaletteHeaderHeight = 32f;
        private bool isDraggingPalette = false;
        private PointF paletteDragOffset;

        // --- ADD THESE FIELDS HERE TO FIX THE CS0103 ERRORS ---
        private FunctoidDefinition? draggingPaletteFunctoid = null;
        private PointF paletteItemDragOffset;
        private Dictionary<FunctoidDefinition, SKRect> renderedItemHitboxes = new();

        // Active State Tracking
        private bool isResizingLog = false;
        private bool isResizingLeft = false;
        private bool isResizingRight = false;
        private PointF lastMousePos;

        // --- Core State Properties ---
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SchemaNode? SourceRoot { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SchemaNode? DestinationRoot { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<FunctoidCategory> FunctoidCategories { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<FunctoidDefinition> AvailableFunctoids { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<FunctoidInstance> ActiveFunctoids { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<MappingConnection> Connections { get; set; } = new();

        private ConnectionEndpoint? dragSource = null;
        private PointF currentDragPoint;

        public SkiaMapperControl() {
            this.DoubleBuffered = true;
            this.Dock = DockStyle.Fill;
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) {
            base.OnPaintSurface(e);
            var canvas = e.Surface.Canvas;
            int w = e.Info.Width;
            int h = e.Info.Height;

            canvas.Clear(SKColors.White);

            float centerLeft = leftTreeWidth;
            float centerRight = w - rightTreeWidth;
            float mainContentHeight = h - logPanelHeight;

            // 1. Draw Viewport Background Panels
            using var bgPaint = new SKPaint();
            bgPaint.Color = SKColors.GhostWhite;
            canvas.DrawRect(0, 0, leftTreeWidth, mainContentHeight, bgPaint);
            canvas.DrawRect(centerRight, 0, rightTreeWidth, mainContentHeight, bgPaint);

            bgPaint.Color = new SKColor(245, 247, 250);
            canvas.DrawRect(centerLeft, 0, centerRight - centerLeft, mainContentHeight, bgPaint);

            bgPaint.Color = new SKColor(30, 30, 30);
            canvas.DrawRect(0, mainContentHeight, w, logPanelHeight, bgPaint);

            // 2. Draw Unified Tree Systems
            float currentY = 10f;
            if (SourceRoot != null) RenderTreeElement(canvas, SourceRoot, 15f, ref currentY, true);

            currentY = 10f;
            if (DestinationRoot != null) RenderTreeElement(canvas, DestinationRoot, centerRight + 15f, ref currentY, false);

            // 3. Draw Placed Canvas Grid Functoids (Will be empty on startup now)
            RenderActiveGridFunctoids(canvas, centerLeft);

            // 4. DRAW THE MOVABLE FLOATING TOOL PALETTE CONTAINER PANEL (Holds all 8 items)
            RenderFunctoidToolPalette(canvas);

            // 5. Draw Layout Grid Component Splitters
            bgPaint.Color = SKColors.DarkGray;
            canvas.DrawRect(leftTreeWidth - 2, 0, SplitterWidth, mainContentHeight, bgPaint);
            canvas.DrawRect(centerRight - 3, 0, SplitterWidth, mainContentHeight, bgPaint);
            canvas.DrawRect(0, mainContentHeight - 2, w, SplitterWidth, bgPaint);

            // 6. Draw Dynamic Connection Lines
            RenderMapConnections(canvas);

            if (dragSource != null) {
                using var dragPaint = new SKPaint { Color = SKColors.Orange, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };
                canvas.DrawLine(currentDragPoint.X, currentDragPoint.Y, lastMousePos.X, lastMousePos.Y, dragPaint);
            }
            // --- DRAW GHOST GIP PREVIEW FOR ACTIVE PALETTE ITEM DRAG ENGINE ---
            if (draggingPaletteFunctoid != null) {
                using var ghostBoxPaint = new SKPaint { Color = new SKColor(52, 152, 219, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
                using var ghostBorderPaint = new SKPaint { Color = new SKColor(41, 128, 185), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
                using var ghostTextPaint = new SKPaint { Color = SKColors.White, TextSize = 11f, IsAntialias = true, TextAlign = SKTextAlign.Center };

                // Emulate standard placement dimensions (110 wide, 36 high) dynamically centered around the cursor pointer
                float ghostX = lastMousePos.X - paletteItemDragOffset.X;
                float ghostY = lastMousePos.Y - paletteItemDragOffset.Y;
                SKRect ghostRect = new SKRect(ghostX, ghostY, ghostX + 110f, ghostY + 36f);

                canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBoxPaint);
                canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBorderPaint);
                canvas.DrawText(draggingPaletteFunctoid.Name, ghostRect.MidX, ghostRect.MidY + 4f, ghostTextPaint);
            }
        }

        private void RenderFunctoidToolPalette(SKCanvas canvas) {
            using var bodyPaint = new SKPaint { Color = new SKColor(240, 243, 248), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var headerPaint = new SKPaint { Color = new SKColor(52, 73, 94), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var titleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 13f, IsAntialias = true, FakeBoldText = true };

            // Styled fonts for category banners
            using var categoryBgPaint = new SKPaint { Color = new SKColor(215, 222, 233), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var categoryTextPaint = new SKPaint { Color = new SKColor(60, 70, 80), TextSize = 11f, IsAntialias = true, FakeBoldText = true };

            using var itemBoxPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var itemBorderPaint = new SKPaint { Color = new SKColor(195, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var itemTextPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 11f, IsAntialias = true };

            // 1. Draw Core Window Frame
            canvas.DrawRoundRect(paletteBounds, 6f, 6f, bodyPaint);

            // 2. Draw Window Caption Bar Header
            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
            canvas.DrawRoundRect(headerRect, 6f, 6f, headerPaint);
            canvas.DrawText("Functoid Toolbox", paletteBounds.Left + 12f, paletteBounds.Top + 21f, titleTextPaint);

            // 3. Draw Section Columns dynamically using state checking logic
            float currentYOffset = paletteBounds.Top + PaletteHeaderHeight + 8f;
            float leftPadding = paletteBounds.Left + 8f;
            float itemWidth = paletteBounds.Width - 16f;
            float itemHeight = 24f;
            float categoryHeaderHeight = 22f;

            foreach (var category in FunctoidCategories) {
                // Stop rendering if we run completely outside the palette panel bounds
                if (currentYOffset + categoryHeaderHeight > paletteBounds.Bottom - 8f) break;

                // Establish interactive hit box for this specific category header row strip
                SKRect catHeaderRect = new SKRect(leftPadding, currentYOffset, leftPadding + itemWidth, currentYOffset + categoryHeaderHeight);
                category.LastRenderedHeaderBounds = catHeaderRect;

                // Draw background strip row for category header banner strip
                canvas.DrawRoundRect(catHeaderRect, 3f, 3f, categoryBgPaint);

                // Add visual arrow string token state representation (▼ = Open, ► = Closed)
                string arrowToken = category.IsExpanded ? "▼ " : "► ";
                canvas.DrawText($"{arrowToken}{category.Name.ToUpper()}", catHeaderRect.Left + 8f, catHeaderRect.Top + 15f, categoryTextPaint);

                currentYOffset += categoryHeaderHeight + 4f;

                // Render child components strictly if the category structure state evaluates to Expanded
                if (category.IsExpanded) {
                    foreach (var functoid in AvailableFunctoids) {
                        if (functoid.CategoryId != category.Id) continue;

                        if (currentYOffset + itemHeight > paletteBounds.Bottom - 8f) break;

                        SKRect rowRect = new SKRect(leftPadding + 6f, currentYOffset, leftPadding + itemWidth - 6f, currentYOffset + itemHeight);

                        canvas.DrawRoundRect(rowRect, 3f, 3f, itemBoxPaint);
                        canvas.DrawRoundRect(rowRect, 3f, 3f, itemBorderPaint);

                        using var dotPaint = new SKPaint { Color = GetCategoryColor(category.Color), Style = SKPaintStyle.Fill, IsAntialias = true };
                        canvas.DrawCircle(rowRect.Left + 12f, rowRect.MidY, 4f, dotPaint);

                        canvas.DrawText(functoid.Name, rowRect.Left + 24f, rowRect.Top + 16f, itemTextPaint);

                        currentYOffset += itemHeight + 4f;
                    }
                }
                currentYOffset += 6f; // Extra margin spacer after structural section close block
            }

            // 4. Trace edge borders lines contour frame boundary layout structures
            canvas.DrawRoundRect(paletteBounds, 6f, 6f, borderPaint);
        }

        private SKColor GetCategoryColor(string colorName) {
            return colorName.ToLower() switch {
                "lightblue" => new SKColor(135, 206, 250),
                "lightgreen" => new SKColor(144, 238, 144),
                "lightyellow" => new SKColor(255, 255, 224),
                _ => SKColors.LightGray
            };
        }

        private void RenderActiveGridFunctoids(SKCanvas canvas, float canvasLeftOffset) {
            using var boxPaint = new SKPaint { Color = SKColors.LightSkyBlue, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = SKColors.SteelBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 12f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            foreach (var functoid in ActiveFunctoids) {
                float absoluteX = canvasLeftOffset + functoid.X;
                SKRect rect = new SKRect(absoluteX, functoid.Y, absoluteX + functoid.Width, functoid.Y + functoid.Height);

                canvas.DrawRoundRect(rect, 4f, 4f, boxPaint);
                canvas.DrawRoundRect(rect, 4f, 4f, borderPaint);
                canvas.DrawText(functoid.Definition.Name, rect.MidX, rect.MidY + 4f, textPaint);
            }
        }
        private void RenderTreeElement(SKCanvas canvas, SchemaNode node, float xOffset, ref float yOffset, bool isSource) {
            node.LastRenderY = yOffset;

            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 13f, IsAntialias = true };
            using var iconPaint = new SKPaint { Color = node.Children.Count > 0 ? SKColors.Gray : SKColors.LightBlue, Style = SKPaintStyle.Fill };

            // Draw expansion toggle control node
            canvas.DrawCircle(xOffset, yOffset + 6, 4, iconPaint);
            canvas.DrawText(node.Name, xOffset + 15f, yOffset + 10f, textPaint);

            yOffset += node.Height;

            if (node.IsExpanded) {
                foreach (var child in node.Children) {
                    RenderTreeElement(canvas, child, xOffset + 20f, ref yOffset, isSource);
                }
            }
        }

        private void RenderFunctoids(SKCanvas canvas, float canvasLeftOffset) {
            using var boxPaint = new SKPaint { Color = SKColors.LightSkyBlue, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = SKColors.SteelBlue, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 12f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            foreach (var functoid in ActiveFunctoids) {
                float absoluteX = canvasLeftOffset + functoid.X;
                SKRect rect = new SKRect(absoluteX, functoid.Y, absoluteX + functoid.Width, functoid.Y + functoid.Height);

                canvas.DrawRoundRect(rect, 4f, 4f, boxPaint);
                canvas.DrawRoundRect(rect, 4f, 4f, borderPaint);
                canvas.DrawText(functoid.Definition.Name, rect.MidX, rect.MidY + 4f, textPaint);
            }
        }

        private void RenderMapConnections(SKCanvas canvas) {
            using var linePaint = new SKPaint { Color = SKColors.CadetBlue, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };

            foreach (var conn in Connections) {
                PointF start = GetEndpointCoordinates(conn.Source);
                PointF end = GetEndpointCoordinates(conn.Target);

                // Draw a cubic Bezier curve link for a professional UI layout appearance
                using var path = new SKPath();
                path.MoveTo(start.X, start.Y);
                path.CubicTo(start.X + 50, start.Y, end.X - 50, end.Y, end.X, end.Y);
                canvas.DrawPath(path, linePaint);
            }
        }

        private PointF GetEndpointCoordinates(ConnectionEndpoint endpoint) {
            float centerLeft = leftTreeWidth;
            float centerRight = Width - rightTreeWidth;

            if (endpoint.Type == ConnectionEndpointType.SourceNode) {
                // Root lookup fallback for demo rendering points
                float y = SourceRoot != null ? FindNodeY(SourceRoot, endpoint.NodePath) : 30f;
                return new PointF(centerLeft, y + 6f);
            }
            if (endpoint.Type == ConnectionEndpointType.DestinationNode) {
                float y = DestinationRoot != null ? FindNodeY(DestinationRoot, endpoint.NodePath) : 30f;
                return new PointF(centerRight, y + 6f);
            }

            // Functoid fallback lookup
            var instance = ActiveFunctoids.Find(f => f.Id == endpoint.FunctoidInstanceId);
            if (instance != null) {
                return new PointF(centerLeft + instance.X + (instance.Width / 2), instance.Y + (instance.Height / 2));
            }

            return PointF.Empty;
        }

        private float FindNodeY(SchemaNode root, string path) {
            if (root.Name == path) return root.LastRenderY;
            foreach (var child in root.Children) {
                float found = FindNodeY(child, path);
                if (found != -1) return found;
            }
            return -1;
        }

        // --- Interaction & Splitter Handling ---

        protected override void OnMouseDown(MouseEventArgs e) {
            lastMousePos = new PointF(e.X, e.Y);
            float mainContentHeight = Height - logPanelHeight;

            // 1. Is the click targeting inside our Floating Tool Palette?
            if (paletteBounds.Contains(e.X, e.Y)) {
                var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);

                if (headerRect.Contains(e.X, e.Y)) {
                    // Target hit inside Window Title Header Bar -> Handle Panel Dragging movement logic
                    isDraggingPalette = true;
                    paletteDragOffset = new PointF(e.X - paletteBounds.Left, e.Y - paletteBounds.Top);
                    Capture = true;
                    return;
                }

                // Check if target hit is intersecting inside any specific Category Header Bounds
                foreach (var category in FunctoidCategories) {
                    if (category.LastRenderedHeaderBounds.Contains(e.X, e.Y)) {
                        // Toggle state property assignment block inversion parameters
                        category.IsExpanded = !category.IsExpanded;
                        Invalidate(); // Refresh structural views canvas loop mapping engine instantly
                        return;
                    }
                }

                // Absorb click events targeting internal components inside the window block panel frame boundary
                return;
            }

            // 2. Default Splitters logic fallback operations layout processing pipeline pass
            if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) isResizingLog = true;
            else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth) isResizingLeft = true;
            else if (Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) isResizingRight = true;
            else {
                if (e.X < leftTreeWidth) {
                    dragSource = new ConnectionEndpoint { Type = ConnectionEndpointType.SourceNode, NodePath = "UserId" };
                    currentDragPoint = new PointF(leftTreeWidth, e.Y);
                }
            }
            Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            if (isDraggingPalette) {
                // Move panel bounds using calculated mouse offsets
                float width = paletteBounds.Width;
                float height = paletteBounds.Height;
                float newLeft = e.X - paletteDragOffset.X;
                float newTop = e.Y - paletteDragOffset.Y;

                paletteBounds = new SKRect(newLeft, newTop, newLeft + width, newTop + height);
                Invalidate();
                return;
            }

            if (isResizingLog) {
                logPanelHeight = Height - e.Y;
                Invalidate();
            } else if (isResizingLeft) {
                leftTreeWidth = e.X;
                Invalidate();
            } else if (isResizingRight) {
                rightTreeWidth = Width - e.X;
                Invalidate();
            } else if (dragSource != null) {
                lastMousePos = new PointF(e.X, e.Y);
                Invalidate();
            }

            // Cursor icon update mapping
            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
            float mainContentHeight = Height - logPanelHeight;

            if (headerRect.Contains(e.X, e.Y)) Cursor = Cursors.SizeAll;
            else if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) Cursor = Cursors.HSplit;
            else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth || Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) Cursor = Cursors.VSplit;
            else Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            isDraggingPalette = isResizingLog = isResizingLeft = isResizingRight = false;

            if (dragSource != null) {
                if (e.X > Width - rightTreeWidth) {
                    Connections.Add(new MappingConnection {
                        Source = dragSource,
                        Target = new ConnectionEndpoint { Type = ConnectionEndpointType.DestinationNode, NodePath = "UserId" }
                    });
                }
                dragSource = null;
            }
            Capture = false;
            Invalidate();
        }

    }
}