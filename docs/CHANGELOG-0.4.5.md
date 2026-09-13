# NppXsdViewer 0.4.5

## Schema element search and navigation

- `Search schema` now indexes nested/local `xs:element` declarations reachable from global root elements, not only global components and named types.
- Search matches element name, qualified/type name, documentation, pattern, enumeration and restriction/facet data of the element type.
- Nested element search results show their schema path so duplicate field names can be distinguished.
- A single click on a filtered search result opens its global root diagram, expands every ancestor branch required to reveal the element, selects the element, centers it in the diagram and navigates to the XSD source declaration.
- Back/Forward history preserves nested element paths.
- Optional elements now use a dotted (`DashStyle.Dot`) border instead of a dashed border.
