# NppXsdViewer 0.3.3

## Forrásnavigáció

- Diagram csomópontra kattintva a teljes XSD deklaráció kerül kiemelésre a forrásban.
- A kiemelés sárga, áttetsző Scintilla `FULLBOX` indikátorral készül.
- A korábbi kiemelés automatikusan törlődik, mielőtt az új tartomány megjelenik.
- A forrásnézet a kijelölt deklaráció kezdősorát függőlegesen középre görgeti.
- A diagram csomópontokhoz az egyszeres kattintás is forrásnavigációt indít; a compositor és `extends` kapszulák működése változatlan.

## XML tartománykeresés

Új `XmlSourceSpanFinder` készült. A Scintilla UTF-8 dokumentumpufferén dolgozik, és kezeli:

- az önzáró tageket;
- az egymásba ágyazott azonos nevű XML elemeket;
- az attribútumértékekben lévő `>` karaktert;
- XML kommenteket;
- CDATA blokkokat;
- processing instruction elemeket.

Ha félkész vagy hibás XML miatt nem található a záró tag, legalább a kezdő tag kerül kiemelésre.
