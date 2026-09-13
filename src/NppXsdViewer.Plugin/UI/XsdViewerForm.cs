using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NppXsdViewer.Diagram;
using NppXsdViewer.Schema.Model;
using NppXsdViewer.Schema.Parsing;

namespace NppXsdViewer.Plugin.UI;

internal sealed class XsdViewerForm : Form
{
    private readonly Button historyBackButton = new Button { Text = "←", Width = 32, Enabled = false };
    private readonly Button historyForwardButton = new Button { Text = "→", Width = 32, Enabled = false };
    private readonly Button overviewButton = new Button { Text = "Components", AutoSize = true };
    private readonly ComboBox rootSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button refreshButton = new Button { Text = "Refresh", AutoSize = true };
    private readonly Button expandAllButton = new Button { Text = "Expand all", AutoSize = true, Visible = false };
    private readonly Button collapseButton = new Button { Text = "Collapse all", AutoSize = true, Visible = false };
    private readonly Button resetZoomButton = new Button { Text = "100%", AutoSize = true, Visible = false };
    private readonly Label breadcrumbLabel = new Label { Dock = DockStyle.Top, Height = 25, AutoEllipsis = true, Padding = new Padding(6, 4, 6, 2), Text = "Schema components" };
    private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel("No XSD loaded.");

    private readonly TextBox searchBox = new TextBox { Dock = DockStyle.Top };
    private readonly TreeView componentTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly SplitContainer mainSplit = new SplitContainer
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Vertical,
        FixedPanel = FixedPanel.Panel1,
        SplitterWidth = 5
    };

    private readonly ListView overviewList = new ListView
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HideSelection = false,
        MultiSelect = false
    };

    private readonly SchemaDiagramControl diagram = new SchemaDiagramControl { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel contentPanel = new Panel { Dock = DockStyle.Fill };
    private readonly SplitContainer diagramSplit = new SplitContainer
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Vertical,
        FixedPanel = FixedPanel.Panel2,
        SplitterWidth = 5,
        Visible = false
    };

    private readonly TabControl propertyTabs = new TabControl { Dock = DockStyle.Fill };
    private readonly ListView generalView = CreatePropertyList();
    private readonly ListView patternView = CreateSingleValueList("Pattern");
    private readonly ListView enumView = CreateSingleValueList("Value");
    private readonly ListView facetView = CreatePropertyList();
    private readonly ListView attributeView = CreateAttributeList();
    private readonly TextBox documentationBox = new TextBox
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = true
    };
    private readonly ListView referencesView = CreateReferencesList();
    private readonly TreeView dependencyTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowNodeToolTips = true };
    private readonly ListView problemsView = CreateProblemsList();

    private readonly XsdSchemaLoader loader = new XsdSchemaLoader();
    private readonly List<NavigationEntry> history = new List<NavigationEntry>();
    private SchemaModel? currentModel;
    private int historyIndex = -1;
    private bool updatingRootSelector;
    private bool navigatingHistory;

    public XsdViewerForm()
    {
        Text = "XSD Schema Viewer";
        FormBorderStyle = FormBorderStyle.None;
        TopLevel = false;

        overviewList.Columns.Add("Component", 250);
        overviewList.Columns.Add("Kind", 130);
        overviewList.Columns.Add("Type / namespace", 280);
        overviewList.Columns.Add("Documentation", 520);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(4),
            WrapContents = false,
            AutoScroll = true
        };
        toolbar.Controls.Add(historyBackButton);
        toolbar.Controls.Add(historyForwardButton);
        toolbar.Controls.Add(overviewButton);
        toolbar.Controls.Add(new Label { Text = "Root element:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        toolbar.Controls.Add(rootSelector);
        toolbar.Controls.Add(collapseButton);
        toolbar.Controls.Add(expandAllButton);
        toolbar.Controls.Add(resetZoomButton);
        toolbar.Controls.Add(refreshButton);

        ConfigurePropertyTabs();
        diagramSplit.Panel1.Controls.Add(diagram);
        diagramSplit.Panel2.Controls.Add(propertyTabs);
        contentPanel.Controls.Add(diagramSplit);
        contentPanel.Controls.Add(overviewList);

        var browserPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var searchLabel = new Label { Text = "Search schema:", Dock = DockStyle.Top, Height = 22, Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont, System.Drawing.FontStyle.Bold) };
        browserPanel.Controls.Add(componentTree);
        browserPanel.Controls.Add(searchBox);
        browserPanel.Controls.Add(searchLabel);

        mainSplit.Panel1.Controls.Add(browserPanel);
        mainSplit.Panel2.Controls.Add(contentPanel);

        var status = new StatusStrip();
        status.Items.Add(statusLabel);

        Controls.Add(mainSplit);
        Controls.Add(breadcrumbLabel);
        Controls.Add(toolbar);
        Controls.Add(status);

        rootSelector.SelectionChangeCommitted += (_, _) =>
        {
            if (!updatingRootSelector && rootSelector.SelectedItem is string root && currentModel != null)
                ShowElementDiagram(root, true);
        };

        overviewList.DoubleClick += (_, _) => OpenSelectedOverviewComponent();
        overviewList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                OpenSelectedOverviewComponent();
                e.Handled = true;
            }
        };

        componentTree.NodeMouseDoubleClick += (_, e) => OpenComponentNode(e.Node);
        searchBox.TextChanged += (_, _) => PopulateComponentBrowser(searchBox.Text);
        overviewButton.Click += (_, _) => ShowOverview(true);
        refreshButton.Click += (_, _) => ReloadRequested?.Invoke(this, EventArgs.Empty);
        collapseButton.Click += (_, _) => diagram.CollapseAll();
        expandAllButton.Click += (_, _) => diagram.ExpandAll();
        resetZoomButton.Click += (_, _) => diagram.ResetZoom();
        historyBackButton.Click += (_, _) => NavigateHistory(-1);
        historyForwardButton.Click += (_, _) => NavigateHistory(1);

        diagram.NavigateRequested += (_, e) => NavigateRequested?.Invoke(this, e);
        diagram.SelectionChanged += (_, e) =>
        {
            ShowProperties(e.Element, e.Type, e.SchemaPath);
            breadcrumbLabel.Text = e.SchemaPath;
        };
        diagram.TypeDefinitionRequested += (_, e) => OpenType(e.QualifiedTypeName, true);
        diagramSplit.SizeChanged += (_, _) => AdjustPropertyPanelWidth();
        mainSplit.SizeChanged += (_, _) => AdjustBrowserWidth();

        generalView.DoubleClick += (_, _) => OpenDefinitionFromGeneral();
        referencesView.DoubleClick += (_, _) => OpenSelectedReference();
        dependencyTree.NodeMouseDoubleClick += (_, e) => OpenDependencyNode(e.Node);
        problemsView.DoubleClick += (_, _) => OpenSelectedProblem();
    }

    public event EventHandler? ReloadRequested;
    public event EventHandler<SchemaLocationEventArgs>? NavigateRequested;
    public event EventHandler<SchemaResourceEventArgs>? OpenResourceRequested;

    public void ClearSchema(string message)
    {
        currentModel = null;
        history.Clear();
        historyIndex = -1;
        UpdateHistoryButtons();
        updatingRootSelector = true;
        try { rootSelector.Items.Clear(); }
        finally { updatingRootSelector = false; }

        overviewList.Items.Clear();
        componentTree.Nodes.Clear();
        diagram.SetSchema(new SchemaModel(), null);
        ClearProperties();
        ShowOverview(false);
        breadcrumbLabel.Text = "Schema components";
        statusLabel.Text = message;
    }

    public void LoadSchema(string path)
    {
        try
        {
            currentModel = loader.Load(path);
            history.Clear();
            historyIndex = -1;
            UpdateHistoryButtons();

            var roots = currentModel.GlobalElements.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            updatingRootSelector = true;
            try
            {
                rootSelector.Items.Clear();
                rootSelector.Items.AddRange(roots.Select(e => (object)e.Name).ToArray());
                rootSelector.SelectedIndex = -1;
            }
            finally { updatingRootSelector = false; }

            PopulateComponentBrowser(string.Empty);
            PopulateOverview();
            PopulateDependencies();
            PopulateProblems();
            diagram.SetSchema(currentModel, null);
            ClearProperties();
            ShowOverview(false);

            statusLabel.Text = $"{Path.GetFileName(path)} · {roots.Length} root elements · {currentModel.Types.Count} types · {currentModel.LoadedSchemas.Count} XSD files";
        }
        catch (Exception ex)
        {
            currentModel = null;
            componentTree.Nodes.Clear();
            overviewList.Items.Clear();
            diagram.SetSchema(new SchemaModel(), null);
            ClearProperties();
            ShowOverview(false);
            statusLabel.Text = "XSD error: " + ex.Message;
        }
    }

    private void ConfigurePropertyTabs()
    {
        propertyTabs.TabPages.Add(CreateTab("General", generalView));
        propertyTabs.TabPages.Add(CreateTab("Pattern", patternView));
        propertyTabs.TabPages.Add(CreateTab("Enumerations", enumView));
        propertyTabs.TabPages.Add(CreateTab("Restrictions", facetView));
        propertyTabs.TabPages.Add(CreateTab("Attributes", attributeView));
        propertyTabs.TabPages.Add(CreateTab("Documentation", documentationBox));
        propertyTabs.TabPages.Add(CreateTab("Used by", referencesView));
        propertyTabs.TabPages.Add(CreateTab("Dependencies", dependencyTree));
        propertyTabs.TabPages.Add(CreateTab("Problems", problemsView));
    }

    private static TabPage CreateTab(string title, Control control)
    {
        var tab = new TabPage(title);
        tab.Controls.Add(control);
        return tab;
    }

    private void PopulateComponentBrowser(string filter)
    {
        componentTree.BeginUpdate();
        try
        {
            componentTree.Nodes.Clear();
            if (currentModel == null) return;

            var groups = new[]
            {
                SchemaComponentKind.Element,
                SchemaComponentKind.ComplexType,
                SchemaComponentKind.SimpleType,
                SchemaComponentKind.Group,
                SchemaComponentKind.AttributeGroup,
                SchemaComponentKind.Attribute,
                SchemaComponentKind.Import,
                SchemaComponentKind.Include
            };

            foreach (var kind in groups)
            {
                var root = new TreeNode(ComponentKindCaption(kind));
                var matches = currentModel.Components
                    .Where(c => c.Kind == kind && MatchesSearch(c, filter))
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (var component in matches)
                {
                    var node = new TreeNode(component.Name) { Tag = component, ToolTipText = component.Documentation };
                    root.Nodes.Add(node);
                }

                if (root.Nodes.Count > 0)
                    componentTree.Nodes.Add(root);
            }

            if (!string.IsNullOrWhiteSpace(filter)) componentTree.ExpandAll();
        }
        finally { componentTree.EndUpdate(); }
    }

    private bool MatchesSearch(SchemaComponentModel component, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        var query = filter.Trim();
        if (Contains(component.Name, query) || Contains(component.QualifiedName, query)
            || Contains(component.TypeName, query) || Contains(component.Documentation, query)) return true;

        if (currentModel != null && currentModel.Types.TryGetValue(component.TypeName, out var type))
        {
            if (type.Patterns.Any(p => Contains(p, query))) return true;
            if (type.EnumerationValues.Any(v => Contains(v, query))) return true;
            if (type.Facets.Any(f => Contains(f.Name, query) || Contains(f.Value, query))) return true;
        }
        return false;
    }

    private static bool Contains(string value, string query)
        => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    private void PopulateOverview()
    {
        overviewList.BeginUpdate();
        try
        {
            overviewList.Items.Clear();
            if (currentModel == null) return;
            foreach (var component in currentModel.Components.Where(c => c.Kind != SchemaComponentKind.Import && c.Kind != SchemaComponentKind.Include))
            {
                var item = new ListViewItem(component.Name) { Tag = component, ToolTipText = component.Documentation };
                item.SubItems.Add(ComponentKindCaption(component.Kind));
                item.SubItems.Add(ShortName(component.TypeName));
                item.SubItems.Add(component.Documentation);
                overviewList.Items.Add(item);
            }
        }
        finally { overviewList.EndUpdate(); }
    }

    private void PopulateDependencies()
    {
        dependencyTree.BeginUpdate();
        try
        {
            dependencyTree.Nodes.Clear();
            if (currentModel == null) return;

            foreach (var sourceGroup in currentModel.Dependencies.GroupBy(d => d.SourceUri).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var sourceNode = new TreeNode(DisplayLocation(sourceGroup.Key)) { ToolTipText = sourceGroup.Key };
                foreach (var dep in sourceGroup)
                {
                    var caption = dep.Relation + " → " + DisplayLocation(dep.SchemaLocation)
                        + (string.IsNullOrWhiteSpace(dep.TargetNamespace) ? string.Empty : "  [" + dep.TargetNamespace + "]");
                    var child = new TreeNode(caption) { Tag = dep, ToolTipText = dep.ResolvedLocation };
                    sourceNode.Nodes.Add(child);
                }
                dependencyTree.Nodes.Add(sourceNode);
            }
            dependencyTree.ExpandAll();
        }
        finally { dependencyTree.EndUpdate(); }
    }

    private void PopulateProblems()
    {
        problemsView.BeginUpdate();
        try
        {
            problemsView.Items.Clear();
            if (currentModel == null) return;
            foreach (var problem in currentModel.Diagnostics)
            {
                var item = new ListViewItem(problem.Severity) { Tag = problem };
                item.SubItems.Add(problem.Message);
                item.SubItems.Add(DisplayLocation(problem.SourceUri));
                item.SubItems.Add(problem.Line.ToString());
                item.SubItems.Add(problem.Column.ToString());
                problemsView.Items.Add(item);
            }
        }
        finally { problemsView.EndUpdate(); }
    }

    private void OpenComponentNode(TreeNode node)
    {
        if (!(node.Tag is SchemaComponentModel component)) return;
        OpenComponent(component, true);
    }

    private void OpenSelectedOverviewComponent()
    {
        if (overviewList.SelectedItems.Count != 1 || !(overviewList.SelectedItems[0].Tag is SchemaComponentModel component)) return;
        OpenComponent(component, true);
    }

    private void OpenComponent(SchemaComponentModel component, bool addHistory)
    {
        switch (component.Kind)
        {
            case SchemaComponentKind.Element:
                ShowElementDiagram(component.Name, addHistory);
                break;
            case SchemaComponentKind.ComplexType:
            case SchemaComponentKind.SimpleType:
                OpenType(component.QualifiedName, addHistory);
                break;
            case SchemaComponentKind.Import:
            case SchemaComponentKind.Include:
                RequestOpenResource(component.QualifiedName);
                break;
            default:
                if (component.SourceLine.HasValue)
                    NavigateRequested?.Invoke(this, new SchemaLocationEventArgs(component.SourceLine.Value, component.SourceUri));
                breadcrumbLabel.Text = ComponentKindCaption(component.Kind) + " > " + component.Name;
                break;
        }
    }

    private void ShowElementDiagram(string rootName, bool addHistory)
    {
        if (currentModel == null) return;
        var root = currentModel.GlobalElements.FirstOrDefault(e => string.Equals(e.Name, rootName, StringComparison.Ordinal));
        if (root == null) return;

        updatingRootSelector = true;
        try
        {
            var index = rootSelector.FindStringExact(rootName);
            rootSelector.SelectedIndex = index;
        }
        finally { updatingRootSelector = false; }

        diagram.SetRoot(rootName);
        currentModel.Types.TryGetValue(root.TypeName, out var rootType);
        ShowProperties(root, rootType, root.Name);
        ShowDiagramUi();
        breadcrumbLabel.Text = root.Name;
        statusLabel.Text = rootName + " · collapsed by default · expand branches manually or use Expand all";
        if (addHistory) AddHistory(new NavigationEntry(NavigationKind.Element, rootName, rootName));
    }

    private void OpenType(string qualifiedTypeName, bool addHistory)
    {
        if (currentModel == null || string.IsNullOrWhiteSpace(qualifiedTypeName)) return;
        if (!currentModel.Types.TryGetValue(qualifiedTypeName, out var type)) return;

        updatingRootSelector = true;
        try { rootSelector.SelectedIndex = -1; }
        finally { updatingRootSelector = false; }

        diagram.SetRootType(qualifiedTypeName);
        var synthetic = new SchemaElementModel
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
        ShowProperties(synthetic, type, type.Name);
        ShowDiagramUi();
        breadcrumbLabel.Text = "Types > " + type.Name;
        statusLabel.Text = type.Name + " · type definition · collapsed by default";
        if (addHistory) AddHistory(new NavigationEntry(NavigationKind.Type, qualifiedTypeName, type.Name));
    }

    private void ShowDiagramUi()
    {
        overviewList.Visible = false;
        diagramSplit.Visible = true;
        diagram.Visible = true;
        diagramSplit.BringToFront();
        expandAllButton.Visible = true;
        collapseButton.Visible = true;
        resetZoomButton.Visible = true;
        AdjustPropertyPanelWidth();
    }

    private void ShowOverview(bool addHistory)
    {
        diagramSplit.Visible = false;
        overviewList.Visible = true;
        overviewList.BringToFront();
        expandAllButton.Visible = false;
        collapseButton.Visible = false;
        resetZoomButton.Visible = false;
        ClearProperties();
        breadcrumbLabel.Text = "Schema components";

        if (currentModel != null)
            statusLabel.Text = $"{Path.GetFileName(currentModel.SourcePath)} · {currentModel.Components.Count} components · double-click a component to open it";
        if (addHistory) AddHistory(new NavigationEntry(NavigationKind.Overview, string.Empty, "Components"));
    }

    private void ShowProperties(SchemaElementModel element, SchemaTypeModel? type, string schemaPath)
    {
        ClearProperties();
        AddProperty(generalView, "Name", element.Name);
        AddProperty(generalView, "Type", ShortName(element.TypeName), element.TypeName);
        AddProperty(generalView, "Occurrence", FormatOccurs(element));
        AddProperty(generalView, "Schema path", schemaPath);
        AddProperty(generalView, "Source", DisplayLocation(element.SourceUri));
        AddProperty(generalView, "Schema line", element.SourceLine?.ToString() ?? string.Empty);
        if (element.IsReference) AddProperty(generalView, "Reference", "yes");
        if (element.IsAbstract) AddProperty(generalView, "Abstract", "yes");

        if (type != null)
        {
            AddProperty(generalView, "Category", type.IsSimple ? "simple type" : type.IsAnonymous ? "anonymous complex type" : "complex type");
            AddProperty(generalView, "Compositor", type.Compositor.ToString());
            if (!string.IsNullOrWhiteSpace(type.BaseTypeName) && ShortName(type.BaseTypeName) != "anyType")
                AddProperty(generalView, "Base type", ShortName(type.BaseTypeName), type.BaseTypeName);
            if (type.IsAbstract) AddProperty(generalView, "Type abstract", "yes");

            foreach (var pattern in type.Patterns) patternView.Items.Add(new ListViewItem(pattern));
            foreach (var value in type.EnumerationValues) enumView.Items.Add(new ListViewItem(value));
            foreach (var facet in type.Facets) AddProperty(facetView, facet.Name, facet.Value);
            foreach (var attribute in type.Attributes)
            {
                var item = new ListViewItem(attribute.Name);
                item.SubItems.Add(ShortName(attribute.TypeName));
                item.SubItems.Add(attribute.Required ? "yes" : "no");
                item.SubItems.Add(attribute.Documentation);
                item.Tag = attribute;
                attributeView.Items.Add(item);
            }

            if (currentModel != null)
            {
                foreach (var reference in currentModel.References.Where(r => string.Equals(r.TargetQualifiedName, type.QualifiedName, StringComparison.Ordinal)))
                {
                    var item = new ListViewItem(reference.OwnerName) { Tag = reference };
                    item.SubItems.Add(reference.ReferenceKind);
                    item.SubItems.Add(reference.Path);
                    item.SubItems.Add(DisplayLocation(reference.SourceUri));
                    referencesView.Items.Add(item);
                }
            }
        }

        documentationBox.Text = !string.IsNullOrWhiteSpace(element.Documentation)
            ? element.Documentation
            : type?.Documentation ?? string.Empty;
    }

    private void OpenDefinitionFromGeneral()
    {
        if (generalView.SelectedItems.Count != 1) return;
        var item = generalView.SelectedItems[0];
        if ((item.Text == "Type" || item.Text == "Base type") && item.Tag is string qualified)
            OpenType(qualified, true);
    }

    private void OpenSelectedReference()
    {
        if (referencesView.SelectedItems.Count != 1 || !(referencesView.SelectedItems[0].Tag is SchemaReferenceModel reference) || currentModel == null) return;
        var root = currentModel.GlobalElements.FirstOrDefault(e => string.Equals(e.QualifiedName, reference.OwnerQualifiedName, StringComparison.Ordinal)
                                                                  || string.Equals(e.Name, reference.OwnerName, StringComparison.Ordinal));
        if (root != null)
        {
            ShowElementDiagram(root.Name, true);
            return;
        }
        if (currentModel.Types.ContainsKey(reference.OwnerQualifiedName))
        {
            OpenType(reference.OwnerQualifiedName, true);
            return;
        }
        if (reference.SourceLine.HasValue)
            NavigateRequested?.Invoke(this, new SchemaLocationEventArgs(reference.SourceLine.Value, reference.SourceUri));
    }

    private void OpenDependencyNode(TreeNode node)
    {
        if (node.Tag is SchemaDependencyModel dependency)
            RequestOpenResource(dependency.ResolvedLocation);
    }

    private void OpenSelectedProblem()
    {
        if (problemsView.SelectedItems.Count != 1 || !(problemsView.SelectedItems[0].Tag is SchemaDiagnosticModel problem)) return;
        if (problem.Line > 0)
            NavigateRequested?.Invoke(this, new SchemaLocationEventArgs(problem.Line, problem.SourceUri));
    }

    private void RequestOpenResource(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return;
        OpenResourceRequested?.Invoke(this, new SchemaResourceEventArgs(location));
    }

    private void AddHistory(NavigationEntry entry)
    {
        if (navigatingHistory) return;
        if (historyIndex >= 0 && historyIndex < history.Count && history[historyIndex].Equals(entry)) return;
        if (historyIndex < history.Count - 1) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
        history.Add(entry);
        historyIndex = history.Count - 1;
        UpdateHistoryButtons();
    }

    private void NavigateHistory(int delta)
    {
        var target = historyIndex + delta;
        if (target < 0 || target >= history.Count) return;
        historyIndex = target;
        navigatingHistory = true;
        try
        {
            var entry = history[target];
            if (entry.Kind == NavigationKind.Overview) ShowOverview(false);
            else if (entry.Kind == NavigationKind.Element) ShowElementDiagram(entry.Key, false);
            else if (entry.Kind == NavigationKind.Type) OpenType(entry.Key, false);
        }
        finally
        {
            navigatingHistory = false;
            UpdateHistoryButtons();
        }
    }

    private void UpdateHistoryButtons()
    {
        historyBackButton.Enabled = historyIndex > 0;
        historyForwardButton.Enabled = historyIndex >= 0 && historyIndex < history.Count - 1;
    }

    private void ClearProperties()
    {
        generalView.Items.Clear();
        patternView.Items.Clear();
        enumView.Items.Clear();
        facetView.Items.Clear();
        attributeView.Items.Clear();
        referencesView.Items.Clear();
        documentationBox.Clear();
    }

    private static void AddProperty(ListView view, string name, string value, object? tag = null)
    {
        var item = new ListViewItem(name) { Tag = tag };
        item.SubItems.Add(value ?? string.Empty);
        view.Items.Add(item);
    }


    private void AdjustBrowserWidth()
    {
        var available = mainSplit.ClientSize.Width - mainSplit.SplitterWidth;
        if (available <= 260) return;
        var desired = Math.Min(280, Math.Max(180, available / 4));
        var maximum = available - 120;
        var distance = Math.Max(120, Math.Min(maximum, desired));
        if (mainSplit.SplitterDistance != distance) mainSplit.SplitterDistance = distance;
    }

    private void AdjustPropertyPanelWidth()
    {
        var available = diagramSplit.ClientSize.Width - diagramSplit.SplitterWidth;
        if (available <= 240) return;
        var desiredPanel2 = Math.Max(300, Math.Min(460, diagramSplit.Width / 3));
        desiredPanel2 = Math.Min(desiredPanel2, available - 120);
        var distance = Math.Max(120, Math.Min(available - 120, available - desiredPanel2));
        if (diagramSplit.SplitterDistance != distance) diagramSplit.SplitterDistance = distance;
    }

    private static ListView CreatePropertyList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add("Property", 120);
        list.Columns.Add("Value", 260);
        return list;
    }

    private static ListView CreateSingleValueList(string columnName)
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add(columnName, 360);
        return list;
    }

    private static ListView CreateAttributeList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add("Name", 110);
        list.Columns.Add("Type", 150);
        list.Columns.Add("Required", 70);
        list.Columns.Add("Documentation", 260);
        return list;
    }

    private static ListView CreateReferencesList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add("Owner", 140);
        list.Columns.Add("Kind", 110);
        list.Columns.Add("Path", 240);
        list.Columns.Add("Source", 180);
        return list;
    }

    private static ListView CreateProblemsList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        list.Columns.Add("Severity", 70);
        list.Columns.Add("Message", 320);
        list.Columns.Add("Source", 180);
        list.Columns.Add("Line", 55);
        list.Columns.Add("Column", 55);
        return list;
    }

    private static string ComponentKindCaption(SchemaComponentKind kind)
    {
        switch (kind)
        {
            case SchemaComponentKind.Element: return "Elements";
            case SchemaComponentKind.ComplexType: return "Complex Types";
            case SchemaComponentKind.SimpleType: return "Simple Types";
            case SchemaComponentKind.Group: return "Groups";
            case SchemaComponentKind.AttributeGroup: return "Attribute Groups";
            case SchemaComponentKind.Attribute: return "Attributes";
            case SchemaComponentKind.Import: return "Imports";
            case SchemaComponentKind.Include: return "Includes";
            default: return kind.ToString();
        }
    }

    private static string FormatOccurs(SchemaElementModel element)
        => element.MinOccurs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".." + element.MaxOccurs;

    private static string ShortName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName)) return string.Empty;
        if (qualifiedName.StartsWith("@anonymous:", StringComparison.Ordinal)) return "anonymous complexType";
        var closingBrace = qualifiedName.LastIndexOf('}');
        return closingBrace >= 0 ? qualifiedName.Substring(closingBrace + 1) : qualifiedName;
    }

    private static string DisplayLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return string.Empty;
        if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) return Path.GetFileName(uri.LocalPath);
            var last = uri.Segments.Length > 0 ? uri.Segments[uri.Segments.Length - 1] : location;
            return last.TrimEnd('/');
        }
        return Path.GetFileName(location);
    }

    private enum NavigationKind { Overview, Element, Type }

    private sealed class NavigationEntry : IEquatable<NavigationEntry>
    {
        public NavigationEntry(NavigationKind kind, string key, string caption)
        {
            Kind = kind;
            Key = key;
            Caption = caption;
        }
        public NavigationKind Kind { get; }
        public string Key { get; }
        public string Caption { get; }
        public bool Equals(NavigationEntry? other) => other != null && other.Kind == Kind && string.Equals(other.Key, Key, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as NavigationEntry);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Key ?? string.Empty).GetHashCode();
    }
}

internal sealed class SchemaResourceEventArgs : EventArgs
{
    public SchemaResourceEventArgs(string location) => Location = location;
    public string Location { get; }
}
