# Build státusz

## Elvégzett ellenőrzések

- a régi `net.r_eg.DllExport` hivatkozás eltávolítva;
- az `UnmanagedExports.Repack.Upgrade` build-integráció jelen van;
- a hat kötelező Notepad++ export forrása jelen van;
- `NPPM_GETFULLCURRENTPATH` a `RUNCOMMAND_USER + FULL_CURRENT_PATH` tartományból képződik;
- a plugin projekt GUID-ja megegyezik a solution GUID-jával;
- minden `.csproj` XML-ként feldolgozható;
- a teszt XSD a teszt outputba másolódik, ezért a teszt nem függ a process aktuális könyvtárától;
- ZIP integritás ellenőrzendő a kiadás készítésekor.

## Környezeti korlát

A csomag előállítására használt Linux környezetben nincs Visual Studio/MSBuild/.NET Framework 4.8 build toolchain,
ezért a Notepad++ plugin DLL natív exportjainak tényleges Windows build-ellenőrzése itt nem futtatható.

A végleges Windows ellenőrzéshez futtasd Visual Studio Developer PowerShellben:

```powershell
.\build.ps1
```

A `build.ps1` először lefuttatja a `verify-source.ps1` regressziós ellenőrzést, majd `Release | x64` buildet indít.

## 0.1.3 - plugin command crash javitas

- A `FuncItem` natív memóriaelrendezése az aktuális NppCSharpPluginPack megoldásához lett igazítva.
- A menüparancs callbackok védett végrehajtást kaptak; managed kivétel nem futhat ki az unmanaged Notepad++ callbackből.
- Hiba esetén részletes diagnosztika készül a `%TEMP%\\NppXsdViewer.log` fájlba.

## 0.1.5 - HTTP/HTTPS schemaLocation feloldas

- A korábbi `LocalOnlyXmlResolver` megszűnt.
- Az új `XsdResourceResolver` támogatja a `file:`, `http:` és `https:` XSD erőforrásokat.
- A távoli sémák relatív `xs:include` és `xs:import` hivatkozásai a távoli alap-URI-hoz képest oldódnak fel.
- Hálózati sémából `file:` URI-ra váltás tiltott.
- A hálózati lekérések időkorlátja 15 másodperc, az egy XSD-re vonatkozó méretkorlát 10 MiB.
- Hálózatot nem igénylő resolver regressziós tesztek kerültek a tesztprojektbe.

## 0.1.8 - teljes XSD diagramfa

- Anonim `complexType` típusok külön belső azonosítót kapnak, ezért a globális elemek névtelen típusai is kirajzolhatók.
- Az `xs:extension` alaptípus külön `extends` kapcsolatként megjelenik és tovább bontható.
- Minden elemhez külön diagramcsomópont készül, a beépített egyszerű XSD típusokhoz is levélcsomóponttal.
- Az összekötések nem a doboz fejlécéből, hanem a konkrét elem sorából indulnak.
- A diagram rekurzív faelrendezést használ; a szülőcsomópont a gyermek-alfa közepére kerül.
- Rekurzív típushivatkozásnál a további bontás leáll, így nem keletkezik végtelen gráf.


## 0.1.9 - compile fix

A `RowVisual.Element` statikus factory és az azonos nevű `Element` property C# névütközése megszüntetve. A downstream `NppXsdViewer.Diagram.dll could not be found` hiba ennek következménye volt.


## 0.2.0 - interaktív összecsukható diagram

- Csomópont fejléc szintű ki-/becsukás (`+/-`).
- `sequence`, `choice`, `all` tartalmi csoport külön ki-/becsukása.
- Kompaktabb diagramelrendezés és kisebb térközök.
- `Összecsuk`, `Mind kinyit`, `Illesztés` eszköztári műveletek.
- Az összecsukás újratördeli a diagramot, ezért nem maradnak üres ágak.


## 0.2.1 - compositor buborék és szövegklippelés

- `sequence` / `choice` / `all` külön, dobozon kívüli buborékként jelenik meg.
- A buborék kattintással nyitható/csukható.
- A compositor gyermekei külön csomópontok.
- A dobozok minden szövege ellipszissel klippelt.
- A forrás ebben a környezetben statikusan ellenőrizhető, Windows/.NET Framework 4.8 build itt nem futott.
