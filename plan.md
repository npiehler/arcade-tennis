# Arcade Tennis — Plan

3D-Arcade-Tennis in Unity. **Teil 1** (dieses Dokument) baut die komplette Spielmechanik mit
Dummy-Figuren. **Teil 2** ersetzt die Dummys durch echte, in Blender erstellte 3D-Assets.

Aktueller Stand: siehe [progress.md](progress.md).

---

## Spielentwurf

Vier Festlegungen, die alles andere prägen. Sie sind entschieden, nicht offen:

| Frage | Entscheidung |
|---|---|
| **Perspektive** | Kamera hinter der eigenen Grundlinie, Blick übers Netz |
| **Schlagmechanik** | Ein Tastendruck, kein Aufladen. Der Trefferzeitpunkt bestimmt die Qualität, die Qualität die Tiefe. Die Richtung ist eine von drei Zonen, mit eigener Taste gewählt und als Marker sichtbar |
| **Umfang Teil 1** | Kompletter Satz mit voller Tennis-Zählweise, Aufschlag und Seitenwechsel |
| **Arcade-Elemente** | Zielmarker am Boden: der Landepunkt des fliegenden Balls **und** die gewählte Zielzone. **Keine** Schlagvarianten, kein Powershot, kein Sprint |

### Bewusst nicht enthalten

Der Nutzer hat Schlagvarianten (Topspin, Slice, Lob, Stopp) abgewählt. Für Teil 1 trägt das,
weil die KI an der Grundlinie bleibt und ein reiner Schlagabtausch ohne Lob funktioniert. Die
Schlaglogik in M4 ist aber so gebaut, dass Varianten später als Datensatz dazukommen und nicht
als Umbau. **Nicht eigenmächtig einbauen** — das ist eine getroffene Entscheidung.

### Weitere gesetzte Annahmen

- Einzel, ein Satz bis 6 Spiele mit zwei Vorsprung, Tiebreak bei 6:6
- Tastatur und Gamepad parallel (neues Input System)
- Netzroller beim Aufschlag zählen als normal, kein Let — spart einen Sonderfall
- `SampleScene` bleibt Sandbox, gespielt wird in `Assets/_Game/Scenes/Match.unity`

---

## Architektur-Grundsätze

Diese vier Regeln gelten für alles, was noch dazukommt.

### 1 Unit = 1 Meter, echte Platzmaße

Feld 23,77 × 8,23 m (Einzel), Aufschlaglinie 6,40 m vom Netz, Netz 0,914 m Mitte / 1,07 m
Pfosten. Arcade-Gefühl kommt über Ball- und Laufgeschwindigkeit, **nicht** über geschrumpfte
Maße. So importieren die Blender-Assets in Teil 2 ohne Skalierungs-Chaos.

### 2. Simulation einmal schreiben, zweimal verwenden

Ballflug und Figurenbewegung liegen in **reinen statischen Funktionen**
(`BallSimulation`, `CharacterMotion`). Der lebende Ball ruft sie pro Tick; Zielmarker und KI
rufen dieselbe Funktion auf einer Kopie des Zustands. Es darf kein zweites, genähertes Modell
geben, das abweichen könnte. Der Vorhersagefehler des Markers ist deshalb exakt 0,00000 m —
nicht „nah dran", sondern dieselbe Rechnung.

Nebeneffekt: alles ist ohne Play Mode testbar (siehe `Tools/verify_*.cs`).

### 3. Koordinatensystem

- Netz liegt auf der X-Achse bei `z = 0`, der Platz läuft entlang Z
- Grundlinien bei `z = ±HalfLength` (±11,885)
- **Seite `+1`** = Hälfte mit `z > 0`, **Seite `-1`** = Hälfte mit `z < 0`
- Ein Spieler auf Seite `S` blickt Richtung `-S` auf der Z-Achse
- Eingabe wird in Screen-Space gelesen (x = rechts, y = Richtung Netz) und pro Seite gespiegelt,
  damit „Stick nach oben" für beide Spieler „Richtung Netz" heißt

### 4. Logik-Root + Visual-Child

Jede Figur ist ein Root mit der Logik und einem Kind `Visual` mit allem Sichtbaren. **In Teil 2
wird nur dieses Kind getauscht.** Schläge feuern Events (`OnSwingStart`, `OnContact`), an die
später ein Animator andockt. Keine Logik ins Visual legen.

### Keine Collider auf Platz und Ball

Ballflug, Aufsprung und Netzkontakt werden analytisch gegen die `CourtDefinition` gerechnet,
Spielerbewegung wird geklemmt statt simuliert. Unity-Physik würde nur kosten und überraschen.

---

## Meilensteine

| # | Inhalt | Fertig, wenn | Status |
|---|---|---|---|
| **M0** | Gerüst: Ordner, Assembly Definition, Szene `Match.unity`, eigene `TennisControls.inputactions` | Play Mode startet fehlerfrei | ✅ |
| **M1** | Court: `CourtDefinition` (Maße + `IsInBounds`/`IsInServiceBox`), generierte Geometrie, Netz, Umgebung, Gizmos | Screenshot zeigt korrekten Platz | ✅ |
| **M2** | Ball: eigene Integration mit Luftwiderstand, Absprung, Trajektorien-Solver, Landepunkt-Vorhersage → **Zielmarker** | Marker sitzt exakt auf dem Aufkommpunkt | ✅ |
| **M3** | Dummy-Figur: Bewegung mit Beschleunigung, Reichweitenring, mitlaufende Kamera | Figur ist steuerbar und lesbar | ✅ |
| **M4** | **Schlagmechanik (Kern):** Idle → Aufladen → Trefferfenster → Erholung. Qualität aus räumlicher *und* zeitlicher Nähe zum Sweet Spot → Perfect/Gut/Zu früh/Zu spät/Daneben. Aufladung steuert Tiefe, Aim die Breite, Qualität die Streuung | Bälle landen kontrolliert im Feld, Qualität ist sichtbar | ✅ |
| **M5** | Aufschlag: Ballwurf, Timing-Fenster, diagonales Aufschlagfeld, erster/zweiter Aufschlag, Doppelfehler | Aufschlag landet regelkonform, Fehler erkannt | ✅ |
| **M6** | Regelwerk: `TennisScore` als reine C#-Klasse (15/30/40, Einstand, Vorteil, Spiele, Satz, Tiebreak), `RuleEvaluator` für Aus/Netz/Doppelaufsprung, Aufschlag- und Seitenwechsel — **mit EditMode-Unit-Tests** | `unity test` grün, Zählweise stimmt in allen Sonderfällen | ⬜ |
| **M7** | Gegner-KI: Anlaufen zum vorhergesagten Punkt → Schlag → Rückkehr zur Grundlinie. Schwierigkeit über Reaktionszeit, Tempo, Timing-Fehler, Zielstreuung | Ballwechsel über mehrere Schläge hält | ⬜ |
| **M8** | Kamera: Seitenwechsel, Aufschluss-Framing. HUD: Punktestand, Aufschlaganzeige, Ladebalken, Punkt-Einblendungen | Ein Satz ist von Anfang bis Ende spielbar | ⬜ |
| **M9** | Balancing über ein zentrales Tuning-Asset, Politur, Übergabe an Teil 2 | Spielgefühl sitzt, Prefabs sind assettauschbereit | ⬜ |

**M4 war der Risikoposten** — dort steckt das Spielgefühl. Nach den Spieltests wurde die
Steuerung dort noch einmal umgebaut: das Aufladen ist raus, die Richtung wird aus drei Zonen
gewählt und ist als Marker sichtbar (siehe [progress.md](progress.md), Abschnitt „Steuerung").
Die Zahlen stehen in `SwingConfig.asset` und `ServeConfig.asset` und sind ohne Codeänderung
verstellbar; `Tools/sweep_shots.cs` sagt, was dabei herauskommt.

### Wichtig für M7

Die KI muss dieselbe Vorhersage-API nutzen wie der Zielmarker (`BallSimulation.Predict`) und
denselben Körper wie der Spieler (`TennisCharacter` + `CharacterMotion`). Schwierigkeitsgrade
ändern **Entscheidungen** (Reaktionszeit, Timing-Fehler, Zielstreuung), niemals die
Bewegungsfähigkeiten. Kein Schummeln über schnellere Beine oder Wissen, das der Spieler nicht hat.

---

## Teil 2 — Blender-Assets (Ausblick)

Erst nach M9. Grober Rahmen, noch nicht ausdetailliert:

1. Figuren in Blender modellieren, riggen und animieren (Laufen, Vorhand, Rückhand, Aufschlag)
2. Export nach Unity, `Visual`-Child der Prefabs tauschen
3. Animator an die vorhandenen Schlag-Events hängen
4. Schläger, Netz, Umgebung, Stadion ersetzen
5. Beleuchtung und Materialien nachziehen

Weil in echten Metern gebaut wurde, importieren die Modelle 1:1.
