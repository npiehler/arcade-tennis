# Arcade Tennis — Fortschritt und Arbeitsanleitung

Übergabedokument für spätere Claude-Sessions. Der Spielentwurf steht in [plan.md](plan.md);
hier steht, **was fertig ist** und **wie man an diesem Projekt arbeitet**.

Stand: M0–M3 abgeschlossen. Als Nächstes **M4 (Schlagmechanik)**.

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

### Git

`.gitignore` hält `Library/` (2,3 GB), `Temp/`, `Logs/`, `UserSettings/`, die generierten
`.csproj`/`.sln`-Dateien und `Assets/Temp/` (Screenshot-Ablage) draußen. Der Basis-Commit
umfasst 159 Dateien, rund 1,1 MB.

Vor größeren Umbauten committen — es gibt jetzt einen Punkt zum Zurückrollen. Autor ist lokal
im Repository gesetzt (`git config user.name` / `user.email`), nicht global.

Erwartete Ausgabe: `7800  ready  /Users/nicopiehler/Arcade Tennis  6000.5.5f1  <PID>`.
`ready` = Edit Mode, `playing` = Play Mode. Leere Tabelle = Editor läuft nicht oder das
Pipeline-Paket fehlt.

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

**`recompile_status` liefert `result` als JSON-*String*, nicht als Objekt.** Beim Parsen erst
`json.loads` auf das Feld anwenden. Gilt auch für andere Kommandos.

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
| `Characters/PlayerInputController.cs` | Liest `Move` aus dem Action Map und füttert den Körper |
| `Presentation/MatchCamera.cs` | Kamera hinter der Grundlinie, **feste Rotation**, seitliche Parallelfahrt |
| `Presentation/ReachIndicator.cs` | Reichweitenring, Mesh zur Laufzeit erzeugt (`HideAndDontSave`) |
| `Utility/RingMesh.cs` | Ringmesh-Generator |
| `Debug/BallTestLauncher.cs` | **Gerüst für M2, in M4 löschen**, sobald echte Schläge den Ball antreiben |
| `Input/TennisControls.inputactions` | Actions `Move`, `Swing`, `Pause`; Tastatur + Gamepad |

### Assets

- Szene: `Assets/_Game/Scenes/Match.unity` (Build-Index 1)
- Settings: `CourtDefinition.asset`, `BallPhysicsConfig.asset`, `CharacterConfig.asset`
- 13 Materialien unter `Assets/_Game/Materials/`

### Szenenaufbau (`Match.unity`, 8 Roots)

`Sun` · `Court` (+`_Generated`, 75 Teile) · `Match Camera` · `Ball` · `Ball Systems`
(Marker + Test-Launcher) · `Player` · `Opponent` · `Reach Ring`

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
| `pose_shot.cs` | Ball für Screenshots eingefroren mitten in den Flug stellen (`SHOT_INDEX`/`STEPS` werden per `sed` ersetzt) |
| `pose_net_shot.cs` | Dasselbe für einen Netztreffer |

Alle drei Prüfsuiten laufen **ohne Play Mode** und ohne Timing-Abhängigkeit, weil Ball- und
Figurenbewegung reine Funktionen sind.

```bash
export PATH="$HOME/.unity/bin:$PATH"
cd "/Users/nicopiehler/Arcade Tennis"
for f in verify_court verify_ball verify_character; do
  unity cmd eval_file --file "Tools/$f.cs" --timeout 120000 --json
done
```

**Erwartet: 18 / 13 / 21 PASS, 0 FAIL.** Nach jeder Änderung laufen lassen. Neue Mechanik
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

---

## 5. Nächster Schritt — M4 (Schlagmechanik)

Der Meilenstein, der über das Spielgefühl entscheidet. Nach M4 einen längeren Spieltest
einplanen, bevor Regelwerk und KI draufkommen.

Zu bauen:

1. **Schlag-Zustandsautomat** auf `TennisCharacter`: Idle → Aufladen → Trefferfenster →
   Erholung. Die `Swing`-Action existiert bereits im Action Map
2. **Trefferauswertung** aus räumlicher *und* zeitlicher Nähe zum Sweet Spot:
   Perfect / Gut / Zu früh / Zu spät / Daneben. Räumlich gegen `HitCentre` und `ReachRadius`
3. **Qualität → Ergebnis:** Ballgeschwindigkeit, Zielgenauigkeit, Streuung. Aufladung steuert
   die Tiefe, Aim-Input die Breite
4. **Rückgabe an den Ball** über `Ball.LaunchAt(position, target, apexHeight)` oder direkt
   `BallSimulation.SolveLaunchVelocity`
5. **Feedback:** Ladebalken, Trefferqualität sichtbar machen
6. **Events** `OnSwingStart` / `OnContact` feuern — daran hängt in Teil 2 der Animator
7. `Debug/BallTestLauncher.cs` und die `Ball Systems`-Verdrahtung im Setup-Skript entfernen
8. Neue `Tools/verify_swing.cs` mit denselben Standards

Schlaglogik so bauen, dass Varianten (Topspin, Slice, Lob, Stopp) später als **Datensatz**
dazukommen — sie sind für Teil 1 abgewählt, aber der Umbau soll später keiner sein.

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
