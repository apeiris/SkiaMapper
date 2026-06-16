using SkiaMapper.Models;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

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

        // Inside your metrics fields area
        private SKRect saveBtnRect;
        private SKRect loadBtnRect;
        private SKRect clearBtnRect; 

        private const float HeaderIconSize = 20f;


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
            using var borderPaint = new SKPaint { Color = new SKColor(170, 185, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var textPaint = new SKPaint { Color = new SKColor(45, 55, 65), TextSize = 11f, IsAntialias = true, FakeBoldText = true, TextAlign = SKTextAlign.Center };
            using var pinPaint = new SKPaint { Color = new SKColor(130, 140, 150), Style = SKPaintStyle.Fill, IsAntialias = true };

            foreach (var functoid in ActiveFunctoids) {
                float absoluteX = canvasLeftOffset + functoid.X;
                SKRect rect = new SKRect(absoluteX, functoid.Y, absoluteX + functoid.Width, functoid.Y + functoid.Height);

                // 1. Resolve Dynamic Category Identity Color Tint
                SKColor nodeFillColor = new SKColor(235, 240, 248); // High-contrast neutral safe fallback
                SKColor accentColor = SKColors.LightGray;
                bool hasAccent = false;

                if (functoid.Definition != null) {
                    var category = FunctoidCategories.Find(c => c.Id == functoid.Definition.CategoryId);
                    if (category != null) {
                        accentColor = GetCategoryColor(category.Color);
                        // Apply a transparent alpha alpha blend (40 out of 255) for the block body 
                        // to maintain crisp text legibility against dark category definitions
                        nodeFillColor = new SKColor(accentColor.Red, accentColor.Green, accentColor.Blue, 40);
                        hasAccent = true;
                    }
                }

                // 2. Render Node Base Container 
                using var dynamicBoxPaint = new SKPaint { Color = nodeFillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(rect, 4f, 4f, dynamicBoxPaint);
                canvas.DrawRoundRect(rect, 4f, 4f, borderPaint);

                // 3. Render Solid Category Accent Strip (Left Side Frame Flag)
                if (hasAccent) {
                    using var accentPaint = new SKPaint { Color = accentColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    SKRect accentStrip = new SKRect(rect.Left, rect.Top, rect.Left + 5f, rect.Bottom);
                    canvas.DrawRoundRect(accentStrip, 4f, 4f, accentPaint);
                    // Squaring out the inside corners of our accent bar frame safely
                    canvas.DrawRect(new SKRect(rect.Left + 3f, rect.Top, rect.Left + 5f, rect.Bottom), accentPaint);
                }

                // 4. Draw Input/Output Handle Vector Nubs
                canvas.DrawCircle(rect.Left, rect.MidY, 4f, pinPaint);
                canvas.DrawCircle(rect.Right, rect.MidY, 4f, pinPaint);

                // 5. Draw Descriptive Label
                canvas.DrawText(functoid.Definition.Name, rect.MidX + (hasAccent ? 2.5f : 0f), rect.MidY + 4f, textPaint);
            }
        }
        private void RenderFunctoidToolPalette(SKCanvas canvas) {
            using var bodyPaint = new SKPaint { Color = new SKColor(240, 243, 248), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var headerPaint = new SKPaint { Color = new SKColor(52, 73, 94), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var borderPaint = new SKPaint { Color = new SKColor(44, 62, 80), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var titleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 13f, IsAntialias = true, FakeBoldText = true };
            using var toggleTextPaint = new SKPaint { Color = SKColors.White, TextSize = 14f, IsAntialias = true, TextAlign = SKTextAlign.Center };

            // Smooth vector icon brush properties
            using var iconStrokePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
            using var iconFillPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };

            using var categoryBgPaint = new SKPaint { Color = new SKColor(215, 222, 233), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var categoryTextPaint = new SKPaint { Color = new SKColor(60, 70, 80), TextSize = 11f, IsAntialias = true, FakeBoldText = true };
            using var itemBoxPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
            using var itemBorderPaint = new SKPaint { Color = new SKColor(195, 205, 215), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
            using var itemTextPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 11f, IsAntialias = true };

            renderedItemHitboxes.Clear();

            // Adjust height boundary box if collapsed
            float currentHeight = isPaletteExpanded ? expandedPaletteHeight : PaletteHeaderHeight;
            paletteBounds = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + currentHeight);

            if (isPaletteExpanded) {
                canvas.DrawRoundRect(paletteBounds, 6f, 6f, bodyPaint);
            }

            // Draw Window Header
            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
            canvas.DrawRoundRect(headerRect, 6f, 6f, headerPaint);
            canvas.DrawText("Functoid Toolbox", paletteBounds.Left + 12f, paletteBounds.Top + 21f, titleTextPaint);

            // --- CALCULATE HEADER BUTTON SLOTS (Right-Aligned) ---
            float rightEdge = paletteBounds.Right - 12f;
            float headerCenterY = paletteBounds.Top + (PaletteHeaderHeight / 2f);

            // 1. Collapse/Expand Toggle Slot
            float toggleX = rightEdge - 8f;
            string toggleGlyph = isPaletteExpanded ? "▲" : "▼";
            canvas.DrawText(toggleGlyph, toggleX, paletteBounds.Top + 22f, toggleTextPaint);

            // 2. Save Button Slot (Floppy Disk Shape)
            float saveCenterX = rightEdge - 42f;
            saveBtnRect = new SKRect(saveCenterX - 10f, headerCenterY - 10f, saveCenterX + 10f, headerCenterY + 10f);
            canvas.DrawRect(saveBtnRect, iconStrokePaint);
            canvas.DrawRect(new SKRect(saveCenterX - 5f, saveBtnRect.Top, saveCenterX + 5f, saveBtnRect.Top + 6f), iconFillPaint);
            canvas.DrawRect(new SKRect(saveCenterX - 4f, saveBtnRect.Bottom - 6f, saveCenterX + 4f, saveBtnRect.Bottom), iconStrokePaint);

            // 3. Load Button Slot (Folder Shape)
            float loadCenterX = rightEdge - 72f;
            loadBtnRect = new SKRect(loadCenterX - 11f, headerCenterY - 8f, loadCenterX + 11f, headerCenterY + 8f);
            using var folderPath = new SKPath();
            folderPath.MoveTo(loadBtnRect.Left, loadBtnRect.Bottom);
            folderPath.LineTo(loadBtnRect.Left, loadBtnRect.Top);
            folderPath.LineTo(loadBtnRect.Left + 7f, loadBtnRect.Top);
            folderPath.LineTo(loadBtnRect.Left + 11f, loadBtnRect.Top + 4f);
            folderPath.LineTo(loadBtnRect.Right, loadBtnRect.Top + 4f);
            folderPath.LineTo(loadBtnRect.Right, loadBtnRect.Bottom);
            folderPath.Close();
            canvas.DrawPath(folderPath, iconStrokePaint);

            // 4. Clear Canvas Button Slot (Trash Can Shape)
            float clearCenterX = rightEdge - 102f;
            clearBtnRect = new SKRect(clearCenterX - 9f, headerCenterY - 9f, clearCenterX + 9f, headerCenterY + 9f);

            // Draw Trash Can base bucket
            canvas.DrawRect(new SKRect(clearBtnRect.Left + 2f, clearBtnRect.Top + 4f, clearBtnRect.Right - 2f, clearBtnRect.Bottom), iconStrokePaint);
            // Draw Trash Can lid brim line
            canvas.DrawLine(clearBtnRect.Left, clearBtnRect.Top + 3f, clearBtnRect.Right, clearBtnRect.Top + 3f, iconStrokePaint);
            // Draw Trash Can lid top handle
            canvas.DrawRect(new SKRect(clearCenterX - 3f, clearBtnRect.Top, clearCenterX + 3f, clearBtnRect.Top + 3f), iconStrokePaint);

            // Render Contents (Skip completely if collapsed)
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
            if (paletteBounds.Contains(e.X, e.Y)) {
                var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);
                if (headerRect.Contains(e.X, e.Y)) {
                    // CLICK TRAP: Clear Canvas Workspace Button (PRIORITIZED)
                    if (clearBtnRect.Contains(e.X, e.Y)) {
                        var choice = MessageBox.Show(
                            "Are you sure you want to completely clear the canvas? All functoids and mapping traces will be deleted.",
                            "Clear Canvas",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );

                        if (choice == DialogResult.Yes) {
                            ActiveFunctoids.Clear();
                            Connections.Clear();

                            // Reset dragging/active states safely
                            dragSource = null;
                            draggingCanvasInstance = null;

                            Invalidate(); // Refresh layout immediately
                        }
                        return;
                    }

                    // CLICK TRAP: Save Config File Button
                    if (saveBtnRect.Contains(e.X, e.Y)) {
                        using var sfd = new SaveFileDialog { Filter = "XML Files|*.xml", Title = "Save Mapping Schema" };
                        if (sfd.ShowDialog() == DialogResult.OK) {
                            string xmlData = SaveConfiguration();
                            File.WriteAllText(sfd.FileName, xmlData);
                        }
                        return;
                    }

                    // CLICK TRAP: Load Config File Button
                    if (loadBtnRect.Contains(e.X, e.Y)) {
                        using var ofd = new OpenFileDialog { Filter = "XML Files|*.xml", Title = "Load Mapping Schema" };
                        if (ofd.ShowDialog() == DialogResult.OK) {
                            string xmlData = File.ReadAllText(ofd.FileName);
                            LoadConfiguration(xmlData);
                        }
                        return;
                    }

                    // CLICK TRAP: Collapse Toggle Arrow Box
                    if (e.X > paletteBounds.Right - 28f) {
                        isPaletteExpanded = !isPaletteExpanded;
                        Invalidate();
                        return;
                    }

                    // Default Header Action: Move/Drag window palette around canvas
                    isDraggingPalette = true;
                    paletteDragOffset = new PointF(e.X - paletteBounds.Left, e.Y - paletteBounds.Top);
                    Capture = true;
                    return;
                }

                // Only process category expanding or item dragging if open
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
                                Type = ConnectionEndpointType.Functoid,
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

            // 1. Handle Active Component Dragging Layouts
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
                Cursor = Cursors.SizeAll;
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
                return;
            } else if (isResizingLeft) {
                leftTreeWidth = e.X;
                Invalidate();
                return;
            } else if (isResizingRight) {
                rightTreeWidth = Width - e.X;
                Invalidate();
                return;
            }

            // 2. Evaluate Dynamic Hover Cursor Styles
            var headerRect = new SKRect(paletteBounds.Left, paletteBounds.Top, paletteBounds.Right, paletteBounds.Top + PaletteHeaderHeight);

            if (headerRect.Contains(e.X, e.Y)) {
                // HOVER OVERRIDE: If over utility buttons or the collapse toggle region, use normal pointer
                if (clearBtnRect.Contains(e.X, e.Y) ||
                    loadBtnRect.Contains(e.X, e.Y) ||
                    saveBtnRect.Contains(e.X, e.Y) ||
                    e.X > paletteBounds.Right - 28f) {
                    Cursor = Cursors.Default;
                } else {
                    Cursor = Cursors.SizeAll; // Dragging space for the title block window
                }
            } else if (Math.Abs(e.Y - mainContentHeight) < SplitterWidth) {
                Cursor = Cursors.HSplit;
            } else if (Math.Abs(e.X - leftTreeWidth) < SplitterWidth || Math.Abs(e.X - (Width - rightTreeWidth)) < SplitterWidth) {
                Cursor = Cursors.VSplit;
            } else {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            float centerLeft = leftTreeWidth;
            float centerRight = Width - rightTreeWidth;
            float mainContentHeight = Height - logPanelHeight;

            // --- 1. PALETTE DROP HANDLER: Add new Functoid to Workspace Canvas ---
            if (draggingPaletteFunctoid != null) {
                // Verify the drop point landed inside the central canvas grid bounds
                if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {

                    // Calculate coordinates relative to the central canvas origin context
                    float localX = (e.X - centerLeft) - paletteItemDragOffset.X;
                    float localY = e.Y - paletteItemDragOffset.Y;

                    // Restrict bounds so dropped items don't bleed beneath layout bars
                    if (localX < 0) localX = 0;
                    if (localX + 110f > (centerRight - centerLeft)) localX = (centerRight - centerLeft) - 110f;
                    if (localY < 0) localY = 0;
                    if (localY + 36f > mainContentHeight) localY = mainContentHeight - 36f;

                    // Instantiate a completely fresh object to append to our collection stream
                    var newInstance = new FunctoidInstance {
                        Id = Guid.NewGuid(),
                        X = localX,
                        Y = localY,
                        Width = 110f,
                        Height = 36f,
                        Definition = draggingPaletteFunctoid
                    };

                    ActiveFunctoids.Add(newInstance);
                }

                draggingPaletteFunctoid = null;
                Capture = false;
                Invalidate();
                return; // Early break out so we don't accidentally check connection line logic
            }

            // --- 2. MAP CONNECTION TRACE WIRE HANDLER ---
            if (dragSource != null) {
                ConnectionEndpoint? dragTarget = null;

                // EVALUATE TARGET: Dropped onto a Destination Tree Node?
                if (e.X > centerRight && e.Y < mainContentHeight && DestinationRoot != null) {
                    SchemaNode? targetedNode = FindDestinationNodeAtPosition(DestinationRoot, e.Y);
                    if (targetedNode != null) {
                        dragTarget = new ConnectionEndpoint {
                            Type = ConnectionEndpointType.DestinationNode,
                            NodePath = targetedNode.Name
                        };
                    }
                }
                // EVALUATE TARGET: Dropped onto a Canvas Functoid?
                else if (e.X > centerLeft && e.X < centerRight && e.Y < mainContentHeight) {
                    foreach (var instance in ActiveFunctoids) {
                        float absoluteX = centerLeft + instance.X;
                        SKRect itemRect = new SKRect(absoluteX, instance.Y, absoluteX + instance.Width, instance.Y + instance.Height);

                        // Clicking/releasing on the left half represents an Input connection
                        if (itemRect.Contains(e.X, e.Y) && e.X <= itemRect.MidX) {
                            dragTarget = new ConnectionEndpoint {
                                Type = ConnectionEndpointType.Functoid,
                                FunctoidInstanceId = instance.Id
                            };
                            break;
                        }
                    }
                }

                // VALIDATE AND COMMIT CONNECTION
                if (dragTarget != null) {
                    bool isSelfLoop = dragSource.Type == ConnectionEndpointType.Functoid &&
                                      dragTarget.Type == ConnectionEndpointType.Functoid &&
                                      dragSource.FunctoidInstanceId == dragTarget.FunctoidInstanceId;

                    if (!isSelfLoop) {
                        // Multi-connection constraint lifted: we bypass duplication filters entirely
                        Connections.Add(new MappingConnection {
                            Source = dragSource,
                            Target = dragTarget
                        });
                    }
                }

                dragSource = null;
                Capture = false;
                Invalidate();
            }

            // --- 3. RESET MISCELLANEOUS RESIZE TRACKERS ---
            draggingCanvasInstance = null;
            isDraggingPalette = false;
            isResizingLog = false;
            isResizingLeft = false;
            isResizingRight = false;
        }
        /// <summary>
        /// Serializes the active canvas functoids and wire links into an XML string.
        /// </summary>
        public string SaveConfiguration() {
            var state = new MappingProjectState {
                ActiveFunctoids = this.ActiveFunctoids,
                Connections = this.Connections
            };

            var serializer = new XmlSerializer(typeof(MappingProjectState));
            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });

            serializer.Serialize(xmlWriter, state);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Deserializes an XML map configuration string and completely rehydrates the canvas layout.
        /// </summary>
        public void LoadConfiguration(string xmlContent) {
            if (string.IsNullOrWhiteSpace(xmlContent)) return;

            try {
                var serializer = new XmlSerializer(typeof(MappingProjectState));
                using var stringReader = new StringReader(xmlContent);

                if (serializer.Deserialize(stringReader) is MappingProjectState loadedState) {
                    // Clear existing layout records
                    this.ActiveFunctoids.Clear();
                    this.Connections.Clear();

                    // Rehydrate collections
                    if (loadedState.ActiveFunctoids != null) {
                        this.ActiveFunctoids.AddRange(loadedState.ActiveFunctoids);
                    }

                    if (loadedState.Connections != null) {
                        // Rehydrate the input index lookup safety map
                        foreach (var conn in loadedState.Connections) {
                            if (conn.Target != null) {
                                // Sync our InputIndex property helper back to ArgumentIndex
                                conn.Target.InputIndex = conn.Target.ArgumentIndex;
                            }
                        }
                        this.Connections.AddRange(loadedState.Connections);
                    }

                    // Force SkiaSharp to repaint the newly hydrated layout elements immediately
                    this.Invalidate();
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[REHYDRATE ERROR] Failed to load layout map XML: {ex.Message}");
                throw;
            }
        }

    }
}