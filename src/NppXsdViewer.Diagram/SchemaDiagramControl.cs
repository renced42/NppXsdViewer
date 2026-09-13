using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using NppXsdViewer.Schema.Model;

namespace NppXsdViewer.Diagram;

/// <summary>
/// Read-only XSD diagram control. Nodes are collapsed by default and can be expanded
/// through compositor/base-type capsules. It also provides schema-path actions.
/// </summary>
public sealed class SchemaDiagramControl : ScrollableControl
{
    private const int NodeWidth = 270;
    private const int HeaderHeight = 30;
    private const int TypeHeight = 24;
    private const int HorizontalGap = 220;
    private const int VerticalGap = 18;
    private const int MarginSize = 28;
    private const int CapsuleHeight = 26;
    private const int CapsuleGap = 10;
    private const int CapsuleInnerGap = 6;
    private const int CapsuleHorizontalPadding = 10;
    private const int CapsuleToggleWidth = 20;
    private const int CapsuleMinWidth = 58;

    private readonly List<NodeVisual> nodes = new List<NodeVisual>();
    private readonly HashSet<string> expandedCompositors = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> expandedBaseTypes = new HashSet<string>(StringComparer.Ordinal);
    private readonly ContextMenuStrip nodeMenu = new ContextMenuStrip();
    private readonly ToolTip nodeToolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 150 };

    private SchemaModel? model;
    private string? rootElementName;
    private string? rootTypeName;
    private float zoom = 1f;
    private NodeVisual? selectedNode;
    private NodeVisual? contextNode;
    private string lastHoverKey = string.Empty;

    public SchemaDiagramControl()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = SystemColors.Window;
        MouseWheel += OnMouseWheelZoom;

        nodeMenu.Items.Add("Copy schema path", null, (_, _) => CopySelectedSchemaPath());
        nodeMenu.Items.Add("Go to type definition", null, (_, _) => RequestTypeDefinition());
        nodeMenu.Opening += (_, e) =>
        {
            var hasNode = contextNode != null;
            nodeMenu.Items[0].Enabled = hasNode;
            nodeMenu.Items[1].Enabled = hasNode && contextNode != null && !string.IsNullOrWhiteSpace(contextNode.Element.TypeName);
            e.Cancel = !hasNode;
        };
    }

    public event EventHandler<SchemaLocationEventArgs>? NavigateRequested;
    public event EventHandler<SchemaSelectionEventArgs>? SelectionChanged;
    public event EventHandler<SchemaTypeRequestEventArgs>? TypeDefinitionRequested;

    public string CurrentRootCaption
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(rootElementName)) return rootElementName!;
            if (!string.IsNullOrWhiteSpace(rootTypeName)) return ShortName(rootTypeName!);
            return string.Empty;
        }
    }

    public void SetSchema(SchemaModel schema, string? rootName = null)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        if (model == null || !string.Equals(model.SourcePath, schema.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            expandedCompositors.Clear();
            expandedBaseTypes.Clear();
        }

        model = schema;
        rootElementName = rootName ?? schema.GlobalElements.FirstOrDefault()?.Name;
        rootTypeName = null;
        selectedNode = null;
        RebuildLayout();
    }

    public void SetRoot(string rootName)
    {
        rootElementName = rootName;
        rootTypeName = null;
        ResetExpansion();
    }

    public void SetRootType(string qualifiedTypeName)
    {
        rootElementName = null;
        rootTypeName = qualifiedTypeName;
        ResetExpansion();
    }

    public void CollapseAll()
    {
        expandedCompositors.Clear();
        expandedBaseTypes.Clear();
        RebuildLayout();
    }

    public void ExpandAll()
    {
        var root = CreateRootElement();
        if (model == null || root == null)
            return;

        expandedCompositors.Clear();
        expandedBaseTypes.Clear();
        MarkExpandedRecursive(root, root.Name, new HashSet<string>(StringComparer.Ordinal));
        RebuildLayout();
    }

    public void ResetZoom()
    {
        zoom = 1f;
        AutoScrollPosition = Point.Empty;
        UpdateScrollSize();
        Invalidate();
    }

    /// <summary>
    /// Expands the ancestors required to reveal a schema element path and selects the target node.
    /// The path uses the same slash-separated form shown by the viewer, for example Root/Group/Field.
    /// </summary>
    public bool RevealPath(string schemaPath, bool navigateToSource)
    {
        if (model == null || string.IsNullOrWhiteSpace(rootElementName) || string.IsNullOrWhiteSpace(schemaPath))
            return false;

        var parts = schemaPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], rootElementName, StringComparison.Ordinal))
            return false;

        var current = model.GlobalElements.FirstOrDefault(e => string.Equals(e.Name, rootElementName, StringComparison.Ordinal));
        if (current == null)
            return false;

        var visualPath = current.Name;
        for (var partIndex = 1; partIndex < parts.Length; partIndex++)
        {
            if (!model.Types.TryGetValue(current.TypeName, out var currentType))
                return false;

            var childIndex = -1;
            SchemaElementModel? child = null;
            for (var i = 0; i < currentType.Elements.Count; i++)
            {
                if (string.Equals(currentType.Elements[i].Name, parts[partIndex], StringComparison.Ordinal))
                {
                    childIndex = i;
                    child = currentType.Elements[i];
                    break;
                }
            }

            if (child == null || childIndex < 0)
                return false;

            var nodeKey = CreateNodeKey(current, visualPath);
            expandedCompositors.Add(nodeKey + "|content");
            visualPath += "/" + child.Name + "#" + childIndex;
            current = child;
        }

        RebuildLayout();
        selectedNode = nodes.FirstOrDefault(n => string.Equals(n.DisplayPath, schemaPath, StringComparison.Ordinal));
        if (selectedNode == null)
            return false;

        CenterNode(selectedNode);
        RaiseSelection(selectedNode);
        Invalidate();
        if (navigateToSource && selectedNode.SourceLine is int line)
            NavigateRequested?.Invoke(this, new SchemaLocationEventArgs(line, selectedNode.Element.SourceUri));
        return true;
    }

    private void CenterNode(NodeVisual node)
    {
        var centerX = (node.Bounds.Left + node.Bounds.Width / 2f) * zoom;
        var centerY = (node.Bounds.Top + node.Bounds.Height / 2f) * zoom;
        var scrollX = Math.Max(0, (int)Math.Round(centerX - ClientSize.Width / 2f));
        var scrollY = Math.Max(0, (int)Math.Round(centerY - ClientSize.Height / 2f));
        AutoScrollPosition = new Point(scrollX, scrollY);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.ScaleTransform(zoom, zoom);
            e.Graphics.TranslateTransform(AutoScrollPosition.X / zoom, AutoScrollPosition.Y / zoom);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawConnections(e.Graphics);
            foreach (var node in nodes)
                DrawNode(e.Graphics, node, ReferenceEquals(node, selectedNode));
        }
        finally
        {
            e.Graphics.Restore(state);
        }

    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var logical = ToLogical(e.Location);
        var node = HitTest(logical);

        if (e.Button == MouseButtons.Right)
        {
            contextNode = node;
            if (node != null)
            {
                selectedNode = node;
                RaiseSelection(node);
                Invalidate();
                nodeMenu.Show(this, e.Location);
            }
            return;
        }

        if (e.Button != MouseButtons.Left || node == null)
            return;

        selectedNode = node;
        RaiseSelection(node);

        if (node.CompositorBubbleBounds.Contains(logical) && node.HasCompositor)
        {
            ToggleState(expandedCompositors, node.CompositorKey);
            RebuildLayout();
            return;
        }

        if (node.BaseBubbleBounds.Contains(logical) && node.HasBaseType)
        {
            ToggleState(expandedBaseTypes, node.BaseKey);
            RebuildLayout();
            return;
        }

        Invalidate();
        if (node.SourceLine is int line)
            NavigateRequested?.Invoke(this, new SchemaLocationEventArgs(line, node.Element.SourceUri));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var logical = ToLogical(e.Location);
        var hoverNode = HitTest(logical);
        var interactive = hoverNode != null && (hoverNode.CompositorBubbleBounds.Contains(logical) || hoverNode.BaseBubbleBounds.Contains(logical));
        Cursor = interactive ? Cursors.Hand : Cursors.Default;

        if (hoverNode == null)
        {
            if (lastHoverKey.Length > 0) nodeToolTip.Hide(this);
            lastHoverKey = string.Empty;
            return;
        }

        if (!string.Equals(lastHoverKey, hoverNode.Key, StringComparison.Ordinal))
        {
            lastHoverKey = hoverNode.Key;
            var documentation = !string.IsNullOrWhiteSpace(hoverNode.Element.Documentation)
                ? hoverNode.Element.Documentation
                : hoverNode.Type?.Documentation ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(documentation))
            {
                var preview = documentation.Length > 420 ? documentation.Substring(0, 417) + "..." : documentation;
                nodeToolTip.Show(preview, this, e.Location.X + 14, e.Location.Y + 18, 7000);
            }
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        lastHoverKey = string.Empty;
        nodeToolTip.Hide(this);
    }

    private void ResetExpansion()
    {
        expandedCompositors.Clear();
        expandedBaseTypes.Clear();
        selectedNode = null;
        zoom = 1f;
        AutoScrollPosition = Point.Empty;
        RebuildLayout();
    }

    private NodeVisual? HitTest(PointF logical)
        => nodes.FirstOrDefault(n => n.Bounds.Contains(logical)
                                     || n.CompositorBubbleBounds.Contains(logical)
                                     || n.BaseBubbleBounds.Contains(logical));

    private void RaiseSelection(NodeVisual node)
        => SelectionChanged?.Invoke(this,
            new SchemaSelectionEventArgs(node.Element, node.Type, node.DisplayPath));

    private void CopySelectedSchemaPath()
    {
        if (contextNode == null || string.IsNullOrWhiteSpace(contextNode.DisplayPath)) return;
        Clipboard.SetText(contextNode.DisplayPath);
    }

    private void RequestTypeDefinition()
    {
        if (contextNode == null || string.IsNullOrWhiteSpace(contextNode.Element.TypeName)) return;
        TypeDefinitionRequested?.Invoke(this, new SchemaTypeRequestEventArgs(contextNode.Element.TypeName));
    }

    private PointF ToLogical(Point point)
        => new PointF((point.X - AutoScrollPosition.X) / zoom, (point.Y - AutoScrollPosition.Y) / zoom);

    private static void ToggleState(ISet<string> values, string key)
    {
        if (!values.Add(key)) values.Remove(key);
    }

    private SchemaElementModel? CreateRootElement()
    {
        if (model == null) return null;
        if (!string.IsNullOrWhiteSpace(rootElementName))
            return model.GlobalElements.FirstOrDefault(e => string.Equals(e.Name, rootElementName, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(rootTypeName) && model.Types.TryGetValue(rootTypeName!, out var type))
        {
            return new SchemaElementModel
            {
                Name = type.Name,
                QualifiedName = type.QualifiedName,
                TypeName = type.QualifiedName,
                MinOccurs = 1,
                MaxOccurs = "1",
                Documentation = type.Documentation,
                SourceLine = type.SourceLine,
                SourceUri = type.SourceUri
            };
        }

        return null;
    }

    private void RebuildLayout()
    {
        nodes.Clear();
        var root = CreateRootElement();
        if (model == null || root == null)
        {
            AutoScrollMinSize = Size.Empty;
            Invalidate();
            return;
        }

        var visualRoot = BuildElementNode(root, new HashSet<string>(StringComparer.Ordinal), root.Name, root.Name);
        MeasureSubtree(visualRoot);
        LayoutSubtree(visualRoot, 0, MarginSize);
        CollectNodes(visualRoot);

        if (selectedNode != null)
            selectedNode = nodes.FirstOrDefault(n => n.Key == selectedNode.Key);

        UpdateScrollSize();
        Invalidate();
    }

    private NodeVisual BuildElementNode(
        SchemaElementModel element,
        HashSet<string> pathTypes,
        string visualPath,
        string displayPath)
    {
        model!.Types.TryGetValue(element.TypeName, out var type);
        var nodeKey = CreateNodeKey(element, visualPath);
        var compositorKey = nodeKey + "|content";
        var baseKey = nodeKey + "|base";
        var compositorExpanded = expandedCompositors.Contains(compositorKey);
        var baseExpanded = expandedBaseTypes.Contains(baseKey);

        var hasCompositor = type != null
                            && type.Elements.Count > 0
                            && type.Compositor != SchemaCompositor.None
                            && type.Compositor != SchemaCompositor.Simple;
        var hasBaseType = type != null
                          && !string.IsNullOrWhiteSpace(type.BaseTypeName)
                          && ShortName(type.BaseTypeName) != "anyType"
                          && model.Types.ContainsKey(type.BaseTypeName);

        var node = new NodeVisual(
            nodeKey, compositorKey, baseKey, element, type, hasCompositor,
            type?.Compositor ?? SchemaCompositor.None, compositorExpanded,
            hasBaseType, baseExpanded, element.SourceLine ?? type?.SourceLine, displayPath);

        if (type == null || !pathTypes.Add(type.QualifiedName))
            return node;

        var childNumber = 0;
        if (baseExpanded && hasBaseType && model.Types.TryGetValue(type.BaseTypeName, out var baseType))
        {
            if (!pathTypes.Contains(type.BaseTypeName))
            {
                var synthetic = new SchemaElementModel
                {
                    Name = "base: " + ShortName(type.BaseTypeName),
                    TypeName = type.BaseTypeName,
                    MinOccurs = 1,
                    MaxOccurs = "1",
                    SourceLine = baseType.SourceLine,
                    SourceUri = baseType.SourceUri,
                    Documentation = baseType.Documentation
                };
                var childNode = BuildElementNode(
                    synthetic,
                    pathTypes,
                    visualPath + "/base#" + childNumber++,
                    displayPath + "/base(" + ShortName(type.BaseTypeName) + ")");
                node.BaseChild = childNode;
                node.Children.Add(childNode);
            }
        }

        if (compositorExpanded && hasCompositor)
        {
            foreach (var child in type.Elements)
            {
                if (pathTypes.Contains(child.TypeName)) continue;
                var childNode = BuildElementNode(
                    child,
                    pathTypes,
                    visualPath + "/" + child.Name + "#" + childNumber++,
                    displayPath + "/" + child.Name);
                node.CompositorChildren.Add(childNode);
                node.Children.Add(childNode);
            }
        }

        pathTypes.Remove(type.QualifiedName);
        return node;
    }

    private void MarkExpandedRecursive(SchemaElementModel element, string visualPath, HashSet<string> pathTypes)
    {
        if (model == null || !model.Types.TryGetValue(element.TypeName, out var type)) return;
        if (!pathTypes.Add(type.QualifiedName)) return;

        var nodeKey = CreateNodeKey(element, visualPath);
        if (type.Elements.Count > 0 && type.Compositor != SchemaCompositor.None && type.Compositor != SchemaCompositor.Simple)
            expandedCompositors.Add(nodeKey + "|content");

        var childNumber = 0;
        if (!string.IsNullOrWhiteSpace(type.BaseTypeName)
            && ShortName(type.BaseTypeName) != "anyType"
            && model.Types.TryGetValue(type.BaseTypeName, out var baseType)
            && !pathTypes.Contains(type.BaseTypeName))
        {
            expandedBaseTypes.Add(nodeKey + "|base");
            var synthetic = new SchemaElementModel
            {
                Name = "base: " + ShortName(type.BaseTypeName),
                TypeName = type.BaseTypeName,
                MinOccurs = 1,
                MaxOccurs = "1",
                SourceLine = baseType.SourceLine,
                SourceUri = baseType.SourceUri
            };
            MarkExpandedRecursive(synthetic, visualPath + "/base#" + childNumber++, pathTypes);
        }

        foreach (var child in type.Elements)
        {
            if (!pathTypes.Contains(child.TypeName))
                MarkExpandedRecursive(child, visualPath + "/" + child.Name + "#" + childNumber, pathTypes);
            childNumber++;
        }

        pathTypes.Remove(type.QualifiedName);
    }

    private static string CreateNodeKey(SchemaElementModel element, string visualPath)
        => (!string.IsNullOrWhiteSpace(element.QualifiedName) ? element.QualifiedName : element.TypeName) + "|" + visualPath;

    private static float MeasureSubtree(NodeVisual node)
    {
        node.NodeHeight = HeaderHeight + TypeHeight;
        if (node.Children.Count == 0)
        {
            node.SubtreeHeight = node.NodeHeight;
            return node.SubtreeHeight;
        }

        var childrenHeight = node.Children.Sum(MeasureSubtree) + (node.Children.Count - 1) * VerticalGap;
        node.SubtreeHeight = Math.Max(node.NodeHeight, childrenHeight);
        return node.SubtreeHeight;
    }

    private static void LayoutSubtree(NodeVisual node, int depth, float top)
    {
        var x = MarginSize + depth * (NodeWidth + HorizontalGap);
        var y = top + (node.SubtreeHeight - node.NodeHeight) / 2f;
        node.Bounds = new RectangleF(x, y, NodeWidth, node.NodeHeight);

        var capsuleY = node.Bounds.Top + node.NodeHeight / 2f - CapsuleHeight / 2f;
        var nextX = node.Bounds.Right + CapsuleGap;

        var compositorWidth = node.HasCompositor ? MeasureCapsuleWidth(CompositorLabel(node.CompositorKind)) : 0f;
        node.CompositorBubbleBounds = node.HasCompositor
            ? new RectangleF(nextX, capsuleY, compositorWidth, CapsuleHeight)
            : RectangleF.Empty;
        if (node.HasCompositor) nextX += compositorWidth + CapsuleInnerGap;

        var baseWidth = node.HasBaseType ? MeasureCapsuleWidth("extends") : 0f;
        node.BaseBubbleBounds = node.HasBaseType
            ? new RectangleF(nextX, capsuleY, baseWidth, CapsuleHeight)
            : RectangleF.Empty;

        if (node.Children.Count == 0) return;
        var totalChildrenHeight = node.Children.Sum(c => c.SubtreeHeight) + (node.Children.Count - 1) * VerticalGap;
        var childTop = top + (node.SubtreeHeight - totalChildrenHeight) / 2f;
        foreach (var child in node.Children)
        {
            LayoutSubtree(child, depth + 1, childTop);
            childTop += child.SubtreeHeight + VerticalGap;
        }
    }

    private void CollectNodes(NodeVisual node)
    {
        nodes.Add(node);
        foreach (var child in node.Children) CollectNodes(child);
    }

    private void UpdateScrollSize()
    {
        if (nodes.Count == 0)
        {
            AutoScrollMinSize = Size.Empty;
            return;
        }

        var maxRight = nodes.Max(n => Math.Max(n.Bounds.Right, Math.Max(n.CompositorBubbleBounds.Right, n.BaseBubbleBounds.Right))) + MarginSize;
        var maxBottom = nodes.Max(n => n.Bounds.Bottom) + MarginSize;
        AutoScrollMinSize = new Size((int)Math.Ceiling(maxRight * zoom), (int)Math.Ceiling(maxBottom * zoom));
    }

    private void DrawConnections(Graphics graphics)
    {
        using var pen = new Pen(SystemColors.ControlDark, 1f);
        foreach (var node in nodes)
        {
            if (node.HasCompositor)
            {
                DrawBubbleLead(graphics, pen, node.Bounds, node.CompositorBubbleBounds);
                if (node.IsCompositorExpanded)
                    foreach (var target in node.CompositorChildren)
                        DrawOrthogonalConnection(graphics, pen, node.CompositorBubbleBounds, target.Bounds);
            }

            if (node.HasBaseType)
            {
                DrawBubbleLead(graphics, pen, node.Bounds, node.BaseBubbleBounds);
                if (node.IsBaseExpanded && node.BaseChild != null)
                    DrawOrthogonalConnection(graphics, pen, node.BaseBubbleBounds, node.BaseChild.Bounds);
            }
        }
    }

    private static void DrawBubbleLead(Graphics graphics, Pen pen, RectangleF node, RectangleF bubble)
    {
        var y = bubble.Top + bubble.Height / 2f;
        graphics.DrawLine(pen, node.Right, y, bubble.Left, y);
    }

    private static void DrawOrthogonalConnection(Graphics graphics, Pen pen, RectangleF bubble, RectangleF target)
    {
        var start = new PointF(bubble.Right, bubble.Top + bubble.Height / 2f);
        var end = new PointF(target.Left, target.Top + HeaderHeight / 2f);
        var bendX = start.X + Math.Max(24f, (end.X - start.X) * 0.42f);
        using var path = new GraphicsPath();
        path.AddLine(start, new PointF(bendX, start.Y));
        path.AddLine(new PointF(bendX, start.Y), new PointF(bendX, end.Y));
        path.AddLine(new PointF(bendX, end.Y), end);
        graphics.DrawPath(pen, path);
        using var brush = new SolidBrush(SystemColors.ControlDarkDark);
        graphics.FillEllipse(brush, end.X - 2.5f, end.Y - 2.5f, 5f, 5f);
    }

    private static void DrawNode(Graphics graphics, NodeVisual node, bool selected)
    {
        using var backgroundBrush = new SolidBrush(SystemColors.Window);
        using var headerBrush = new SolidBrush(selected ? SystemColors.GradientActiveCaption : SystemColors.ControlLight);
        using var typeBrush = new SolidBrush(SystemColors.Control);
        var optional = node.Element.MinOccurs == 0m;
        using var borderPen = new Pen(selected ? SystemColors.Highlight : SystemColors.ControlDark, selected ? 2f : 1f);
        if (optional)
            borderPen.DashStyle = DashStyle.Dot;
        using var titleFont = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
        using var typeFont = new Font(SystemFonts.MessageBoxFont, FontStyle.Italic);

        graphics.FillRectangle(backgroundBrush, node.Bounds);
        graphics.FillRectangle(headerBrush, node.Bounds.Left, node.Bounds.Top, node.Bounds.Width, HeaderHeight);
        graphics.FillRectangle(typeBrush, node.Bounds.Left, node.Bounds.Top + HeaderHeight, node.Bounds.Width, TypeHeight);
        graphics.DrawRectangle(borderPen, node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);

        var occurrence = FormatOccurrence(node.Element);
        var occurrenceWidth = MeasureBadgeWidth(graphics, occurrence);
        var optionalWidth = optional ? MeasureBadgeWidth(graphics, "optional") : 0f;
        var titleRightReserve = occurrenceWidth + 18f;
        var typeRightReserve = optional ? optionalWidth + 18f : 8f;

        DrawClippedText(graphics, node.Element.Name, titleFont, SystemColors.ControlText,
            new RectangleF(node.Bounds.Left + 8, node.Bounds.Top + 2,
                Math.Max(24f, node.Bounds.Width - 8 - titleRightReserve), HeaderHeight - 4));
        DrawClippedText(graphics, TypeCaption(node), typeFont, SystemColors.ControlText,
            new RectangleF(node.Bounds.Left + 8, node.Bounds.Top + HeaderHeight,
                Math.Max(24f, node.Bounds.Width - 8 - typeRightReserve), TypeHeight));

        DrawBadge(graphics, occurrence, node.Bounds.Right - 8, node.Bounds.Top + 5, BadgeKind.Occurrence);
        if (optional)
            DrawBadge(graphics, "optional", node.Bounds.Right - 8, node.Bounds.Top + HeaderHeight + 3, BadgeKind.Optional);

        if (node.HasCompositor)
            DrawCapsule(graphics, node.CompositorBubbleBounds, CompositorLabel(node.CompositorKind), node.IsCompositorExpanded);
        if (node.HasBaseType)
            DrawCapsule(graphics, node.BaseBubbleBounds, "extends", node.IsBaseExpanded);
    }

    private static string FormatOccurrence(SchemaElementModel element)
    {
        var min = element.MinOccurs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var max = element.MaxOccurs == "unbounded" ? "*" : element.MaxOccurs;
        return element.MinOccurs == 1m && max == "1" ? "[1]" : "[" + min + ".." + max + "]";
    }

    private static float MeasureBadgeWidth(Graphics graphics, string text)
    {
        using var font = new Font(SystemFonts.MessageBoxFont.FontFamily, 7f, FontStyle.Regular);
        var size = graphics.MeasureString(text, font);
        return Math.Max(30f, size.Width + 10f);
    }

    private static void DrawBadge(Graphics graphics, string text, float right, float top, BadgeKind kind)
    {
        using var font = new Font(SystemFonts.MessageBoxFont.FontFamily, 7f, FontStyle.Regular);
        var width = MeasureBadgeWidth(graphics, text);
        var bounds = new RectangleF(right - width, top, width, 17f);
        using var fill = new SolidBrush(kind == BadgeKind.Optional ? SystemColors.ControlLightLight : SystemColors.Info);
        using var pen = new Pen(SystemColors.ControlDark);
        graphics.FillRectangle(fill, bounds);
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        DrawClippedText(graphics, text, font,
            kind == BadgeKind.Optional ? SystemColors.ControlText : SystemColors.InfoText, bounds);
    }

    private enum BadgeKind
    {
        Occurrence,
        Optional
    }

    private static string TypeCaption(NodeVisual node)
    {
        if (node.Type == null) return ShortName(node.Element.TypeName);
        if (node.Element.TypeName.StartsWith("@anonymous:", StringComparison.Ordinal)) return "anonymous complexType";
        return string.IsNullOrWhiteSpace(node.Type.Name) ? ShortName(node.Element.TypeName) : node.Type.Name;
    }

    private static string CompositorLabel(SchemaCompositor compositor)
        => compositor switch
        {
            SchemaCompositor.Sequence => "sequence",
            SchemaCompositor.Choice => "choice",
            SchemaCompositor.All => "all",
            _ => "compositor"
        };

    private static float MeasureCapsuleWidth(string label)
    {
        using var font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8f, FontStyle.Bold);
        var measured = TextRenderer.MeasureText(label, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return Math.Max(CapsuleMinWidth, measured.Width + CapsuleHorizontalPadding * 2 + CapsuleToggleWidth);
    }

    private static void DrawCapsule(Graphics graphics, RectangleF bounds, string label, bool expanded)
    {
        using var fill = new SolidBrush(SystemColors.Window);
        using var pen = new Pen(SystemColors.ControlDarkDark, 1f);
        using var font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8f, FontStyle.Bold);
        using var textBrush = new SolidBrush(SystemColors.ControlText);
        using var capsulePath = CreateRoundedRectangle(bounds, bounds.Height / 2f);
        graphics.FillPath(fill, capsulePath);
        graphics.DrawPath(pen, capsulePath);

        var toggleLeft = bounds.Right - CapsuleToggleWidth;
        graphics.DrawLine(pen, toggleLeft, bounds.Top + 3f, toggleLeft, bounds.Bottom - 3f);
        var textBounds = new RectangleF(bounds.Left + CapsuleHorizontalPadding, bounds.Top,
            Math.Max(0f, bounds.Width - CapsuleToggleWidth - CapsuleHorizontalPadding * 2), bounds.Height);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(label, font, textBrush, textBounds, format);

        var centerX = toggleLeft + CapsuleToggleWidth / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        const float markerHalf = 4f;
        graphics.DrawLine(pen, centerX - markerHalf, centerY, centerX + markerHalf, centerY);
        if (!expanded) graphics.DrawLine(pen, centerX, centerY - markerHalf, centerX, centerY + markerHalf);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height));
        var arc = new RectangleF(bounds.Left, bounds.Top, diameter, diameter);
        var path = new GraphicsPath();
        path.AddArc(arc, 90, 180);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 180);
        path.CloseFigure();
        return path;
    }

    private static void DrawClippedText(Graphics graphics, string text, Font font, Color color, RectangleF bounds)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(text ?? string.Empty, font, brush, bounds, format);
    }

    private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) != Keys.Control) return;
        var logicalBefore = ToLogical(e.Location);
        var oldZoom = zoom;
        zoom = Math.Max(0.35f, Math.Min(2.5f, zoom + (e.Delta > 0 ? 0.1f : -0.1f)));
        if (Math.Abs(oldZoom - zoom) < 0.001f) return;

        UpdateScrollSize();
        var targetX = Math.Max(0, (int)Math.Round(logicalBefore.X * zoom - e.X));
        var targetY = Math.Max(0, (int)Math.Round(logicalBefore.Y * zoom - e.Y));
        AutoScrollPosition = new Point(targetX, targetY);
        Invalidate();
    }

    private static string ShortName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName)) return "(anonymous)";
        if (qualifiedName.StartsWith("@anonymous:", StringComparison.Ordinal)) return "anonymous";
        var closingBrace = qualifiedName.LastIndexOf('}');
        return closingBrace >= 0 ? qualifiedName.Substring(closingBrace + 1) : qualifiedName;
    }

    private sealed class NodeVisual
    {
        public NodeVisual(
            string key,
            string compositorKey,
            string baseKey,
            SchemaElementModel element,
            SchemaTypeModel? type,
            bool hasCompositor,
            SchemaCompositor compositorKind,
            bool compositorExpanded,
            bool hasBaseType,
            bool baseExpanded,
            int? sourceLine,
            string displayPath)
        {
            Key = key;
            CompositorKey = compositorKey;
            BaseKey = baseKey;
            Element = element;
            Type = type;
            HasCompositor = hasCompositor;
            CompositorKind = compositorKind;
            IsCompositorExpanded = compositorExpanded;
            HasBaseType = hasBaseType;
            IsBaseExpanded = baseExpanded;
            SourceLine = sourceLine;
            DisplayPath = displayPath;
        }

        public string Key { get; }
        public string CompositorKey { get; }
        public string BaseKey { get; }
        public SchemaElementModel Element { get; }
        public SchemaTypeModel? Type { get; }
        public bool HasCompositor { get; }
        public SchemaCompositor CompositorKind { get; }
        public bool IsCompositorExpanded { get; }
        public bool HasBaseType { get; }
        public bool IsBaseExpanded { get; }
        public int? SourceLine { get; }
        public string DisplayPath { get; }
        public List<NodeVisual> Children { get; } = new List<NodeVisual>();
        public List<NodeVisual> CompositorChildren { get; } = new List<NodeVisual>();
        public NodeVisual? BaseChild { get; set; }
        public RectangleF Bounds { get; set; }
        public RectangleF CompositorBubbleBounds { get; set; }
        public RectangleF BaseBubbleBounds { get; set; }
        public float NodeHeight { get; set; }
        public float SubtreeHeight { get; set; }
    }
}

public sealed class SchemaLocationEventArgs : EventArgs
{
    public SchemaLocationEventArgs(int lineNumber, string sourceUri = "")
    {
        LineNumber = lineNumber;
        SourceUri = sourceUri ?? string.Empty;
    }

    public int LineNumber { get; }
    public string SourceUri { get; }
}

public sealed class SchemaSelectionEventArgs : EventArgs
{
    public SchemaSelectionEventArgs(SchemaElementModel element, SchemaTypeModel? type, string schemaPath)
    {
        Element = element;
        Type = type;
        SchemaPath = schemaPath;
    }

    public SchemaElementModel Element { get; }
    public SchemaTypeModel? Type { get; }
    public string SchemaPath { get; }
}

public sealed class SchemaTypeRequestEventArgs : EventArgs
{
    public SchemaTypeRequestEventArgs(string qualifiedTypeName) => QualifiedTypeName = qualifiedTypeName;
    public string QualifiedTypeName { get; }
}
