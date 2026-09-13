# NppXsdViewer 0.3.1

## Javítás

- Javítva a `SplitContainer` inicializálási hiba, amely kis kezdeti szélességnél
  `InvalidOperationException` kivételt okozott a `Panel2MinSize` beállításakor.
- A tulajdonságpanel minimális szélessége már csak a tényleges rendelkezésre álló
  ablakméret ismeretében kerül beállításra.
- Az osztó pozíciója minden átméretezésnél biztonságos tartományra van korlátozva.
- A jobb oldali tulajdonságpanel automatikusan igazodik a dockolt panel aktuális
  szélességéhez, ezért keskeny Notepad++ elrendezés mellett sem omlik össze a plugin.
