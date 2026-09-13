# NppXsdViewer

Read-only graphical XSD viewer and explorer plugin for Notepad++, written in C#.

<img width="1574" height="905" alt="image" src="https://github.com/user-attachments/assets/4b33b67b-887c-4171-b15b-a24b4049cf98" />


## Purpose

The plugin reads the currently opened and saved `.xsd` file, resolves local and remote `xs:include` / `xs:import` references, and displays the schema structure and type relationships in a dockable Notepad++ panel.

**The plugin is read-only: it does not edit or write changes back to the XSD file.**

The main goal is to make large and complex XSD schemas easier to inspect, search, navigate, and understand without leaving Notepad++.

---

## Technology

- C#
- .NET Framework 4.8
- WinForms
- `System.Xml.Schema.XmlSchemaSet`
- Notepad++ plugin API
- `UnmanagedExports.Repack.Upgrade` 1.2.1
- local `RGiesecke.DllExport.DllExportAttribute`
- x64 target platform
- custom `AssemblyResolve` handling for managed DLLs located in the plugin folder

The plugin export layer follows the current `NppCSharpPluginPack` approach.

There is no `net.r_eg.DllExport` dependency.

---

## Solution structure

```text
NppXsdViewer.sln

src/
  NppXsdViewer.Schema/     XSD loading, resolver and normalized schema model
  NppXsdViewer.Diagram/    Read-only WinForms diagram and explorer UI
  NppXsdViewer.Plugin/     Notepad++ integration and dock panel

tests/
  NppXsdViewer.Schema.Tests/

examples/
  invoice.xsd

docs/
  PLUGIN-INFRASTRUCTURE.md
```

### `NppXsdViewer.Schema`

Contains schema-processing logic:

- XSD loading;
- schema compilation;
- local and remote imports/includes;
- normalized schema model;
- global and local elements;
- complex/simple types;
- restrictions and facets;
- documentation;
- dependency information.

### `NppXsdViewer.Diagram`

Contains the graphical viewer:

- diagram layout;
- node rendering;
- compositor rendering;
- search;
- navigation;
- property sections;
- selection;
- zoom;
- source synchronization.

### `NppXsdViewer.Plugin`

Contains Notepad++ integration:

- unmanaged plugin entry points;
- Notepad++ messaging;
- Scintilla integration;
- docking panel;
- source navigation;
- managed dependency resolution.

---

# Current features

## Global element overview

The initial view lists the global XSD elements.

Double-click an element or press Enter to open its diagram.

The selected root element opens collapsed by default so large schemas remain manageable.

---

## Graphical schema diagram

Supported schema structures include:

- global elements;
- local/nested elements;
- `complexType`;
- `simpleType`;
- anonymous `complexType`;
- `sequence`;
- `choice`;
- `all`;
- `xs:extension`;
- elements and attributes;
- `minOccurs`;
- `maxOccurs`;
- enumerations;
- patterns;
- XSD facet values;
- documentation.

There is no artificial diagram depth limit.

The schema structure can be expanded manually or with:

```text
Expand all
Collapse all
```

---

## Compositor display

`sequence`, `choice`, and `all` are displayed outside the element/type box as separate interactive capsules.

The capsules show their full names:

```text
sequence
choice
all
extends
```

Each capsule has its own expand/collapse control.

This keeps structural information separate from element properties and makes large diagrams easier to read.

---

## Required and optional elements

Element cardinality is visible directly in the diagram.

### Required elements

Required elements use:

- a solid border;
- an occurrence badge, for example:

```text
[1]
[1..*]
```

### Optional elements

Elements with:

```xml
minOccurs="0"
```

use:

- a dotted border;
- an `optional` badge;
- an occurrence badge, for example:

```text
[0..1]
[0..*]
```

The distinction does not rely only on color.

---

## Schema Component Browser

The component browser groups the main schema components into categories:

- Elements
- Complex Types
- Simple Types
- Groups
- Attribute Groups
- Attributes
- Imports
- Includes

Global elements and named types can be opened directly from the browser.

---

## Search schema

`Search schema` searches the schema model, including both global components and nested/local `xs:element` declarations.

Searchable information includes:

- element name;
- type name;
- namespace;
- documentation;
- pattern;
- enumeration values;
- restriction/facet values.

Example:

```xml
<element name="Field_0C0001C0032CA"
         minOccurs="0"
         meta:metaType="FieldMetaType">
    <annotation>
        <documentation>
            32. A szakirányú oktatás és a duális képzés...
        </documentation>
    </annotation>
    <simpleType>
        <restriction base="string">
            <minLength value="1"/>
            <maxLength value="15"/>
            <pattern value="[0-9]+"/>
        </restriction>
    </simpleType>
</element>
```

A local element such as `Field_0C0001C0032CA` can be found directly by name or by searchable schema metadata.

### Nested result navigation

For nested/local elements, the result includes the complete schema path.

Selecting a nested result:

1. opens the owning global root element;
2. expands the schema path down to the result;
3. selects the matching diagram node;
4. centers it in the diagram;
5. navigates to the corresponding XSD declaration in Notepad++;
6. highlights the complete declaration in the source editor.

---

## Source navigation and highlighting

Clicking a diagram node navigates directly to the corresponding source declaration.

The plugin:

- locates the complete XSD XML declaration;
- highlights the full declaration using a Scintilla indicator;
- scrolls the editor so the declaration is approximately centered;
- clears the previous highlight;
- works with UTF-8 byte positions so accented/non-ASCII text does not break navigation.

For self-closing elements, only the self-closing tag is highlighted.

Compositor capsules only expand/collapse the diagram and do not navigate to the source.

---

## Navigation

The viewer provides:

- Back
- Forward
- breadcrumb path
- Root element selector
- Go to type definition
- Used by
- Copy schema path

Example breadcrumb:

```text
CreateReceiptRequest / issuingSoftware / name
```

---

## Right-side property inspector

The right-side inspector uses vertically stacked boxes instead of tabs.

Available sections include:

- General
- Pattern
- Enumerations
- Restrictions
- Attributes
- Documentation
- Used by
- Dependencies
- Problems

Behavior:

- empty sections are hidden;
- each section can be collapsed independently;
- visible sections automatically share the available vertical space;
- long content scrolls inside its own box;
- if many sections are visible, the complete inspector column can scroll.

The diagram nodes themselves stay intentionally compact: detailed restrictions, patterns, enumerations, attributes, and documentation belong in the property inspector.

---

## Used by

The `Used by` section shows where a selected schema type or component is referenced.

This is useful in large schemas with reusable common types.

---

## Dependencies

The Dependencies section shows `xs:import` and `xs:include` relationships.

Displayed information may include:

- dependency type;
- namespace;
- `schemaLocation`;
- resolved local or remote location;
- resolution status.

Local imported XSD files can be opened directly in Notepad++.

---

## Problems

Schema loading and validation problems are shown separately.

Available information includes:

- Error / Warning;
- source;
- line;
- column;
- message.

The viewer attempts to preserve useful schema navigation even when the schema contains recoverable problems.

---

## Automatic refresh

The viewer refreshes automatically when:

- the current XSD is saved;
- the active Notepad++ document changes.

---

## Zoom

Zoom is available with:

```text
Ctrl + mouse wheel
```

Text and diagram geometry use the same GDI+ transform, so labels and boxes remain aligned while zooming.

The logical point under the mouse cursor is preserved as closely as possible during zoom.

---

# Notepad++ unmanaged exports

`NppXsdViewer.dll` exports the entry points expected by Notepad++:

```text
isUnicode
setInfo
getFuncsArray
messageProc
getName
beNotified
```

Export implementation:

```text
src/NppXsdViewer.Plugin/PluginInfrastructure/UnmanagedExports.cs
```

Export attribute:

```text
src/NppXsdViewer.Plugin/PluginInfrastructure/DllExport/DllExportAttribute.cs
```

Because this layer exposes unmanaged Notepad++ plugin entry points, the plugin is built specifically for `x64`.

`Any CPU` should not be used for release builds.

---

# Requirements

## Operating system

Supported development/runtime environment:

```text
Windows 10
Windows 11
```

## Notepad++

The current build targets:

```text
64-bit Notepad++
```

## .NET Framework

The plugin targets:

```text
.NET Framework 4.8
```

This is a **.NET Framework 4.8** application.

It is not a `.NET 8`, `.NET 9`, or `.NET 10` plugin.

For development/building, install:

```text
.NET Framework 4.8 Developer Pack / Targeting Pack
```

The target machine must have the .NET Framework 4.8 runtime available.

---

# Recommended build environment

Recommended IDE:

```text
Visual Studio 2022
```

The Community edition is sufficient.

Install the following workload/components:

- `.NET desktop development`
- `.NET Framework 4.8 Targeting Pack`
- MSBuild
- NuGet package restore support

---

# Build with Visual Studio

1. Open:

```text
NppXsdViewer.sln
```

2. Select one of:

```text
Debug | x64
Release | x64
```

For distribution, use:

```text
Release | x64
```

3. Restore NuGet packages.

4. Run:

```text
Build -> Rebuild Solution
```

The main runtime assemblies are:

```text
NppXsdViewer.dll
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

The main Notepad++ plugin DLL is:

```text
NppXsdViewer.dll
```

---

# Build from the command line

Run the build from a Visual Studio Developer PowerShell or Developer Command Prompt.

Example:

```powershell
msbuild NppXsdViewer.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

The repository also contains:

```text
build.ps1
```

Run it from the repository root:

```powershell
.\build.ps1
```

The script invokes:

```powershell
msbuild NppXsdViewer.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

and prepares the deployment output.

---

# Build output

After a successful build, the plugin deployment directory is created as:

```text
dist/
└─ NppXsdViewer/
   ├─ NppXsdViewer.dll
   ├─ NppXsdViewer.Schema.dll
   └─ NppXsdViewer.Diagram.dll
```

The root `build.ps1` also creates:

```text
dist\NppXsdViewer-plugin.zip
```

Only the runtime DLLs required by the plugin are copied to `dist`.

Build-time packages such as DllExport or Microsoft.Build components do not need to be copied into the Notepad++ installation.

---

# Manual installation

## 1. Close Notepad++

Close all running Notepad++ instances before installing or replacing plugin files.

## 2. Create the plugin directory

For a standard 64-bit Notepad++ installation:

```text
C:\Program Files\Notepad++\plugins\NppXsdViewer\
```

## 3. Copy the runtime DLLs

Copy:

```text
NppXsdViewer.dll
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

The final layout should be:

```text
C:\Program Files\Notepad++\plugins\NppXsdViewer\
    NppXsdViewer.dll
    NppXsdViewer.Schema.dll
    NppXsdViewer.Diagram.dll
```

All three assemblies must remain in the same directory.

## 4. Restart Notepad++

Start Notepad++ again.

The plugin appears under:

```text
Plugins
  NppXsdViewer
```

The plugin-owned UI is English-only.

---

# Example

Open:

```text
examples\invoice.xsd
```

in Notepad++ and start the viewer from:

```text
Plugins -> NppXsdViewer
```

The viewer opens as a dockable panel.

---

# Managed dependency loading

The Notepad++ process does not necessarily use:

```text
plugins\NppXsdViewer
```

as a normal managed assembly probing path.

Therefore the plugin installs an `AssemblyResolve` handler during early initialization.

It resolves the plugin's own managed dependencies from the directory containing `NppXsdViewer.dll`:

```text
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

This is why the three DLLs must remain together in the same plugin directory.

They do not need to be copied into the Notepad++ application root.

---

# XSD imports and includes

`XsdResourceResolver` supports:

- local `file:` references;
- remote `http:` references;
- remote `https:` references;
- relative `schemaLocation` resolution;
- chained imports/includes.

Relative references are resolved against the URI of the referring XSD.

Supported network behavior includes:

- TLS 1.2;
- Windows default proxy configuration;
- default proxy credentials;
- chained remote imports/includes.

Security restrictions:

- unsupported URI schemes are rejected;
- switching from a network XSD to a local `file:` URI is blocked;
- network requests use a timeout;
- remote XSD size is limited.

Current limits:

```text
Network timeout: 15 seconds
Maximum remote XSD size: 10 MiB per resource
```

---

# Diagnostics

The plugin writes diagnostic information to the current user's TEMP directory.

Main plugin log:

```text
%TEMP%\NppXsdViewer.log
```

Schema resolver/network diagnostics:

```text
%TEMP%\NppXsdViewer-resolver.log
```

If the plugin does not start, a managed dependency cannot be loaded, or a remote import cannot be resolved, check these files first.

---

# Troubleshooting

## The plugin does not appear in Notepad++

Check that:

- you are running 64-bit Notepad++;
- the plugin was built as `x64`;
- the plugin folder is named `NppXsdViewer`;
- the main DLL is named `NppXsdViewer.dll`;
- all required DLLs are in the same plugin folder;
- Notepad++ was restarted after installation.

## Missing `NppXsdViewer.Diagram` assembly

Example:

```text
Could not load file or assembly 'NppXsdViewer.Diagram'
```

Verify that all runtime assemblies are installed together:

```text
NppXsdViewer.dll
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

## The project does not build

Check that:

- Visual Studio 2022 is installed;
- `.NET desktop development` workload is installed;
- .NET Framework 4.8 Developer Pack is installed;
- NuGet restore completed successfully;
- `Debug | x64` or `Release | x64` is selected;
- MSBuild is launched from a Visual Studio developer shell.

---

# Security

`XsdResourceResolver` supports local `file:` and remote `http:` / `https:` `schemaLocation` references.

Relative references are resolved against the URI of the referring schema.

Other URI schemes are rejected.

For security reasons, a schema loaded from the network is not allowed to switch to a local `file:` URI.

Network requests use:

```text
15 second timeout
10 MiB maximum XSD size per resource
```

---

# Scope

This plugin is intentionally read-only.

It does not provide:

- XSD modification;
- drag-and-drop editing;
- element/type creation;
- editable property panels;
- diagram-to-XSD write-back.

The XSD source remains the authoritative document and is edited directly in Notepad++.

---

# Notepad++ Plugin Admin packaging

For submission to the official Notepad++ Plugin Admin repository, create a release ZIP whose root contains the runtime DLLs directly:

```text
NppXsdViewer-<version>-x64.zip
├── NppXsdViewer.dll
├── NppXsdViewer.Schema.dll
└── NppXsdViewer.Diagram.dll
```

Do not place an additional `NppXsdViewer` directory inside the Plugin Admin ZIP.

Plugin Admin creates the destination plugin folder itself.

For an x64-only release, the plugin list entry belongs in:

```text
src/pl.x64.json
```

of the official:

```text
notepad-plus-plus/nppPluginList
```

repository.

Before submission:

- publish a public release;
- verify the DLL/file version;
- calculate the release ZIP SHA-256;
- add a project license;
- test Plugin Admin installation;
- test update;
- test removal.

---

# Version history

## 0.1.8 - diagram rendering

- Added anonymous `complexType` handling.
- Added `xs:extension` base-type handling.
- Complex and simple child elements are represented in the diagram.
- The schema structure can be expanded manually.

## 0.1.8 - list-based navigation

- The initial view is the global XSD element list.
- Double-click or Enter opens the selected element's detailed diagram.
- `Back to list` returns to the overview.

## 0.1.9 - build fixes

- Fixed the `SchemaDiagramControl.RowVisual` `Element` naming collision.
- Source element property renamed to `SourceElement`.
- Factory method renamed to `ForElement`.
- Fixed nullable annotation in `AssemblyResolver`.
- MSTest uses `Assert.ThrowsExactly`.
- The missing `NppXsdViewer.Diagram.dll` was a secondary build failure; fixing the source error restores the assembly output.

## 0.2.0 - diagram interaction

- Diagram nodes can be expanded/collapsed from the header.
- `sequence`, `choice`, and `all` content groups have independent expand/collapse state.
- Collapsing a compositor hides its child branch.
- Added `Collapse all` and `Expand all`.
- Reduced box spacing and simplified orthogonal connectors.

## 0.2.1 - Altova-style compositor visualization

- `sequence`, `choice`, and `all` are displayed outside the type box.
- Compositors are separate clickable diagram controls.
- Child elements appear only as child diagram nodes and are not duplicated inside the parent box.
- Long labels use ellipsis to prevent text overflow.
- Inheritance remains independent from compositor expansion.

## 0.3.0 - property inspector, lazy expansion and zoom fixes

- Root diagrams open collapsed.
- Removed the artificial depth limit.
- Removed the Fit button.
- Added manual `sequence` / `choice` / `all` expansion.
- Added independent base-type (`extends`) expansion.
- `Expand all` opens the complete reachable non-cyclic structure.
- `Collapse all` returns to the compact root view.
- Nested anonymous `Chain_*` structures can be expanded without a four-level limit.
- Diagram boxes contain only structural identity information.
- Pattern, enumeration, facets, attributes, and documentation are shown in the property inspector.
- Fixed zoom rendering so text and shapes use the same graphics transformation.
- Cursor-centered zoom behavior was added.

## 0.3.2 - SplitContainer initialization fix

The right-side property inspector minimum width is no longer assigned unsafely during control construction.

`Panel1MinSize`, `Panel2MinSize`, and `SplitterDistance` are calculated only after a usable docked size exists.

This prevents startup/narrow-panel `InvalidOperationException` failures.

## 0.3.3 - source highlighting and centered navigation

- Clicking a diagram element navigates to the full source declaration.
- The complete XML declaration is highlighted.
- Source navigation centers the declaration approximately in the editor.
- Navigation works on a single node click.
- Compositor capsules continue to expand/collapse only.
- UTF-8 byte positions are used for Scintilla compatibility.

## 0.3.4 - English-only UI

All plugin-owned user interface labels, toolbar actions, menu commands, status messages, and error messages are English-only.

Text originating from the loaded XSD, such as `xs:documentation`, is displayed unchanged.

## 0.4.0 - schema explorer features

Version 0.4.0 expands the viewer into a broader XSD exploration tool while remaining read-only.

Added:

- grouped Component Browser:
  - Elements
  - Complex Types
  - Simple Types
  - Groups
  - Attribute Groups
  - Attributes
  - Imports
  - Includes
- schema-wide search;
- Back/Forward navigation;
- breadcrumb navigation;
- Go to type definition;
- Used by navigation;
- import/include dependency inspector;
- validation Problems view;
- occurrence badges;
- documentation preview tooltip;
- Copy schema path;
- direct opening of local imported XSD files in Notepad++.

## 0.4.1 - diagram cleanup

- Removed the mini-map because it did not provide enough practical value and consumed useful canvas space.

## 0.4.2 - required and optional elements

- Required elements use a solid border.
- Optional elements (`minOccurs="0"`) use a dotted border.
- Optional elements show an `optional` badge.
- Every element shows an occurrence badge:
  - `[1]`
  - `[0..1]`
  - `[1..*]`
  - `[0..*]`
- Badge space is reserved so long names remain inside the node.

## 0.4.3 / 0.4.4 - vertical property sections

The right-side inspector no longer uses tabs.

Property categories are shown as vertically stacked boxes.

- Empty boxes are hidden.
- Each box can be collapsed independently.
- Long content scrolls inside its own box.
- The complete inspector column can scroll.
- Visible boxes automatically share the available vertical space.
- Section heights are no longer fixed.

## 0.4.5 - nested element search

- `Search schema` searches nested/local `xs:element` declarations in addition to global components and named types.
- Nested results display the complete schema path.
- Selecting a nested result opens the owning root element.
- The path down to the result is automatically expanded.
- The matching node is selected and centered.
- The plugin jumps to and highlights the declaration in the Notepad++ source editor.
- Optional element boxes use a dotted border.



For an open-source release, verify that the selected license is compatible with all third-party components used by the project.
