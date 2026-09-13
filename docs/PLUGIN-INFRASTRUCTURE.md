# Notepad++ plugin infrastruktúra

## Kiindulópont

A `NppXsdViewer.Plugin` a `NppCSharpPluginPack` aktuális exportmintáját követi.

A Notepad++ natív plugin ABI-ja miatt a DLL-nek az alábbi függvényeket kell exportálnia:

- `isUnicode`
- `setInfo`
- `getFuncsArray`
- `messageProc`
- `getName`
- `beNotified`

Az exportálást a projekt nem a régi `net.r_eg.DllExport` namespace-re építi. A projektben helyben szerepel a
`RGiesecke.DllExport.DllExportAttribute` attribútum, az exportok előállítását pedig az
`UnmanagedExports.Repack.Upgrade` MSBuild target végzi.

## Notepad++ üzenetek

Az aktuális fájlnév lekérése a Notepad++ API szerinti `RUNCOMMAND_USER + FULL_CURRENT_PATH` üzenettel történik.
Ez szándékosan nem `NPPMSG + n` tartományból képzett érték.

A dock panel regisztrációja `NPPM_DMMREGASDCKDLG`, megjelenítése `NPPM_DMMSHOW` segítségével történik.

## Read-only működés

A plugin nem ír a szerkesztett dokumentumba. A Scintilla felé csak navigációs üzenetet küld (`SCI_GOTOLINE`),
amikor a felhasználó a diagram egy forrássorral rendelkező elemére navigál.
