using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaMapper.Models;

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

        private bool isPaletteExpanded = true;
        private float expandedPaletteHeight = 420f; // Remembers its size when open

        // Drag-and-Drop / Interactive Link State Tracking
        private FunctoidDefinition? draggingPaletteFunctoid = null;
        private PointF paletteItemDragOffset;
        private Dictionary<FunctoidDefinition, SKRect> renderedItemHitboxes = new();

        private FunctoidInstance? draggingCanvasInstance = null;
        private PointF canvasInstanceDragOffset;

        // Active Splitter/Line Drag Tracking
        private bool isResizingLog = false;
        private bool isResizingLeft = false;
        private bool isResizingRight = false;
        private PointF lastMousePos;

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

            // Declared ONCE cleanly for the entire method scope
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

            // 2. Draw Trees
            float currentY = 10f;
            if (SourceRoot != null) RenderTreeElement(canvas, SourceRoot, 15f, ref currentY, true);

            currentY = 10f;
            if (DestinationRoot != null) RenderTreeElement(canvas, DestinationRoot, centerRight + 15f, ref currentY, false);

            // 3. Draw Placed Canvas Grid Functoids
            RenderActiveGridFunctoids(canvas, centerLeft);

            // 4. Draw Floating Tool Palette Window
            RenderFunctoidToolPalette(canvas);

            // 5. Draw Layout Grid Splitters
            bgPaint.Color = SKColors.DarkGray;
            canvas.DrawRect(leftTreeWidth - 2, 0, SplitterWidth, mainContentHeight, bgPaint);
            canvas.DrawRect(centerRight - 3, 0, SplitterWidth, mainContentHeight, bgPaint);
            canvas.DrawRect(0, mainContentHeight - 2, w, SplitterWidth, bgPaint);

            // 6. Draw Permanent Map Wire Connections
            RenderMapConnections(canvas);

            // 7. Draw Live Dragging Connection Line Link
            if (dragSource != null) {
                using var dragPaint = new SKPaint { Color = SKColors.Orange, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };

                using var previewPath = new SKPath();
                previewPath.MoveTo(currentDragPoint.X, currentDragPoint.Y);

                // FIXED: Using lastMousePos (cached from MouseMove) instead of e.X/e.Y
                float controlOffset = Math.Max(30f, Math.Abs(lastMousePos.X - currentDragPoint.X) * 0.5f);
                float cp1X = currentDragPoint.X + controlOffset;
                float cp1Y = currentDragPoint.Y;
                float cp2X = lastMousePos.X - controlOffset;
                float cp2Y = lastMousePos.Y;

                previewPath.CubicTo(cp1X, cp1Y, cp2X, cp2Y, lastMousePos.X, lastMousePos.Y);
                canvas.DrawPath(previewPath, dragPaint);
            }

            // 8. Draw Ghost Palette Item Preview Box
            if (draggingPaletteFunctoid != null) {
                using var ghostBoxPaint = new SKPaint { Color = new SKColor(52, 152, 219, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
                using var ghostBorderPaint = new SKPaint { Color = new SKColor(41, 128, 185), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
                using var ghostTextPaint = new SKPaint { Color = SKColors.White, TextSize = 11f, IsAntialias = true, TextAlign = SKTextAlign.Center };

                // FIXED: Using lastMousePos instead of e.X/e.Y
                float ghostX = lastMousePos.X - paletteItemDragOffset.X;
                float ghostY = lastMousePos.Y - paletteItemDragOffset.Y;
                SKRect ghostRect = new SKRect(ghostX, ghostY, ghostX + 110f, ghostY + 36f);

                canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBoxPaint);
                canvas.DrawRoundRect(ghostRect, 4f, 4f, ghostBorderPaint);
                canvas.DrawText(draggingPaletteFunctoid.Name, ghostRect.MidX, ghostRect.MidY + 4f, ghostTextPaint);
            }
        }
        private void RenderTreeElement(SKCanvas canvas, SchemaNode node, float x, ref float currentY, bool isSourceTree) {
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 12f, IsAntialias = true };
            using var nodeBoxPaint = new SKPaint { Color = new SKColor(220, 230, 242), Style = SKPaintStyle.Fill };
            using var nodeBorderPaint = new SKPaint { Color = new SKColor(120, 150, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

            float nodeHeight = 22f;
            float indentSpacing = 15f;

            node.LastRenderedY = currentY + (nodeHeight / 2f);
            node.LastRenderedHeight = nodeHeight;

            SKRect rowRect = new SKRect(x, currentY, x + (isSourceTree ? leftTreeWidth : rightTreeWidth) - 30f, currentY + nodeHeight);
            canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBoxPaint);
            canvas.DrawRoundRect(rowRect, 2f, 2f, nodeBorderPaint);

            string expandGlyph = node.Children.Count > 0 ? (node.IsExpanded ? "▼ " : "► ") : "  ";
            canvas.DrawText($"{expandGlyph}{node.Name}", x + 6f, currentY + 16f, textPaint);

            currentY += nodeHeight + 4f;

            if (node.IsExpanded) {
                foreach (var child in node.Children) {
                    RenderTreeElement(canvas, child, x + indentSpacing, ref currentY, isSourceTree);
                }
            }
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

        private void RenderFunctoidToolPalette(SKCanvas canvas) {
            using var bodyPaint = new SKPaint { Color = new SKColor(240, 243, 248), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var headerPaint = new SKPaint { Color = new SKColor(52, 73, 94), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var titleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 13f, IsAntialias = true, FakeBoldText = true };
            using var toggleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 14f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            using var categoryBgPaint = new SKPaint { Color = new SKColor(215, 222, 233), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var categoryTextPaint = new SKPaint { Color = new SKColor(60, 70, 80), TextSize = 11f, IsAntialias = true, FakeBoldText = true };

            using var itemBoxPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var itemBorderPaint = new SKPaint { Color = new SKColor(195, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var itemTextPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 11f, IsAntialias = true };

            renderedItemHitboxes.Clear();

            // 1. Adjust height boundary box if collapsed
            float currentHeight = isPaletteExpanded ? expandedPaletteHeight : PaletteHeaderHeight;
            paletteBounds = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + currentHeight);

            // Draw Window Body background (only if open)
            if (isPaletteExpanded) {
                canvas.DrawRoundRect(paletteBounds, 6f, 6f, bodyPaint);
            }

            // 2. Draw Window Header
            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
            canvas.DrawRoundRect(headerRect, 6f, 6f, headerPaint);
            canvas.DrawText("Functoid Toolbox", paletteBounds.Left + 12f, paletteBounds.Top + 21f, titleTextPaint);

            // 3. Draw Collapse/Expand Toggle Button Glyph on the right edge
            string toggleGlyph = isPaletteExpanded ? "▲" : "▼";
            float toggleX = paletteBounds.Right - 20f;
            float toggleY = paletteBounds.Top + 22f;
            canvas.DrawText(toggleGlyph, toggleX, toggleY, toggleTextPaint);

            // 4. Render Contents (Skip completely if collapsed)
            if (isPaletteExpanded) {
                float currentYOffset = paletteBounds.Top + PaletteHeaderHeight + 8f;
                float leftPadding = paletteBounds.Left + 8f;
                float itemWidth = paletteBounds.Width - 16f;
                float itemHeight = 24f;
                float categoryHeaderHeight = 22f;

                foreach (var category in FunctoidCategories) {
                    if (currentYOffset + categoryHeaderHeight > paletteBounds.Bottom - 8f) break;

                    SKRect catHeaderRect = new SKRect(leftPadding, currentYOffset, leftPadding + itemWidth, currentYOffset + categoryHeaderHeight);
                    category.LastRenderedHeaderBounds = catHeaderRect;

                    canvas.DrawRoundRect(catHeaderRect, 3f, 3f, categoryBgPaint);
                    string arrowToken = category.IsExpanded ? "▼ " : "► ";
                    canvas.DrawText($"{arrowToken}{category.Name.ToUpper()}", catHeaderRect.Left + 8f, catHeaderRect.Top + 15f, categoryTextPaint);

                    currentYOffset += categoryHeaderHeight + 4f;

                    if (category.IsExpanded) {
                        foreach (var functoid in AvailableFunctoids) {
                            if (functoid.CategoryId != category.Id) continue;
                            if (currentYOffset + itemHeight > paletteBounds.Bottom - 8f) break;

                            SKRect rowRect = new SKRect(leftPadding + 6f, currentYOffset, leftPadding + itemWidth - 6f, currentYOffset + itemHeight);
                            renderedItemHitboxes[functoid] = rowRect;

                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBoxPaint);
                            canvas.DrawRoundRect(rowRect, 3f, 3f, itemBorderPaint);

                            using var dotPaint = new SKPaint { Color = GetCategoryColor(category.Color), Style = SKPaintStyle.Fill, IsAntialias = true };
                            canvas.DrawCircle(rowRect.Left + 12f, rowRect.MidY, 4f, dotPaint);

                            canvas.DrawText(functoid.Name, rowRect.Left + 24f, rowRect.Top + 16f, itemTextPaint);

                            currentYOffset += itemHeight + 4f;
                        }
                    }
                    currentYOffset += 6f;
                }
            }

            // Outer border outline
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

        private void RenderMapConnections(SKCanvas canvas) {
            using var linePaint = new SKPaint {
                Color = new SKColor(46, 204, 113), // Crisp BizTalk Green
                StrokeWidth = 2.0f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            float centerLeft = leftTreeWidth;

            foreach (var conn in Connections) {
                float startX = 0f, startY = 0f;
                float endX = 0f, endY = 0f;

                // 1. EVALUATE START POINT (Where is the line coming from?)
                // ALIGNED: Using ConnectionEndpointType.Functoid from MappingProject.cs
                if (conn.Source.Type == ConnectionEndpointType.Functoid && conn.Source.FunctoidInstanceId != null) {
                    var instance = ActiveFunctoids.Find(f => f.Id == conn.Source.FunctoidInstanceId);
                    if (instance != null) {
                        startX = centerLeft + instance.X + instance.Width; // Starts at right edge of functoid
                        startY = instance.Y + (instance.Height / 2f);
                    } else continue;
                } else // Fallback: It's a Source Tree Node
                  {
                    startY = FindNodeY(SourceRoot, conn.Source.NodePath);
                    startX = leftTreeWidth - 25f; // Center-right of source node row
                    if (startY == -1) continue;
                }

                // 2. EVALUATE END POINT (Where is the line going to?)
                // ALIGNED: Using ConnectionEndpointType.Functoid from MappingProject.cs
                if (conn.Target.Type == ConnectionEndpointType.Functoid && conn.Target.FunctoidInstanceId != null) {
                    var instance = ActiveFunctoids.Find(f => f.Id == conn.Target.FunctoidInstanceId);
                    if (instance != null) {
                        endX = centerLeft + instance.X; // Ends at left edge of functoid
                        endY = instance.Y + (instance.Height / 2f);
                    } else continue;
                } else // Fallback: It's a Destination Tree Node
                  {
                    endY = FindNodeY(DestinationRoot, conn.Target.NodePath);
                    endX = Width - rightTreeWidth + 5f; // Left edge of destination node row
                    if (endY == -1) continue;
                }

                // 3. DRAW CURVE
                using var path = new SKPath();
                path.MoveTo(startX, startY);

                float controlOffset = Math.Max(30f, Math.Abs(endX - startX) * 0.5f);
                path.CubicTo(startX + controlOffset, startY, endX - controlOffset, endY, endX, endY);
                canvas.DrawPath(path, linePaint);
            }
        }

        private float FindNodeY(SchemaNode? root, string path) {
            if (root == null) return -1;
            if (root.Name == path) return root.LastRenderedY;

            foreach (var child in root.Children) {
                float found = FindNodeY(child, path);
                if (found != -1) return found;
            }
            return -1;
        }

        private SchemaNode? FindNodeAtPosition(SchemaNode node, float mouseCodeY) {
            float halfHeight = node.LastRenderedHeight / 2f;
            if (mouseCodeY >= node.LastRenderedY - halfHeight && mouseCodeY <= node.LastRenderedY + halfHeight) {
                return node;
            }

            if (node.IsExpanded) {
                foreach (var child in node.Children) {
                    var found = FindNodeAtPosition(child, mouseCodeY);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private SchemaNode? FindDestinationNodeAtPosition(SchemaNode node, float mouseCodeY) {
            float halfHeight = node.LastRenderedHeight / 2f;
            if (mouseCodeY >= node.LastRenderedY - halfHeight && mouseCodeY <= node.LastRenderedY + halfHeight) {
                return node;
            }

            if (node.IsExpanded) {
                foreach (var child in node.Children) {
                    var found = FindDestinationNodeAtPosition(child, mouseCodeY);
                    if (found != null) return found;
                }
            }
            return null;
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            lastMousePos = new PointF(e.X, e.Y);
            float mainContentHeight = Height - logPanelHeight;
            float centerLeft = leftTreeWidth;
            float centerRight = Width - rightTreeWidth;

            // 1. Tool Palette Interactions Intercept
            // Inside OnMouseDown, update Section 1:
            if (paletteBounds.Contains(e.X, e.Y)) {
                var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
                if (headerRect.Contains(e.X, e.Y)) {
                    // Check if the click happened near the right edge (within 35 pixels of the end)
                    if (e.X > paletteBounds.Right - 35f) {
                        isPaletteExpanded = !isPaletteExpanded; // Toggle state!
                        Invalidate();
                        return;
                    }

                    // Otherwise, drag the palette window around as normal
                    isDraggingPalette = true;
                    paletteDragOffset = new PointF(e.X - paletteBounds.Left, e.Y - paletteBounds.Top);
                    Capture = true;
                    return;
                }

                // Only process category expanding or item dragging if the panel is open!
                if (isPaletteExpanded) {
                    foreach (var category in FunctoidCategories) {
                        if (category.LastRenderedHeaderBounds.Contains(e.X, e.Y)) {
                            category.IsExpanded = !category.IsExpanded;
                            Invalidate();
                            return;
                        }
                    }

                    foreach (var itemEntry in renderedItemHitboxes) {
                        if (itemEntry.Value.Contains(e.X, e.Y)) {
                            draggingPaletteFunctoid = itemEntry.Key;
                            paletteItemDragOffset = new PointF(55f, 18f);
                            Capture = true;
                            Invalidate();
                            return;
                        }
                    }
                }
                return;
            }

            // 2. Existing Canvas Functoid Click Intercept
            if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                for (int i = ActiveFunctoids.Count - 1; i >= 0; i--) {
                    var instance = ActiveFunctoids[i];
                    float absoluteX = centerLeft + instance.X;
                    SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

                    if (itemRect.Contains(e.X, e.Y)) {
                        // Clicked Right Half: Initiate a valid downstream output connection wire trace
                        if (e.X > itemRect.MidX) {
                            dragSource = new ConnectionEndpoint {
                                Type = ConnectionEndpointType.Functoid, // Aligned with your MappingProject.cs enum
                                FunctoidInstanceId = instance.Id
                            };
                            currentDragPoint = new PointF(itemRect.Right, itemRect.MidY);
                        }
                        // Clicked Left Half: Drag and reposition the block layout node 
                        else {
                            draggingCanvasInstance = instance;
                            canvasInstanceDragOffset = new PointF(e.X - absoluteX, e.Y - instance.Y);
                        }

                        Capture = true;
                        Invalidate();
                        return;
                    }
                }
            }

            // 3. Source Node Linking Line Selection Intercept
            if (e.X < leftTreeWidth && e.Y < mainContentHeight && SourceRoot != null) {
                SchemaNode? targetedNode = FindNodeAtPosition(SourceRoot, e.Y);
                if (targetedNode != null) {
                    dragSource = new ConnectionEndpoint {
                        Type = ConnectionEndpointType.SourceNode,
                        NodePath = targetedNode.Name
                    };
                    currentDragPoint = new PointF(leftTreeWidth - 25f, targetedNode.LastRenderedY);
                    Capture = true;
                    Invalidate();
                    return;
                }
            }

            // 4. Component Layout Splitters Sizing Slices
            if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) isResizingLog = true;
            else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth) isResizingLeft = true;
            else if (Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) isResizingRight = true;

            Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            lastMousePos = new PointF(e.X, e.Y);
            float centerLeft = leftTreeWidth;
            float centerRight = Width - rightTreeWidth;
            float mainContentHeight = Height - logPanelHeight;

            if (draggingCanvasInstance != null) {
                float proposedCanvasX = (e.X - centerLeft) - canvasInstanceDragOffset.X;
                float proposedCanvasY = e.Y - canvasInstanceDragOffset.Y;

                float maxCanvasWidth = centerRight - centerLeft;
                if (proposedCanvasX < 0) proposedCanvasX = 0;
                if (proposedCanvasX + draggingCanvasInstance.Width > maxCanvasWidth) proposedCanvasX = maxCanvasWidth - draggingCanvasInstance.Width;

                if (proposedCanvasY < 0) proposedCanvasY = 0;
                if (proposedCanvasY + draggingCanvasInstance.Height > mainContentHeight) proposedCanvasY = mainContentHeight - draggingCanvasInstance.Height;

                draggingCanvasInstance.X = proposedCanvasX;
                draggingCanvasInstance.Y = proposedCanvasY;
                Invalidate();
                return;
            }

            if (draggingPaletteFunctoid != null || dragSource != null) {
                Invalidate();
                return;
            }

            if (isDraggingPalette) {
                float width = paletteBounds.Width;
                float height = paletteBounds.Height;
                float newLeft = e.X - paletteDragOffset.X;
                float newTop = e.Y - paletteDragOffset.Y;

                paletteBounds = new SKRect(newLeft, newTop, newLeft + width, newTop + height);
                Invalidate();
                return;
            }

            if (isResizingLog) { logPanelHeight = Height - e.Y; Invalidate(); } else if (isResizingLeft) { leftTreeWidth = e.X; Invalidate(); } else if (isResizingRight) { rightTreeWidth = Width - e.X; Invalidate(); }

            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
            if (headerRect.Contains(e.X, e.Y)) Cursor = Cursors.SizeAll;
            else if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) Cursor = Cursors.HSplit;
            else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth || Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) Cursor = Cursors.VSplit;
            else Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            // 1. Release active canvas dragging tracking state safely
            if (draggingCanvasInstance != null) {
                draggingCanvasInstance = null;
                Capture = false;
                Invalidate();
                return;
            }

            // 2. Handle dropping a new toolbox item onto the central canvas grid
            if (draggingPaletteFunctoid != null) {
                float centerLeft = leftTreeWidth;
                float centerRight = Width - rightTreeWidth;
                float mainContentHeight = Height - logPanelHeight;

                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    float targetCanvasX = (e.X - centerLeft) - paletteItemDragOffset.X;
                    float targetCanvasY = e.Y - paletteItemDragOffset.Y;

                    ActiveFunctoids.Add(new FunctoidInstance {
                        Id = Guid.NewGuid(),
                        Definition = draggingPaletteFunctoid,
                        X = targetCanvasX,
                        Y = targetCanvasY,
                        Width = 110f,
                        Height = 36f
                    });
                }
                draggingPaletteFunctoid = null;
                Capture = false;
                Invalidate();
                return;
            }

            isDraggingPalette = isResizingLog = isResizingLeft = isResizingRight = false;

            // 3. HANDLE LINE DROPS WITH VALIDATION
            if (dragSource != null) {
                float centerLeft = leftTreeWidth;
                float centerRight = Width - rightTreeWidth;
                float mainContentHeight = Height - logPanelHeight;

                // Case A: Dropped inside the central canvas (Targeting a Functoid Input Slot)
                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    FunctoidInstance? targetFunctoid = null;
                    bool droppedOnRightHalf = false;

                    foreach (var instance in ActiveFunctoids) {
                        float absoluteX = centerLeft + instance.X;
                        SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);
                        if (itemRect.Contains(e.X, e.Y)) {
                            targetFunctoid = instance;
                            if (e.X > itemRect.MidX) droppedOnRightHalf = true;
                            break;
                        }
                    }

                    if (targetFunctoid != null) {
                        // RULE 1: Direct validation blocking dropping anything on the RIGHT half (Output)
                        if (droppedOnRightHalf) {
                            System.Diagnostics.Debug.WriteLine("[VALIDATION] Rejected: Cannot wire lines into an output slot.");
                            dragSource = null;
                            Capture = false;
                            Invalidate();
                            return;
                        }

                        // RULE 2: No Self-Looping (Functoid connecting its own output to its own input)
                        if (dragSource.Type == ConnectionEndpointType.Functoid && dragSource.FunctoidInstanceId == targetFunctoid.Id) {
                            System.Diagnostics.Debug.WriteLine("[VALIDATION] Rejected: A functoid cannot connect to itself.");
                            dragSource = null;
                            Capture = false;
                            Invalidate();
                            return;
                        }

                        // Everything is valid! Save the connection into the target input slot (Index 0)
                        Connections.Add(new MappingConnection {
                            Source = dragSource,
                            Target = new ConnectionEndpoint {
                                Type = ConnectionEndpointType.Functoid,
                                FunctoidInstanceId = targetFunctoid.Id,
                                ArgumentIndex = 0
                            }
                        });
                    }
                }
                // Case B: Dropped inside the Right Tree Panel (Targeting a Destination Node)
                else if (e.X > centerRight && DestinationRoot != null) {
                    // RULE 3: Ensure Source tree nodes can't hop straight to destination trees if that's a restriction you want,
                    // or simply guarantee that we are dragging valid sources here.
                    SchemaNode? targetedDestNode = FindDestinationNodeAtPosition(DestinationRoot, e.Y);
                    if (targetedDestNode != null) {
                        Connections.Add(new MappingConnection {
                            Source = dragSource,
                            Target = new ConnectionEndpoint {
                                Type = ConnectionEndpointType.DestinationNode,
                                NodePath = targetedDestNode.Name
                            }
                        });
                    }
                }
                dragSource = null;
            }
            Capture = false;
            Invalidate();
        }
    }
}