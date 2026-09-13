# NppXsdViewer 0.4.0

## Schema exploration

- Added grouped component browser for elements, complex types, simple types, groups, attribute groups, attributes, imports and includes.
- Added schema-wide search across names, qualified names, types, documentation, patterns, enumerations and restrictions.
- Added direct diagram opening for global elements and global types.
- Added Back/Forward navigation and breadcrumb path display.
- Added Go to type definition from the diagram context menu and property panel.
- Added Used by/reference view with navigation back to referring components.

## Schema dependencies and validation

- Added dependency inspector for import/include relationships and namespaces.
- Local dependency files can be opened directly in Notepad++; HTTP/HTTPS dependencies open through the system browser.
- Added Problems tab for schema validation diagnostics with source navigation.

## Diagram usability

- Added schema-path copy action.
- Added occurrence badges such as 0..1 and 0..*.
- Added documentation preview tooltip.
- Added mini-map for larger diagrams.
- Added 100% zoom reset.
- Existing collapsed-by-default and Expand all/Collapse all behaviour remains unchanged.

## Model

- Schema model now exposes components, dependencies, diagnostics and reverse references.
- Type/element source URI metadata is retained to support cross-file navigation.
