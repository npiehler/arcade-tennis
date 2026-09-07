# Arcade Tennis — Fortschritt und Arbeitsanleitung

Übergabedokument für spätere Claude-Sessions. Der Spielentwurf steht in [plan.md](plan.md);
hier steht, **was fertig ist** und **wie man an diesem Projekt arbeitet**.

Stand: M0–M4 abgeschlossen. Als Nächstes **M5 (Aufschlag)**.

---

## 1. Umgebung

- **Unity 6000.5.5f1**, URP, neues Input System (`activeInputHandler: 1`)
- **Unity CLI 1.0.0-beta.8** unter `~/.unity/bin/unity` (nicht global im PATH von
  nicht-interaktiven Shells — immer selbst setzen)
- **`com.unity.pipeline` 0.6.0-exp.1** im Projekt: das ist die Brücke, über die die CLI den
  laufenden Editor steuert
- MCP-Server `unity-editor-mcp` ist in `~/.claude.json` registriert (User-Scope, absoluter
  Binärpfad)
- **Git**, Remote `https://github.com/npiehler/arcade-tennis`, Branch `main`.
  `.plastic/` existiert aus einer früheren Unity-Version, wird nicht benutzt und ist ignoriert

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity status          # zeigt Port, State, Projekt, PID
```

Erwartete Ausgabe: `7800  ready  /Users/nicopiehler/Arcade Tennis  6000.5.5f1  <PID>`.
`ready` = Edit Mode, `playing` = Play Mode. Leere Tabelle = Editor läuft nicht oder das
Pipeline-Paket fehlt.

### Git

`.gitignore` hält `Library/` (2,3 GB), `Temp/`, `Logs/`, `UserSettings/`, die generierten
`.csproj`/`.sln`-Dateien und `Assets/Temp/` (Screenshot-Ablage) draußen. Der Basis-Commit
umfasst 159 Dateien, rund 1,1 MB.

Vor größeren Umbauten committen — es gibt jetzt einen Punkt zum Zurückrollen. Autor ist lokal
im Repository gesetzt (`git config user.name` / `user.email`), nicht global.

---

## 2. CLI-Kochbuch

Alles läuft über `unity cmd <befehl>`. `unity list` zeigt ~150 verfügbare Befehle.

```bash
# Kommandos mit Parametern: --flag wert  (NICHT key=wert)
unity cmd create_gameobject --name Ground --primitive plane --no-banner --json

# Vektor-Parameter als JSON-String
unity cmd set_transform --target /Ground --position "[0,0,0]" --scale "[2,1,2]" --json

# C# im Editor ausführen
unity cmd eval --code 'return UnityEngine.Application.unityVersion;' --json
unity cmd eval_file --file "Tools/setup_match_scene.cs" --json

# Kompilieren und warten
unity cmd recompile --json
unity cmd recompile_status --json      # so lange pollen bis status == "completed"

# Konsole und Play Mode
unity cmd get_console_logs --json
unity cmd clear_console --json
unity cmd editor_play --json
unity cmd editor_stop --json
```

### Fallstricke (alle hart erarbeitet)

**`eval` erlaubt keine `using`-Direktiven.** Der Host wickelt den Code in einen Methodenrumpf,
`using X;` wird als *using-Statement* geparst und schlägt fehl. Immer voll qualifizieren:
`UnityEngine.Vector3`, `UnityEditor.AssetDatabase`, `ArcadeTennis.Court.CourtDefinition`.
Lambdas über `System.Func<>` gehen; lokale Funktionen lieber meiden.

**Pfade lösen gegen den Authoring-Root `Assets/` auf.** `capture_game_view --save_path
"Temp/x.png"` legt die Datei als **Asset** unter `Assets/Temp/x.png` ab, nicht in `Temp/`.
Danach immer `unity cmd delete_asset --asset "Assets/Temp" --confirm true` aufräumen, sonst
sammeln sich Screenshots im Projekt.

**`delete_asset` nimmt `--asset`, nicht `--path`.** Bei Parameterfehlern nennt die
Fehlermeldung den korrekten Namen; alternativ:
`unity cmd --query <befehl> --detail full --json` zeigt das komplette Schema.

**`import_asset` ist für *externe* Dateien** (kopiert von außerhalb ins Projekt). Um eine
extern geschriebene Datei im Projekt neu einzulesen:

```bash
unity cmd eval --code 'UnityEditor.AssetDatabase.ImportAsset("Assets/pfad.ext",
  UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
  return "ok";' --json
```

**Das Nutzergebnis steckt in `data.result`, nicht in `result`** — und dort als
JSON-*String*, nicht als Objekt. Wer auf der obersten Ebene nachschaut, bekommt still `None`
und pollt endlos. Gilt für alle Kommandos:

```python
import sys, json
d = json.load(sys.stdin)
r = (d.get("data") or {}).get("result")
if isinstance(r, str):
    r = json.loads(r)          # bei eval/eval_file bleibt es ein String
```

**Der Editor ist beim Domain-Reload zeitweise nicht erreichbar.** Nach `recompile`,
`editor_play` und `editor_stop` kommt oft „Network error" oder „No Unity Editor instances
found". Das ist normal — in einer Schleife auf `unity status` warten, nicht abbrechen.

### Play Mode — drei Stolperfallen

1. **Das Play Mode tickt nur mit Unity-Fensterfokus.** Auch mit aktivem `Run In Background`
   (steht in `ProjectSettings.asset` auf `1`). Vor Play-Mode-Tests:
   `open -a "/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app"`.
   Prüfen über `UnityEngine.Time.frameCount` — steigt der nicht, tickt nichts.

2. **`Time.timeScale` überlebt den Play-Mode-Wechsel.** Wer in einem Skript `timeScale = 0`
   setzt (etwa für ein eingefrorenes Einzelbild), findet das in der nächsten Session wieder vor
   und wundert sich, warum sich nichts bewegt. Am Anfang jedes Play-Mode-Tests
   `UnityEngine.Time.timeScale = 1f;` setzen. Symptom: `Time.frameCount` steigt, `Time.time`
   bleibt bei 0.

3. **Auf `playMode == "playing"` warten, nicht auf `ready`.** `ready` heißt Edit Mode. Wer zu
   früh loslegt, führt Skripte im Edit Mode aus, wo keine `Awake`/`OnEnable`/`FixedUpdate`
   laufen — MonoBehaviour-Events feuern dann nicht und man sucht den Fehler im Code statt im
   Testaufbau.

```bash
unity cmd editor_status --json   # -> result.playMode
```

### Das Spiel über die CLI steuern

Ein Eval-Aufruf kostet rund 0,2 s Wanduhrzeit — gegen ein Trefferfenster von 0,17 s ist das
ein Münzwurf. Der Ausweg ist nicht schnelleres Pollen, sondern eine **langsamere Uhr**:

```bash
unity cmd eval --code 'UnityEngine.Time.timeScale = 0.08f; return "ok";' --json
```

Bei `timeScale = 0.08` kostet derselbe Aufruf nur noch 16 ms *Spielzeit*. Die Physik läuft
unverändert weiter — `Time.fixedDeltaTime` ist nicht skaliert, es wird nur seltener getickt.
Damit lässt sich ein Schlag über die CLI sauber timen: pollen, bis `t*` (dieselbe Formel wie
in `SwingSolver.Evaluate`) auf die `ContactDelay` zuläuft, dann loslassen. So entstand der
M4-Live-Test — zweimal „Perfect", Rückschlag mit 25 m/s, Aufkommen 0,72 m vor der
gegnerischen Grundlinie.

Zwei Dinge dabei beachten:

- **`PlayerInputController` vorher abschalten.** Er schreibt den Tastenzustand jeden Frame neu
  und überschreibt sofort, was das Skript gesetzt hat
- **`Time.timeScale` am Ende auf 1 zurücksetzen** — im Play Mode *und* im Edit Mode. Es sind
  zwei getrennte Werte, und der Edit-Mode-Wert überlebt Sessions (siehe M4)

### Reproduzierbare Screenshots

Statt gegen die Spielschleife zu rennen: `Time.timeScale = 0`, Zustand gezielt setzen,
aufnehmen. `Tools/pose_shot.cs` macht genau das für den Ball. Danach `timeScale` zurücksetzen.

---

## 3. Was existiert

### Code — `Assets/_Game/Scripts/Runtime/` (Assembly `ArcadeTennis.Runtime`)

| Datei | Zweck |
|---|---|
| `Court/CourtDefinition.cs` | **Einzige Quelle aller Platzmaße.** `IsInBounds`, `GetServiceBox`, `IsInServiceBox`, `NetHeightAt`, `GetServePosition`, `GetBaselineCentre`, `ClampToPlayArea`, `ClampToOwnHalf` |
| `Court/CourtBuilder.cs` | Erzeugt die Geometrie aus der Definition unter `_Generated`. Idempotent, Kontextmenü „Rebuild Court". Gizmos für Feld, Aufschlagfelder, Netzkurve |
| `Ball/BallPhysicsConfig.cs` | ScriptableObject: Gravitation, Luftwiderstand, Radius, Restitution, Reibung, Substepping |
| `Ball/BallState.cs` | `BallState`, `BallEvent`, `BallPrediction` |
| `Ball/BallSimulation.cs` | **Kern.** `Advance` (Integration + Kollision), `Predict` (Vorhersage), `SolveLaunchVelocity` (Ziel-Solver) |
| `Ball/Ball.cs` | MonoBehaviour-Hülle. `Launch`, `LaunchAt`, `PredictLanding`, Events `Bounced`/`HitNet`/`Launched`, `BounceCount` |
| `Ball/BallLandingMarker.cs` | Zielmarker, Farbe nach drin/aus/Netz |
| `Characters/CharacterConfig.cs` | ScriptableObject: Tempo, Beschleunigung, Bremsen, Deadzone, Reichweite, Trefferhöhe, Drehung |
| `Characters/CharacterMotion.cs` | **Reine Bewegungsfunktion.** `ToWorldIntent` (Seitenspiegelung), `Step` |
| `Characters/TennisCharacter.cs` | Körper für Spieler *und* KI. `SetMoveIntent`, `Teleport`, `HitCentre`, `ReachRadius`, `Side` |
| `Characters/SwingConfig.cs` | ScriptableObject: Aufladen, Trefferfenster, Sweet Spot, Zielweite, Streuung, Bogenhöhe |
| `Characters/SwingSolver.cs` | **Kern der Schlagmechanik.** `Tick` (Zustandsautomat), `Evaluate` (Trefferurteil), `ResolveTarget`, `ResolveApex` — alles rein |
| `Characters/SwingController.cs` | Schlag als Fähigkeit des Körpers. `SetSwingHeld`, `SetAim`, Events `SwingStarted`/`Contacted`, `LastContact` |
| `Characters/PlayerInputController.cs` | Liest `Move` und `Swing` aus dem Action Map und füttert Körper und Schlag |
| `Presentation/MatchCamera.cs` | Kamera hinter der Grundlinie, **feste Rotation**, seitliche Parallelfahrt |
| `Presentation/ReachIndicator.cs` | Reichweitenring, Mesh zur Laufzeit erzeugt (`HideAndDontSave`) |
| `Presentation/SwingIndicator.cs` | Ladebalken über der Figur, blitzt nach dem Schlag in der Farbe der Trefferqualität |
| `Utility/RingMesh.cs` | Ringmesh-Generator |
| `Debug/BallFeeder.cs` | **Gerüst für M4, in M5 löschen.** Spielt Bälle an, damit der Schlag ohne Aufschlag geübt werden kann |
| `Input/TennisControls.inputactions` | Actions `Move`, `Swing`, `Pause`; Tastatur + Gamepad |

### Assets

- Szene: `Assets/_Game/Scenes/Match.unity` (Build-Index 1)
- Settings: `CourtDefinition.asset`, `BallPhysicsConfig.asset`, `CharacterConfig.asset`,
  `SwingConfig.asset`
- 15 Materialien unter `Assets/_Game/Materials/`

### Szenenaufbau (`Match.unity`, 8 Roots)

`Sun` · `Court` (+`_Generated`, 75 Teile) · `Match Camera` · `Ball` · `Ball Systems`
(Marker + Feeder) · `Player` (+`Visual`, +`Swing Bar`) · `Opponent` (+`Visual`) · `Reach Ring`

Der Ladebalken hängt bewusst **nicht** unter `Visual`: in Teil 2 wird `Visual` gegen das
Blender-Modell getauscht, das Feedback soll das überleben.

**`Tools/setup_match_scene.cs` ist die Quelle der Wahrheit für die Szene.** Das Skript leert die
Szene und baut sie komplett neu auf — idempotent. Nach Codeänderungen an Szenenobjekten:

```bash
unity cmd eval_file --file "Tools/setup_match_scene.cs" --json
```

Wer die Szene von Hand in Unity ändert, muss das Skript nachziehen, sonst geht die Änderung beim
nächsten Rebuild verloren.

### `Tools/` — außerhalb von `Assets/`, wird nie mitkompiliert

| Datei | Zweck |
|---|---|
| `setup_match_scene.cs` | Baut die komplette Szene neu auf |
| `verify_court.cs` | 18 Prüfungen: Maße, Aus/Drin, Aufschlagfelder, Netz, generierte Geometrie |
| `verify_ball.cs` | 13 Prüfungen: Vorhersagegenauigkeit, Solver, Netz, Tunneling, Energieverlust |
| `verify_character.cs` | 21 Prüfungen: Steuerung, Tempo, Bremsen, Grenzen, Netzlinie, Szenenverdrahtung |
| `verify_swing.cs` | 54 Prüfungen: Zustandsautomat, Trefferurteil, Ziel und Bogen, Flug durch die echte Ballsimulation, Netz/Drin/Aus-Bänder, Szenenverdrahtung |
| `sweep_charge.cs` | **Kein Test, ein Stellwerkzeug.** Fährt die Aufladung von 0 bis 1 und meldet, was der Ball tut — nach jeder Änderung an `SwingConfig.asset` laufen lassen |
| `pose_shot.cs` | Ball für Screenshots eingefroren mitten in den Flug stellen (`SHOT_INDEX`/`STEPS` werden per `sed` ersetzt) |
| `pose_net_shot.cs` | Dasselbe für einen Netztreffer |
| `pose_swing.cs` + `pose_swing_freeze.cs` | Stellt Ladebalken und Schlag für Screenshots ein. Streckt die Ladezeit, weil ein CLI-Aufruf sonst länger dauert als die ganze Aufladung; `pose_swing_freeze.cs` gibt sie zurück |

Alle vier Prüfsuiten laufen **ohne Play Mode** und ohne Timing-Abhängigkeit, weil Ball- und
Figurenbewegung reine Funktionen sind.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd "/Users/nicopiehler/Arcade Tennis"
for f in verify_court verify_ball verify_character verify_swing; do
  unity cmd eval_file --file "Tools/$f.cs" --timeout 120000 --json
done
```

**Erwartet: 18 / 13 / 21 / 54 PASS, 0 FAIL.** Nach jeder Änderung laufen lassen. Neue Mechanik
bekommt eine eigene `verify_*.cs`.

---

## 4. Erledigte Meilensteine

### M0 — Gerüst ✅

Ordnerstruktur `Assets/_Game/`, Assembly Definition, Szene `Match.unity`, eigene
`TennisControls.inputactions`.

Die Unity Starter Assets (Third-/First-Person-Controller, 92 MB) wurden gelöscht, nachdem
geprüft war, dass keine ihrer 201 GUIDs von `ProjectSettings/` oder anderen Assets referenziert
wird.

⚠️ **`Assets/InputSystem_Actions.inputactions` nicht löschen.** Die Template-Datei ist in
`ProjectSettings/EditorBuildSettings.asset` als projektweite Input-Actions registriert. Unsere
eigenen Actions liegen getrennt davon unter `_Game/Scripts/Runtime/Input/`.

### M1 — Court ✅

`CourtDefinition` + `CourtBuilder`, 75 generierte Teile, Netz mit Durchhang und weißem Band,
Auslaufzone, umgebender Boden bis zum Horizont.

### M2 — Ball ✅

Eigene Integration mit quadratischem Luftwiderstand (`0,020` = `0,5·ρ·Cd·A/m` eines echten
Tennisballs). Substepping nach Geschwindigkeit (max. 5 cm pro Schritt), deshalb kein Tunneln
selbst bei 60 m/s.

Der Ziel-Solver hat wegen des Luftwiderstands keine geschlossene Lösung: er startet bei der
widerstandsfreien Parabel und korrigiert die Horizontalgeschwindigkeit iterativ gegen die
simulierte Reichweite. **Zielfehler 1 mm, Vorhersagefehler 0,00000 m.**

Der Solver zielt bewusst **netzfrei** — ob das Netz dazwischenkommt, ist ein Spielergebnis, das
M6 bewertet, kein Grund das Zielen zu verzerren.

**Optische Ballgröße ≠ physikalische:** `visualScale` (2,6×) vergrößert nur das Mesh. Ein
regelkonformer Ball misst 66 mm und war von der Grundlinienkamera unsichtbar. Die Simulation
rechnet weiter mit 33 mm Radius.

### M3 — Figur und Kamera ✅

`CharacterMotion` als reine Funktion, `TennisCharacter` als Hülle. Steuerung pro Seite
gespiegelt. Reichweitenring. Kamera fährt seitlich mit.

Zwei Fehler, die erst der Betrieb zeigte:

- **Spieler konnte durchs Netz laufen.** `ClampToPlayArea` kannte nur den Gesamtplatz. Neu:
  `ClampToOwnHalf(position, side, netClearance = 0.35f)`. `CharacterMotion` und `Teleport`
  nutzen jetzt diese Variante
- **Kamera kippte beim Mitlaufen.** Sie zielte auf einen festen Punkt; bei seitlichem Versatz
  wächst die Distanz dorthin, die Neigung wird flacher, der Platz wandert im Bild. Jetzt ist die
  Rotation **konstant** (`BaseRotation()` aus der zentrierten Position) und die Kamera fährt nur
  parallel mit. Neigung bleibt bei 20,62°

### M4 — Schlagmechanik ✅

Der Meilenstein, der über das Spielgefühl entscheidet. Ablauf: **Idle → Aufladen →
Trefferfenster → Erholung**. Taste halten lädt in 0,6 s auf volle Kraft, Loslassen startet den
Schwung, 0,09 s später trifft der Schläger. Genau dieser Versatz macht den Schlag zu einer
Timing-Entscheidung — man schlägt *bevor* der Ball da ist, nicht wenn er schon da ist.

**Das Trefferurteil kommt aus einer einzigen Geometrie.** Für Relativposition `r` und
Relativgeschwindigkeit `v` liegt die dichteste Annäherung bei `t* = -(r·v)/(v·v)`. Deren
Abstand ist die räumliche Güte, `-t*` die zeitliche — mit Vorzeichen, also inklusive der
Unterscheidung „zu früh" von „zu spät".

Das ist der Grund, warum hier **gerechnet und nicht simuliert** wird: eine Vorwärtssimulation
kann Verspätung erst im Nachhinein bemerken, die geschlossene Form kennt sie sofort und
vorzeichenrichtig. Über die wenigen Hundertstel eines Schwungs ist die Gerade eine sehr gute
Näherung der Flugbahn.

Beide Hälften werden **multipliziert**, nicht gemittelt: perfektes Timing soll einen Ball am
äußersten Schlägerrand nicht schönrechnen.

Weiter:

- **Aufladung → Tiefe, Aim → Breite, Qualität → Streuung.** Die Aufladung spannt vom Netzfuß
  (0,60 m dahinter) bis 1,70 m **hinter** die gegnerische Grundlinie, also über beide Fehler
  hinaus. Zu kurz gedrückt fällt der Ball ins Netz, zu lang gedrückt geht er ins Aus
- **Ein schwacher Schlag wird kaum angehoben** (`ApexWeak` 0,50 gegen `ApexFull` 1,45). Ohne
  das rettet der hohe Bogen selbst den schwächsten Ball über das Netz, und die untere Hälfte
  der Aufladung bedeutet nichts mehr
- **Der Bogen-Boden skaliert mit der Kraft.** `MinPeakHeight` sorgt dafür, dass ein knapp über
  dem Boden getroffener Ball mit vollem Schwung noch übers Netz kommt — aber eben nur mit
  vollem Schwung. Als feste Untergrenze hätte er jedem Stochern dieselbe Garantie geschenkt
- **Qualität kostet nur wenig Tiefe** (`MinQualityPower` 0,80). Höher angesetzt als anfangs,
  damit die Haltezeit die Tiefe bestimmt und nicht der Zufall des Kontakts — sonst ist die
  Aufladung nicht erlernbar. Die Strafe für schlechten Kontakt liegt in der **Streuung**
Gemessen mit `Tools/sweep_charge.cs`, sauberer Kontakt, Haltezeit bei `ChargeTime` = 0,6 s:

| Treffpunkt | Netz | Drin | Aus |
|---|---|---|---|
| Am Netz (z = −6) | bis 0,06 s | 0,06–0,51 s | ab 0,54 s |
| Grundlinie (z = −11) | bis 0,12 s | 0,12–0,51 s | ab 0,54 s |
| Hinter der Linie (z = −13,5) | bis 0,15 s | 0,15–0,51 s | ab 0,54 s |

Wer weiter hinten steht, hat mehr Netzrisiko — die Position bekommt dadurch Bedeutung. Rund
zwei Drittel der Aufladung sind sicher; das Halten der Taste über 0,54 s hinaus geht **immer**
ins Aus, weil die Aufladung bei 1,0 deckelt.

- **Der Zielmarker bleibt gültig**, weil der Rückschlag über `Ball.LaunchAt` läuft und damit
  über dieselbe Simulation
- **Beide Figuren bekommen den `SwingController`**, nicht nur der Spieler — die KI in M7 fährt
  denselben Körper mit denselben Grenzen
- **Schlagvarianten bleiben abgewählt**, aber jede Zahl, die die *Form* eines Schlags
  beschreibt, steht in `SwingConfig`. Varianten kämen später als weitere Assets dieses Typs
  dazu, nicht als Umbau

Ein Fund am Rande: `Time.timeScale` stand im Edit Mode noch auf 0 — der Rest eines
M2-Screenshot-Skripts, der einen Session-Wechsel überlebt hat. Zurückgesetzt.

---

## 5. Nächster Schritt — M5 (Aufschlag)

Vorher lohnt ein **längerer Spieltest mit der Hand am Controller**. Die Zahlen aus M4 sind in
`SwingConfig.asset` gesammelt und ohne Codeänderung verstellbar; die interessanten sind
`ChargeTime`, `ContactDelay`, `TimingWindow` und `SweetSpotRadius`.

Zu bauen:

1. **Ballwurf**: Ball steigt beim Tastendruck, Trefferfenster nahe dem Scheitel
2. **Aufschlagrichtung** über `CourtDefinition.GetServiceBox` / `IsInServiceBox` — beides
   existiert seit M1 und ist geprüft
3. **Aufschlagposition** über `GetServePosition(serverSide, deuceCourt)`, ebenfalls vorhanden
4. **Erster und zweiter Aufschlag**, Doppelfehler
5. **`Debug/BallFeeder.cs` und seine Verdrahtung im Setup-Skript entfernen**, sobald der
   Aufschlag Bälle ins Spiel bringt
6. Neue `Tools/verify_serve.cs` mit denselben Standards

Netzroller beim Aufschlag zählen laut Plan als normal — **kein Let**, das spart einen
Sonderfall.

---

## 6. Arbeitsweise

- **Codekommentare auf Englisch**, passend zu den C#-Bezeichnern. Kommunikation mit dem Nutzer
  auf Deutsch
- **Kommentare erklären das Warum**, nicht das Was. Vorbild: die vorhandenen Dateien
- **Jede neue Mechanik bekommt eine `verify_*.cs`** und wird vor der Meldung „fertig"
  ausgeführt. Behauptungen ohne Prüfergebnis sind wertlos
- **Nach jedem Meilenstein** Screenshot aus der Spielkamera und Konsole auf Fehler prüfen
- **Aufräumen:** `Assets/Temp` nach Screenshots löschen, Gerüstcode entfernen wenn er seinen
  Zweck erfüllt hat
- **Diese Datei aktuell halten.** Nach jedem Meilenstein Status in `plan.md` und Abschnitte 3–5
  hier nachziehen
