# NppXsdViewer 0.4.4

- Property sections no longer use fixed expanded heights.
- Visible expanded sections automatically share the available vertical space.
- When only a few sections are visible, they grow to fill the property panel.
- When many sections are visible, each keeps a minimum usable height and the outer property panel remains scrollable.
- Each section content control keeps its own scrolling behavior when its content exceeds the allocated height.
- Collapsing or expanding a section immediately redistributes the available height among the remaining expanded sections.
