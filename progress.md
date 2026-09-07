# Arcade Tennis — Fortschritt und Arbeitsanleitung

Übergabedokument für spätere Claude-Sessions. Der Spielentwurf steht in [plan.md](plan.md);
hier steht, **was fertig ist** und **wie man an diesem Projekt arbeitet**.

Stand: M0–M5 abgeschlossen, Steuerung nach den Spieltests umgebaut. Als Nächstes
**M6 (Regelwerk)**.

### Steuerung

| Eingabe | Wirkung |
|---|---|
| **Pfeiltasten** / WASD / linker Stick / D-Pad | Laufen — solange die Leertaste **nicht** gedrückt ist |
| **Leertaste halten** / A halten | Zielen. Der Marker erscheint. Beim Aufschlag fliegt gleichzeitig der Ball hoch |
| **+ Pfeil oben** | tiefes Feld (Voreinstellung bei jedem neuen Druck) |
| **+ Pfeil links / rechts** | kurzes Feld links bzw. rechts |
| **Leertaste loslassen** | Schlagen. Der Ball geht ins gewählte Feld |

**Kein Aufladen.** Halten kostet nichts und bringt nichts außer der Zeit, ein Feld zu wählen —
und dem Ball, der derweil näher kommt.

Dieselben Tasten machen zwei Dinge, aber nie gleichzeitig: frei steuern sie, mit gedrückter
Leertaste wählen sie das Zielfeld, und die Figur bleibt dabei stehen. Das ist auch der Grund
dafür, dass die Laufrichtung nicht mehr aus Versehen die Schlagrichtung bestimmt.

Die Trefferqualität entscheidet, **wie weit** der Ball ins gewählte Feld kommt: sauber
getroffen bis in die Mitte der Zone, schlecht getroffen bis zu `DepthShortfall` davor.

Der Marker hat **eine** Farbe für beide Schlagarten. Vorher war der Aufschlag gelb und der
Ballwechsel blau, was nur die Frage aufwarf, was der Unterschied bedeutet — wo der Ring liegt,
sagt ohnehin, ob gerade aufgeschlagen wird.

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

**`refresh_assets` gibt es nicht.** Der Befehl steht in keiner Kommandoliste und schlägt
still fehl — die Konsole meldet „No command named 'refresh_assets' is available", aber die CLI
gibt Erfolg zurück. Wer sich darauf verlässt, arbeitet gegen einen alten Stand:

```bash
unity cmd eval --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";' --json
```

**Ein extern geändertes `.asset` lädt auch ein Refresh nicht zuverlässig neu.** Das ScriptableObject
bleibt mit den alten Werten im Speicher, und man misst minutenlang gegen eine Einstellung, die
gar nicht mehr auf der Platte steht. Nach jedem Editieren einer `.asset`-Datei von außen:

```bash
unity cmd eval --code 'UnityEditor.AssetDatabase.ImportAsset("Assets/_Game/Settings/SwingConfig.asset",
  UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
  return "ok";' --json
```

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

**Ein Skript-Neuübersetzen im laufenden Play Mode lädt die Domain neu.** Private Felder mit
*Unity-Objekt*-Referenzen überleben das, **einfache verwaltete Objekte nicht** — sie kommen als
`null` zurück, und es läuft kein `Awake` mehr, das sie repariert. Ein `MaterialPropertyBlock`,
der nur in `Awake` erzeugt wird, ist danach weg, während der `Renderer` daneben noch steht.

Das ist nicht kosmetisch: `BallLandingMarker` setzt seine Farbe aus dem `Launched`-Ereignis des
Balls, also flog die Ausnahme aus `Ball.Launch` heraus und **jeder Ballwurf danach schlug fehl**
— mit der Meldung „Value cannot be null. Parameter name: dest", die auf einen Texturparameter
zeigt und nirgendwo in die Nähe der Ursache. Solche Felder deshalb bei Bedarf erzeugen
(`properties ??= new MaterialPropertyBlock()`), nicht nur in `Awake`.

### Play Mode — drei Stolperfallen

1. **Das Play Mode tickt nur mit Unity-Fensterfokus.** Auch mit aktivem `Run In Background`
   (steht in `ProjectSettings.asset` auf `1`). Vor Play-Mode-Tests:
   `open -a "/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app"`.
   Prüfen über `UnityEngine.Time.frameCount` — steigt der nicht, tickt nichts.

2. **`Time.timeScale` überlebt nicht nur den Play-Mode-Wechsel, sondern die ganze Session** —
   der Wert steht in `ProjectSettings/TimeManager.asset` als `m_TimeScale` und wird mit
   committet. In diesem Projekt stand dort monatelang `0`, der Rest eines M2-Screenshot-Skripts;
   das war die eigentliche Ursache dafür, dass der Edit-Mode-Wert immer wieder auf 0 stand.
   Play Mode und Edit Mode haben getrennte Laufzeitwerte, aber beide gehen auf dieselbe Datei
   zurück. Am Anfang jedes Play-Mode-Tests `UnityEngine.Time.timeScale = 1f;` setzen, am Ende
   ebenso — und wenn `git status` `TimeManager.asset` zeigt, hineinschauen. Symptom eines
   vergessenen Werts: `Time.frameCount` steigt, `Time.time` bleibt bei 0.

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
| `Court/AimZones.cs` | **Die drei Zielzonen** als reine Geometrie: ein tiefes Feld hinter der Aufschlaglinie, zwei kurze davor, dasselbe im Aufschlagfeld. Spiegelung pro Seite, Eingabe → Zone |
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
| `Characters/SwingSolver.cs` | **Kern der Schlagmechanik.** `Tick` (Zustandsautomat), `Evaluate` (Trefferurteil), `ResolveTarget`, `ResolveApex` — alles rein. `ContactTuning` trägt die Zahlen, die `Evaluate` braucht, damit Grundschlag *und* Aufschlag dieselbe Beurteilung nutzen |
| `Characters/ServeConfig.cs` | ScriptableObject: Ballwurf, Treffhöhe, Timing, Zielsetzung im Aufschlagfeld, Bogen |
| `Characters/ServeSolver.cs` | **Kern des Aufschlags.** `Tick`, `TossOrigin`/`HitCentre`/`TossVelocity`, `ResolveTarget`, `Judge`, `RegisterFault`, `NextPoint` — alles rein |
| `Characters/ServeController.cs` | Aufschlag als Fähigkeit des Körpers. Besitzt den Ball vom Wurf bis zum Urteil und hält solange den Grundschlag zurück |
| `Characters/SwingController.cs` | Schlag als Fähigkeit des Körpers. `SetSwingHeld`, `SetAim`, Events `SwingStarted`/`Contacted`, `LastContact` |
| `Characters/PlayerInputController.cs` | Liest `Move` und `Swing` aus dem Action Map und füttert Körper und Schlag |
| `Presentation/MatchCamera.cs` | Kamera hinter der Grundlinie, **feste Rotation**, seitliche Parallelfahrt |
| `Presentation/ReachIndicator.cs` | Reichweitenring, Mesh zur Laufzeit erzeugt (`HideAndDontSave`) |
| `Presentation/SwingIndicator.cs` | Blitzt nach dem Schlag in der Farbe der Trefferqualität. War der Ladebalken; ohne Aufladung bleibt die Rückmeldung |
| `Presentation/AimZoneIndicator.cs` | **Der Zielmarker.** Ring um die gewählte Zone plus Linie dorthin, gezeichnet aus denselben Zahlen, mit denen der Solver zielt |
| `Utility/EllipseRingMesh.cs` | Elliptischer Umriss mit gleichmäßiger Strichstärke |
| `Utility/RingMesh.cs` | Ringmesh-Generator |
| `Debug/ServePracticeDriver.cs` | **Gerüst für M5, in M6 löschen.** Entscheidet nur, *wann* der nächste Aufschlag kommt — wer den Punkt gewinnt, ist Sache des Regelwerks |
| `Input/TennisControls.inputactions` | Actions `Move`, `Swing`, `Pause`; Tastatur + Gamepad |

### Assets

- Szene: `Assets/_Game/Scenes/Match.unity` (Build-Index 1)
- Settings: `CourtDefinition.asset`, `BallPhysicsConfig.asset`, `CharacterConfig.asset`,
  `SwingConfig.asset`, `ServeConfig.asset`
- 16 Materialien unter `Assets/_Game/Materials/`
- `Assets/_Game/Shaders/AimMarker.shader` — unbeleuchtet, `ZTest Always`. Der Zielmarker ist
  eine Anzeige und muss auch dann lesbar sein, wenn das Netz davorsteht; URP Lit bietet kein
  `ZTest`

### Szenenaufbau (`Match.unity`, 9 Roots)

`Sun` · `Court` (+`_Generated`, 75 Teile) · `Match Camera` · `Ball` · `Ball Systems`
(Marker + Übungstreiber) · `Player` (+`Visual`, +`Swing Bar`) · `Opponent` (+`Visual`) ·
`Reach Ring` · `Aim Zone` (Ring + Linie)

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
| `verify_swing.cs` | 57 Prüfungen: Zustandsautomat, Trefferurteil, Ziel und Bogen, Ziel-Deadzone, Flug durch die echte Ballsimulation, Netz/Drin/Aus-Bänder, Szenenverdrahtung |
| `verify_serve.cs` | 74 Prüfungen: Aufschlaggeometrie, Ballwurf, Zustandsautomat, erster/zweiter Aufschlag, Zielsetzung, Flug, Urteil, Rhythmus, Szenenverdrahtung |
| `sweep_shots.cs` | **Kein Test, ein Stellwerkzeug.** Fährt die Trefferqualität von 0 bis 1 für beide Schläge und alle drei Zonen und meldet, was der Ball tut, plus die Wurfrhythmus-Tabelle. Nach jeder Änderung an `SwingConfig.asset` oder `ServeConfig.asset` laufen lassen |
| `pose_shot.cs` | Ball für Screenshots eingefroren mitten in den Flug stellen (`SHOT_INDEX`/`STEPS` werden per `sed` ersetzt) |
| `pose_net_shot.cs` | Dasselbe für einen Netztreffer |
| `pose_swing.cs` + `pose_swing_freeze.cs` | Stellt Ladebalken und Schlag für Screenshots ein. Streckt die Ladezeit, weil ein CLI-Aufruf sonst länger dauert als die ganze Aufladung; `pose_swing_freeze.cs` gibt sie zurück |

Alle fünf Prüfsuiten laufen **ohne Play Mode** und ohne Timing-Abhängigkeit, weil Ball- und
Figurenbewegung reine Funktionen sind.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd "/Users/nicopiehler/Arcade Tennis"
for f in verify_court verify_ball verify_character verify_swing verify_serve; do
  unity cmd eval_file --file "Tools/$f.cs" --timeout 120000 --json
done
```

**Erwartet: 18 / 13 / 21 / 57 / 74 PASS, 0 FAIL** — zusammen 183. Nach jeder Änderung laufen lassen. Neue Mechanik
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
Schwung, 0,18 s später trifft der Schläger. Genau dieser Versatz macht den Schlag zu einer
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

- **Seitliche Streuung ist auf 30 % der Streuung gedeckelt** (`LateralSpreadFactor`). Der
  Grund ist keine Feinabstimmung, sondern eine Unterscheidung: die *Länge* daneben ist die
  eigene Fehlzeit und liest sich als fair, die *Richtung* daneben ist das, worum der Spieler
  gebeten hat, und liest sich als kaputtes Spiel. Ohne den Deckel landete ein Schlag ohne
  Richtungstaste bei Qualität 0,4 irgendwo zwischen −1,8 m und +1,8 m; jetzt bei ±0,55 m
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

- **Die Richtung wird beim Treffer gelesen, nicht beim Loslassen eingefroren.** Die 0,18 s
  `ContactDelay` sind deshalb doppelt belegt: sie sind das Timing-Fenster *und* die Zeit, in
  der noch gesteuert werden kann. Wer sie ändert, ändert beides
- **Seitliches Zielen deckelt bei `AimWidth` 0,96.** Das ist der letzte Wert, bei dem ein
  perfekt getroffener Linienball durch die eigene Streuung noch drin bleibt; ab etwa 0,97
  verliert schon der bestmögliche Kontakt die Linie. Die Tastatur kann dem nicht ausweichen,
  weil A/D nur −1, 0 und +1 kennen — volles Zielen ist dort die einzige Seitwärtsoption
- **Zielen benutzt die Bewegungs-Deadzone** aus `CharacterConfig`, nicht eine eigene. Sonst
  bewegt ein ruhender Stick bei 0,10 die Figur nicht, verschiebt das Ziel aber um einen halben
  Meter — „stillstehen" muss eine Bedeutung haben und nicht zwei
- **Der Zielmarker bleibt gültig**, weil der Rückschlag über `Ball.LaunchAt` läuft und damit
  über dieselbe Simulation
- **Beide Figuren bekommen den `SwingController`**, nicht nur der Spieler — die KI in M7 fährt
  denselben Körper mit denselben Grenzen
- **Schlagvarianten bleiben abgewählt**, aber jede Zahl, die die *Form* eines Schlags
  beschreibt, steht in `SwingConfig`. Varianten kämen später als weitere Assets dieses Typs
  dazu, nicht als Umbau

Ein Fund am Rande: `Time.timeScale` stand im Edit Mode noch auf 0 — der Rest eines
M2-Screenshot-Skripts, der einen Session-Wechsel überlebt hat. Zurückgesetzt.

### M5 — Aufschlag ✅

**Ein Tastendruck, kein zweiter.** Drücken wirft den Ball und startet gleichzeitig die
Aufladung, Loslassen schwingt, 0,16 s später trifft der Schläger. Wurf und Aufladung sind
damit **eine** Entscheidung, und genau das musste zusammen eingestellt werden.

Der Rhythmus, gemessen mit `Tools/sweep_serve.cs`:

| Loslassen nach | Was passiert |
|---|---|
| bis 0,15 s | gut getroffen, aber ohne Kraft → **ins Netz** |
| 0,20–0,65 s | **im Aufschlagfeld** |
| 0,70–0,80 s | zu tief gezielt → **hinter die Aufschlaglinie** |
| ab 0,85 s | Ball schon unter der Reichweite → **Schlag ins Leere**, auch das ein Fehler |

Der tiefste noch gültige Aufschlag liegt bei 0,65 s mit perfektem Kontakt — 0,05 s vom Fehler
entfernt. Das ist Absicht.

Weiter:

- **Einen Wurf fallen zu lassen kostet nichts.** Wer nicht schlägt, fängt den Ball und wirft
  neu, wie im echten Tennis. Nur der Schlag ins Leere ist ein Fehler
- **Erster und zweiter Aufschlag** laufen über dieselbe Mechanik. Der zweite wird *nicht*
  automatisch entschärft — kürzer aufzuladen ist die Entscheidung des Spielers, so wie überall
  sonst in diesem Spiel auch
- **Netzroller sind Fehler, kein Let.** Steht so im Plan und spart dem ganzen Spiel einen
  Sonderfall
- **Der Aufschlag besitzt den Ball** vom Wurf bis zum Urteil und setzt solange
  `SwingController.Suspended`. Ohne das schlägt der Grundschlag nach dem eigenen Ballwurf
- **Beurteilt wird das echte Ereignis**, nicht eine zweite Vorhersage: der `ServeController`
  hört auf `Ball.Bounced` und `Ball.HitNet` und fragt `CourtDefinition.IsInServiceBox`

**Zwei Funde beim Bauen:**

`SwingSolver.Evaluate` bekam eine `ContactTuning` statt einer `SwingConfig`. Aufschlag und
Grundschlag werden mit derselben Arithmetik beurteilt, aber niemals mit denselben Zahlen — der
Aufschlag will einen viel engeren Sweet Spot. Die Zahlen zu übergeben statt der Konfiguration
ist das, was eine zweite Kopie des Trefferurteils verhindert.

**Am Scheitel des Wurfs steht der Ball fast still, und dort zerfällt `t* = -(r·v)/(v·v)`** — eine
kleine Zahl geteilt durch eine noch kleinere. Ein bequem erreichbarer Ball kam als „zehn
Sekunden zu früh" heraus und wäre als Fehlschlag durchgefallen. `Evaluate` prüft jetzt, ob der
Ball während eines ganzen Schwungfensters überhaupt weiter reist als der Sweet Spot breit ist;
wenn nicht, gibt es kein „zu früh" mehr zu messen und es entscheidet allein die Platzierung.

### Steuerungsumbau nach den Spieltests ✅

M4 und M5 waren gebaut, aber nach dem Spielen kam die Rückmeldung: das Aufladen soll weg, und
die Richtung soll über einen sichtbaren Marker aus drei Bereichen gewählt werden. Vorbild ist
Robo Tennis von Wavedash.

**Was ersetzt wurde:** Die Aufladung war eine zweite Entscheidung, die gegen die erste getimt
werden musste. Jetzt hält man die Taste zum Zielen — das kostet nichts — und lässt im richtigen
Moment los.

**Die drei Zonen** sind ein tiefes Feld hinter der Aufschlaglinie und zwei kurze davor, links
und rechts. Die Aufteilung folgt der Aufschlaglinie, liegt also auf Markierungen, die der Platz
ohnehin hat. Im Aufschlagfeld dasselbe im Kleinen. `AimZones` rechnet beides aus der
`CourtDefinition`, und der Marker zeichnet sich aus denselben Zahlen — er kann nicht anfangen
zu lügen, wenn jemand die Tiefen verstellt.

**Der Marker muss zeichnen, wo der Ball wirklich landet.** Beim Aufschlag tat er das nicht:
die kurzen Zonen waren bei schlechtem Kontakt gar nicht erreichbar, der Ball kam am Netz an
(`z = 0,0`), der Ring versprach trotzdem das Feld. Beide Prüfsuiten haben dafür jetzt einen
Test, der für jede Zone über die ganze Qualitätsspanne nachrechnet, ob der Aufkommpunkt in der
gezeichneten Ellipse liegt.

**Kurze Bälle brauchen mehr Bogen, nicht weniger.** Der erste Versuch gab allen Zonen dieselbe
Bogenkurve, worauf die kurzen Zonen bis Trefferqualität 0,5 ins Netz fielen und die tiefe nie —
genau verkehrt herum. Einen Ball wenige Meter hinter dem Netz aufkommen zu lassen verlangt, ihn
zu heben und steil fallen zu lassen. Deshalb haben die kurzen Zonen eigene Bogenwerte
(`ApexShortWeak`/`ApexShortFull`), und ein verzogener kurzer Ball wird ein hoher, langsamer —
eine Einladung statt eines gelungenen Stoppballs. Beim Aufschlag galt dasselbe, nur habe ich es
dort zunächst nicht angewendet — das war die Ursache der falschen gelben Zielfelder.

**Zwei Funde beim Bauen:**

Der Aufschlag hatte ein **Vorzeichenfehler** in der neuen Zielrechnung: `nominal.z` trägt das
Vorzeichen des Empfängers bereits, es noch einmal zu negieren zielte *jeden* Aufschlag auf
0,72 m hinter das Netz. Der Sweep zeigte eine Tabelle aus lauter `n` — ohne ihn wäre das erst
beim Spielen aufgefallen.

`refresh_assets` **existiert als CLI-Befehl nicht** und schlug die ganze Zeit still fehl (siehe
Abschnitt 2).

---

## 5. Nächster Schritt — M6 (Regelwerk)

Ab hier wird nicht mehr an der Mechanik gedreht, sondern gezählt.

Zu bauen:

1. **`TennisScore` als reine C#-Klasse**: 15/30/40, Einstand, Vorteil, Spiele, Satz bis 6 mit
   zwei Vorsprung, Tiebreak bei 6:6. Keine `MonoBehaviour`-Abhängigkeit — das ist die Klasse,
   die **EditMode-Unit-Tests** verdient (`unity test`), nicht nur eine `verify_*.cs`
2. **`RuleEvaluator`**: Aus, Netz, Doppelaufsprung, wer den Punkt gewinnt. Die Bausteine
   stehen: `CourtDefinition.IsInBounds`, `Ball.BounceCount`, `ServeSolver.Judge`
3. **Aufschlag- und Seitenwechsel**: Aufschlagrecht nach jedem Spiel, Seitenwechsel nach
   ungeraden Spielen
4. **`Debug/ServePracticeDriver.cs` und seine Verdrahtung entfernen**, sobald das Regelwerk
   entscheidet, wann der nächste Aufschlag kommt
5. Neue `Tools/verify_rules.cs` für die Verdrahtung, Unit-Tests für die Zählweise

Die Sonderfälle stehen im Plan: **kein Let** beim Aufschlag, Einzel, ein Satz.

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
