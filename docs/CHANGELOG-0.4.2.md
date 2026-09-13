# NppXsdViewer 0.4.2

## Required vs optional element visualization

- Required elements (`minOccurs >= 1`) use a solid node border.
- Optional elements (`minOccurs = 0`) use a dashed node border.
- Optional elements display an `optional` badge.
- Every element displays an occurrence badge such as `[1]`, `[0..1]`, `[1..*]` or `[0..*]`.
- Requirement is never communicated by color alone, so the diagram remains readable in different themes and for color-vision deficiencies.
- Node title/type text reserves space for badges and is clipped with ellipsis instead of overlapping them.
