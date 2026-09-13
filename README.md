# NppXsdViewer

Read-only, grafikus XSD megjelenítő plugin Notepad++-hoz C# nyelven.

## Cél

A plugin az aktuálisan megnyitott, mentett `.xsd` állományt olvassa, feloldja a lokális `xs:include` és `xs:import` hivatkozásokat, majd egy Notepad++ dock panelen gráfszerűen jeleníti meg a séma elemeit és típuskapcsolatait.

**A plugin nem szerkeszti és nem írja vissza az XSD-t.**

## Technológia

- C#
- .NET Framework 4.8
- WinForms
- `System.Xml.Schema.XmlSchemaSet`
- Notepad++ plugin API
- `UnmanagedExports.Repack.Upgrade` 1.2.1
- helyi `RGiesecke.DllExport.DllExportAttribute`
- x64 célplatform
- saját `AssemblyResolve` kezelés a plugin mappában lévő managed DLL-ekhez

A plugin export-rétege az aktuális `NppCSharpPluginPack` mintáját követi. Nincs `net.r_eg.DllExport` függőség.

## Solution

```text
NppXsdViewer.sln
src/
  NppXsdViewer.Schema/     XSD betöltés, resolver, normalizált modell
  NppXsdViewer.Diagram/    read-only WinForms diagram
  NppXsdViewer.Plugin/     Notepad++ integráció és dock panel
tests/
  NppXsdViewer.Schema.Tests/
examples/
  invoice.xsd
docs/
  PLUGIN-INFRASTRUCTURE.md
```

## Jelenlegi funkciók

- globális elemek listázása
- `complexType` és `simpleType`
- `sequence`, `choice`, `all`
- element és attribute megjelenítés
- `minOccurs` / `maxOccurs`
- enum, pattern és XSD facet értékek betöltése
- lokális `include` / `import`
- HTTP/HTTPS XSD import/include feloldás
- gyökérelem választó
- zoom: `Ctrl + egérgörgő`
- diagram node dupla kattintás -> ugrás az XSD forrássorára
- automatikus frissítés mentéskor és aktív dokumentum váltásakor

## Notepad++ unmanaged exportok

A `NppXsdViewer.dll` az alábbi, Notepad++ által elvárt belépési pontokat exportálja:

```text
isUnicode
setInfo
getFuncsArray
messageProc
getName
beNotified
```

Az exportok forrása:

```text
src/NppXsdViewer.Plugin/PluginInfrastructure/UnmanagedExports.cs
```

Az export attribútum:

```text
src/NppXsdViewer.Plugin/PluginInfrastructure/DllExport/DllExportAttribute.cs
```

## Build

Előfeltételek:

1. Windows 10/11
2. Visual Studio 2022
3. `.NET desktop development` workload
4. .NET Framework 4.8 Developer Pack
5. NuGet package restore
6. 64 bites Notepad++

Visual Studio alatt:

1. Nyisd meg a `NppXsdViewer.sln` fájlt.
2. Válaszd a `Debug | x64` vagy `Release | x64` konfigurációt.
3. Restore NuGet packages.
4. Build Solution.

Parancssorból, Visual Studio Developer PowerShellben:

```powershell
.\build.ps1
```

A build script:

```powershell
msbuild NppXsdViewer.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

## Telepítés fejlesztéshez

Release build után hozd létre:

```text
<Notepad++>\plugins\NppXsdViewer\
```

és másold bele a build outputból legalább:

```text
NppXsdViewer.dll
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

Ezután indítsd újra a Notepad++-t.

A menüben:

```text
Plugins
  NppXsdViewer
    XSD diagram megjelenítése
    XSD diagram frissítése
```

## Példa

Nyisd meg a Notepad++-ban:

```text
examples\invoice.xsd
```

majd:

```text
Plugins -> NppXsdViewer -> XSD diagram megjelenítése
```

A panel jobb oldalon dockolva jelenik meg.

## Biztonság

Az `XsdResourceResolver` támogatja a lokális `file:` és a távoli `http:`/`https:` `schemaLocation` hivatkozásokat. A relatív hivatkozásokat a hivatkozó XSD URI-jához képest oldja fel. Más URI-sémák tiltottak. Hálózati XSD-ből `file:` URI-ra váltás biztonsági okból nem engedélyezett. A hálózati lekérés 15 másodperces időkorlátot és XSD-nként 10 MiB-os méretkorlátot használ.

## Hatókör

Ez a verzió kizárólag megjelenítő. Nem tartalmaz:

- XSD módosítást
- drag & drop szerkesztést
- element/type létrehozást
- property editort
- diagram -> XSD visszaírást

## Build kimenet / Notepad++ telepítési csomag

A plugin projekt minden sikeres buildje után automatikusan létrejön a telepítési könyvtár:

```text
dist/
└─ NppXsdViewer/
   ├─ NppXsdViewer.dll
   ├─ NppXsdViewer.Schema.dll
   └─ NppXsdViewer.Diagram.dll
```

Ezt a teljes `NppXsdViewer` könyvtárat kell a Notepad++ `plugins` könyvtárába másolni:

```text
C:\Program Files\Notepad++\plugins\NppXsdViewer\
```

A gyökérből futtatott `build.ps1` a sikeres `Release|x64` build után ezen felül elkészíti:

```text
dist\NppXsdViewer-plugin.zip
```

A `dist` könyvtárba kizárólag a plugin futásához szükséges saját DLL-ek kerülnek. A build-time `DllExport`, `Microsoft.Build` és egyéb csomagfájlokat nem kell a Notepad++ alá másolni.

### Managed függőségek betöltése

A Notepad++ folyamata nem feltétlenül a `plugins\NppXsdViewer` könyvtárat használja managed assembly probing útvonalként. Emiatt a plugin már az unmanaged export réteg első inicializálásakor `AssemblyResolve` handlert telepít, amely kizárólag az alábbi saját függőségeket tölti be a `NppXsdViewer.dll` mappájából:

```text
NppXsdViewer.Schema.dll
NppXsdViewer.Diagram.dll
```

Ezért a három DLL-nek továbbra is ugyanabban a plugin könyvtárban kell lennie, de nincs szükség a Notepad++ gyökérkönyvtárába másolásra.

### 0.1.8 diagrammegjelenítés

A viewer az anonim `complexType`, az `xs:extension` alaptípus, valamint a komplex és egyszerű gyermekelemek struktúráját is kezeli. A részletes nézetben a szerkezet manuálisan nyitható ki.


## 0.1.8 lista alapú navigáció

Az alapnézet a globális XSD-elemek listája. Dupla kattintás vagy Enter megnyitja a kiválasztott elem részletes diagramját. A „Vissza a listához” gombbal az áttekintő nézethez lehet visszatérni.


## 0.1.9 fordítási javítás

- Javítva a `SchemaDiagramControl.RowVisual` `Element` névütközése.
- A forráselem property neve `SourceElement`, a factory metódus neve `ForElement`.
- Javítva az `AssemblyResolver` nullable annotációja.
- Az MSTest teszt `Assert.ThrowsExactly` API-t használ.
- A `NppXsdViewer.Diagram.dll` hiánya fordításkor csak következményhiba volt; az alap fordítási hiba megszűnésével létrejön.

## 0.2.0 diagram-interakció

- A diagram csomópontjai a fejléc bal oldalán található `+/-` vezérlővel ki- és becsukhatók.
- A `sequence`, `choice` és `all` tartalmi csoport külön `+/-` vezérlőt kapott.
- Az összecsukott kompozitor elrejti a gyermekelemeket és a hozzájuk tartozó diagramágakat, de a típus és az attribútumok továbbra is láthatók.
- A felső eszköztáron `Összecsuk` és `Mind kinyit` művelet érhető el.
- A dobozok kompaktabbak, kisebb a vízszintes és függőleges térköz, az összekötések egyszerűbb ortogonális vonalakkal jelennek meg.


## 0.2.1 Altova-szerű compositor megjelenítés

- A `sequence`, `choice` és `all` kompozitor nem a típusdoboz egyik soraként jelenik meg.
- A kompozitor a doboz jobb oldalán külön, kattintható buborékot kap (`S`, `C`, `A`).
- A buborékon lévő `+` / `-` jel nyitja és csukja a kompozitorhoz tartozó gyermekágat.
- A kompozitor gyermekelemei külön diagram-csomópontok; nem ismétlődnek meg a szülődoboz belsejében.
- A hosszú címek, típusnevek, attribútumok és egyéb sorok `EndEllipsis` megjelenítést használnak, ezért a szöveg nem lóghat ki a dobozból.
- Az öröklési (`extends`) kapcsolat a típusdobozban marad, a compositor összecsukása ettől független.


## 0.3.0 tulajdonságpanel, lazy kibontás és zoom-javítás

- A részletes diagram alapból **becsukott gyökérelemmel** indul.
- Megszűnt a `Mélység` mező: nincs mesterséges kibontási mélységkorlát.
- Megszűnt az `Illesztés` gomb.
- Az `S` / `C` / `A` buborékokkal a `sequence` / `choice` / `all` ágak kézzel nyithatók és csukhatók.
- A `B` buborék az alaptípus (`extends`) ágát nyitja és csukja.
- A `Mind kinyit` a kiválasztott gyökérből elérhető teljes, nem ciklikus struktúrát kinyitja.
- A `Mind becsuk` visszaállítja a kompakt gyökérnézetet.
- A PMT25 `Chain_*` elemeknél a `Chain_elem` anonim `complexType` további ágai is manuálisan nyithatók; nincs többé 4 szintes korlát.
- A diagramdobozok csak az elem nevét és típusát mutatják. Pattern, enum, facet, attribútum és dokumentáció nem terheli a diagramot.
- A jobb oldali tabos tulajdonságpanel lapjai: `Általános`, `Pattern`, `Enumerációk`, `Korlátozások`, `Attribútumok`, `Dokumentáció`.
- Egy diagramcsomópontra kattintva a jobb oldali panel az adott elem/típus adataira vált.
- A zoom renderelése egységes GDI+ transzformációt használ. A szöveg már nem külön `TextRenderer` rétegen rajzolódik, ezért nagyításkor/kicsinyítéskor a szövegek és a dobozok együtt mozognak és skálázódnak.
- `Ctrl + egérgörgő` nagyításkor a kurzor alatti logikai pont helyzete megmarad.

## 0.3.2 SplitContainer inicializálási javítás

A jobb oldali tulajdonságpanel minimumszélessége már nem a konstruktorban kerül beállításra.
A plugin a tényleges dockolt panelméret alapján, biztonságos tartományban állítja a
`Panel1MinSize`, `Panel2MinSize` és `SplitterDistance` értékeket. Ez megszünteti a
Notepad++ indulásakor/keskeny panelnél jelentkező `InvalidOperationException` hibát.

## 0.3.3 forráskiemelés és középre igazított navigáció

- A diagram egy elemére kattintva a forrásban már nem csak az elem kezdősorára ugrik.
- A plugin megkeresi az adott XSD deklaráció teljes XML-tartományát, beleértve a beágyazott tartalmat is.
- A teljes tartomány sárga kiemelést kap Scintilla indikátorral; a Notepad++ normál kijelölési színeit nem módosítja.
- A forrásnézet függőlegesen úgy görget, hogy a kiválasztott elem kezdete megközelítőleg az editor közepére kerüljön.
- A navigáció már egyszeres kattintásra működik a diagram csomópontjain; a sequence/choice/all/extends kapszulák kattintása továbbra is csak nyit/csuk.
- Az XML-tartomány keresése UTF-8 bájtpozíciókkal dolgozik, ezért a Scintilla pozíciókkal közvetlenül kompatibilis, és az ékezetes tartalom sem tolja el a kijelölést.

## 0.3.4 English-only UI

All plugin-owned user interface labels, toolbar actions, menu commands, status messages and error messages are English-only. Text originating from the loaded XSD, such as `xs:documentation`, is displayed unchanged.

## 0.4.0 schema explorer features

Version 0.4.0 turns the viewer into a broader XSD exploration tool while remaining read-only.

- grouped component browser: Elements, Complex Types, Simple Types, Groups, Attribute Groups, Attributes, Imports and Includes;
- schema-wide search including documentation, pattern and enumeration values;
- Back/Forward navigation and breadcrumb path;
- Go to type definition and Used by navigation;
- import/include dependency inspector;
- validation Problems view;
- occurrence badges;
- documentation preview tooltip;
- Copy schema path context action;
- local imported XSD files can be opened in Notepad++ directly.

The selected root/type still opens collapsed by default. Use the compositor/base capsules for manual expansion or **Expand all** to open the reachable graph.


## 0.4.1 diagram cleanup

The mini-map was removed from the diagram view because it did not provide enough practical value and occupied useful canvas space. All other 0.4.0 schema explorer features remain available.


## 0.4.2 required and optional elements

Diagram nodes now distinguish XSD cardinality visually without relying on color:

- required elements use a solid border;
- optional elements (`minOccurs=0`) use a dotted border and an `optional` badge;
- every node shows an occurrence badge (`[1]`, `[0..1]`, `[1..*]`, `[0..*]`);
- badge space is reserved so long element/type names stay inside the node.

## 0.4.4 vertical property sections

The right-side inspector no longer uses tabs. Property categories are displayed as separate vertically stacked boxes. Empty boxes are hidden, each box can be collapsed independently, long content scrolls inside its own box, and the complete inspector column remains scrollable.


## 0.4.5 nested element search

- `Search schema` searches nested/local `xs:element` declarations in addition to global components and named types.
- Nested results display their complete schema path.
- Clicking a filtered nested element result opens the owning root element, expands the path down to the result, selects and centers the node, and jumps to/highlights the declaration in the Notepad++ source editor.
- Optional element boxes use a dotted border.
