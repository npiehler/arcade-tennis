// Editor-side scene assembly for the Match scene. Lives outside Assets/ on
// purpose so Unity never compiles it into the game; it runs via
// `unity cmd eval_file`. The eval host wraps this in a method body, so no
// using-directives here -- every type is fully qualified.

var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.name != "Match") return "ABORT: active scene is " + scene.name;

// Clear the scene so re-running this stays idempotent.
foreach (var root in scene.GetRootGameObjects())
    UnityEngine.Object.DestroyImmediate(root);

// --- Lighting -----------------------------------------------------------
var lightGo = new UnityEngine.GameObject("Sun");
var light = lightGo.AddComponent<UnityEngine.Light>();
light.type = UnityEngine.LightType.Directional;
light.intensity = 1.4f;
light.shadows = UnityEngine.LightShadows.Soft;
light.color = new UnityEngine.Color(1f, 0.97f, 0.90f);
lightGo.transform.rotation = UnityEngine.Quaternion.Euler(52f, -35f, 0f);

// --- Court --------------------------------------------------------------
var courtGo = new UnityEngine.GameObject("Court");
var builder = courtGo.AddComponent<ArcadeTennis.Court.CourtBuilder>();

var so = new UnityEditor.SerializedObject(builder);
var courtDef = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
so.FindProperty("court").objectReferenceValue = courtDef;
so.FindProperty("surfaceMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Surface.mat");
so.FindProperty("apronMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Apron.mat");
so.FindProperty("lineMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Line.mat");
so.FindProperty("netMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Net.mat");
so.FindProperty("netTapeMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_NetTape.mat");
so.FindProperty("outfieldMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Outfield.mat");
so.FindProperty("postMaterial").objectReferenceValue =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Court_Post.mat");
so.ApplyModifiedPropertiesWithoutUndo();

builder.Rebuild();


// --- Characters ---------------------------------------------------------
// Logic on the root, everything visible under a "Visual" child. Replacing the
// placeholder capsule with a rigged Blender model later means swapping that
// child, not rewiring the mechanics.
var characterConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.CharacterConfig>(
    "Assets/_Game/Settings/CharacterConfig.asset");
var swingConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var serveConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.ServeConfig>(
    "Assets/_Game/Settings/ServeConfig.asset");
var controlsAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
    "Assets/_Game/Scripts/Runtime/Input/TennisControls.inputactions");

System.Func<string, UnityEngine.PrimitiveType, UnityEngine.Transform, UnityEngine.Vector3,
            UnityEngine.Vector3, string, UnityEngine.GameObject> makePart =
    (partName, prim, parent, localPos, localScale, matPath) =>
    {
        var part = UnityEngine.GameObject.CreatePrimitive(prim);
        part.name = partName;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<UnityEngine.Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        if (!string.IsNullOrEmpty(matPath))
            part.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial =
                UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(matPath);
        return part;
    };

System.Func<string, int, string, UnityEngine.Vector3, ArcadeTennis.Characters.TennisCharacter> makeCharacter =
    (charName, charSide, bodyMaterial, spawn) =>
    {
        var root = new UnityEngine.GameObject(charName);
        root.transform.position = spawn;

        var visualRoot = new UnityEngine.GameObject("Visual").transform;
        visualRoot.SetParent(root.transform, false);

        // Capsule primitive is 2 units tall, so 0.9 vertical scale gives 1.8 m.
        makePart("Body", UnityEngine.PrimitiveType.Capsule, visualRoot,
            new UnityEngine.Vector3(0f, 0.9f, 0f), new UnityEngine.Vector3(0.5f, 0.9f, 0.5f), bodyMaterial);

        makePart("Racket Head", UnityEngine.PrimitiveType.Cube, visualRoot,
            new UnityEngine.Vector3(0.52f, 1.12f, 0.14f),
            new UnityEngine.Vector3(0.26f, 0.34f, 0.03f), "Assets/_Game/Materials/Racket.mat");

        makePart("Racket Handle", UnityEngine.PrimitiveType.Cube, visualRoot,
            new UnityEngine.Vector3(0.42f, 0.86f, 0.11f),
            new UnityEngine.Vector3(0.05f, 0.28f, 0.05f), "Assets/_Game/Materials/Racket.mat");

        var character = root.AddComponent<ArcadeTennis.Characters.TennisCharacter>();
        var charSo = new UnityEditor.SerializedObject(character);
        charSo.FindProperty("config").objectReferenceValue = characterConfig;
        charSo.FindProperty("court").objectReferenceValue = courtDef;
        charSo.FindProperty("visual").objectReferenceValue = visualRoot;
        charSo.FindProperty("side").intValue = charSide;
        charSo.ApplyModifiedPropertiesWithoutUndo();

        // Both sides get the stroke, not just the player: milestone 7's AI drives
        // the same component, so nothing about the body differs between them.
        var swingComp = root.AddComponent<ArcadeTennis.Characters.SwingController>();
        var swingSo = new UnityEditor.SerializedObject(swingComp);
        swingSo.FindProperty("config").objectReferenceValue = swingConfig;
        swingSo.ApplyModifiedPropertiesWithoutUndo();

        // Same reasoning for the serve: milestone 7's AI serves through this very
        // component, so the opponent gets one too.
        var serveComp = root.AddComponent<ArcadeTennis.Characters.ServeController>();
        var serveSo = new UnityEditor.SerializedObject(serveComp);
        serveSo.FindProperty("config").objectReferenceValue = serveConfig;
        serveSo.FindProperty("rallySwing").objectReferenceValue = swingComp;
        serveSo.ApplyModifiedPropertiesWithoutUndo();

        visualRoot.rotation = UnityEngine.Quaternion.LookRotation(
            new UnityEngine.Vector3(0f, 0f, -charSide), UnityEngine.Vector3.up);

        return character;
    };

var player = makeCharacter("Player", -1, "Assets/_Game/Materials/Player_Body.mat",
    courtDef.GetBaselineCentre(-1));
player.gameObject.AddComponent<ArcadeTennis.Characters.PlayerInputController>();
var inputSo = new UnityEditor.SerializedObject(
    player.GetComponent<ArcadeTennis.Characters.PlayerInputController>());
inputSo.FindProperty("controls").objectReferenceValue = controlsAsset;
inputSo.ApplyModifiedPropertiesWithoutUndo();

// The opponent is a body without a brain until milestone 7.
var opponent = makeCharacter("Opponent", 1, "Assets/_Game/Materials/Opponent_Body.mat",
    courtDef.GetBaselineCentre(1));

// --- Reach ring ---------------------------------------------------------
var ringGo = new UnityEngine.GameObject("Reach Ring");
ringGo.AddComponent<UnityEngine.MeshFilter>();
ringGo.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Reach_Ring.mat");
var reach = ringGo.AddComponent<ArcadeTennis.Presentation.ReachIndicator>();
var reachSo = new UnityEditor.SerializedObject(reach);
reachSo.FindProperty("character").objectReferenceValue = player;
reachSo.ApplyModifiedPropertiesWithoutUndo();

// --- Camera -------------------------------------------------------------
// Behind the baseline of the near player (side -1), looking over the net.
var def = builder.Court;
var camGo = new UnityEngine.GameObject("Match Camera");
camGo.tag = "MainCamera";
var cam = camGo.AddComponent<UnityEngine.Camera>();
camGo.AddComponent<UnityEngine.AudioListener>();
cam.fieldOfView = 32f;
cam.nearClipPlane = 0.3f;
cam.farClipPlane = 200f;

// Framing lives on MatchCamera: pulled well back with a narrow FOV, so the far
// baseline stays large while the ground a player retreats to is still covered.
var matchCamera = camGo.AddComponent<ArcadeTennis.Presentation.MatchCamera>();
var camSo = new UnityEditor.SerializedObject(matchCamera);
camSo.FindProperty("court").objectReferenceValue = courtDef;
camSo.FindProperty("target").objectReferenceValue = player.transform;
camSo.FindProperty("side").intValue = -1;
camSo.ApplyModifiedPropertiesWithoutUndo();
matchCamera.ApplyImmediate();


// --- Ball ---------------------------------------------------------------
var ballGo = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
ballGo.name = "Ball";
UnityEngine.Object.DestroyImmediate(ballGo.GetComponent<UnityEngine.Collider>());
ballGo.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Ball.mat");

var ball = ballGo.AddComponent<ArcadeTennis.BallPhysics.Ball>();
var ballConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

var ballSo = new UnityEditor.SerializedObject(ball);
ballSo.FindProperty("config").objectReferenceValue = ballConfig;
ballSo.FindProperty("court").objectReferenceValue = courtDef;
ballSo.ApplyModifiedPropertiesWithoutUndo();

ballGo.transform.position = new UnityEngine.Vector3(0f, 1.1f, -12.2f);
ballGo.transform.localScale = UnityEngine.Vector3.one * ballConfig.Radius * 2f * 2.6f;

// --- Landing marker -----------------------------------------------------
var markerGo = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cylinder);
markerGo.name = "Landing Marker";
UnityEngine.Object.DestroyImmediate(markerGo.GetComponent<UnityEngine.Collider>());
markerGo.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Marker.mat");
markerGo.transform.position = new UnityEngine.Vector3(0f, 0.012f, 0f);
markerGo.transform.localScale = new UnityEngine.Vector3(0.9f, 0.004f, 0.9f);

var systemsGo = new UnityEngine.GameObject("Ball Systems");
markerGo.transform.SetParent(systemsGo.transform, true);

var markerComp = systemsGo.AddComponent<ArcadeTennis.BallPhysics.BallLandingMarker>();
var markerSo = new UnityEditor.SerializedObject(markerComp);
markerSo.FindProperty("ball").objectReferenceValue = ball;
markerSo.FindProperty("marker").objectReferenceValue = markerGo.transform;
markerSo.ApplyModifiedPropertiesWithoutUndo();

// --- Swing wiring -------------------------------------------------------
// Done here rather than in makeCharacter because the ball does not exist yet
// at the point the characters are built.
foreach (var swinger in new ArcadeTennis.Characters.TennisCharacter[] { player, opponent })
{
    var sc = swinger.GetComponent<ArcadeTennis.Characters.SwingController>();
    var scSo = new UnityEditor.SerializedObject(sc);
    scSo.FindProperty("ball").objectReferenceValue = ball;
    scSo.ApplyModifiedPropertiesWithoutUndo();

    var srv = swinger.GetComponent<ArcadeTennis.Characters.ServeController>();
    var srvSo = new UnityEditor.SerializedObject(srv);
    srvSo.FindProperty("ball").objectReferenceValue = ball;
    srvSo.FindProperty("landingMarker").objectReferenceValue = markerComp;
    srvSo.ApplyModifiedPropertiesWithoutUndo();
}

// --- Charge bar ---------------------------------------------------------
// Under the logic root, deliberately not under Visual: part 2 swaps Visual out
// for a rigged model and this feedback has to survive that.
var barRoot = new UnityEngine.GameObject("Swing Bar");
barRoot.transform.SetParent(player.transform, false);

var barBack = makePart("Back", UnityEngine.PrimitiveType.Cube, barRoot.transform,
    UnityEngine.Vector3.zero, new UnityEngine.Vector3(1.17f, 0.18f, 0.02f),
    "Assets/_Game/Materials/Swing_Bar_Back.mat");

var barFill = makePart("Fill", UnityEngine.PrimitiveType.Cube, barRoot.transform,
    new UnityEngine.Vector3(0f, 0f, -0.01f), new UnityEngine.Vector3(1.10f, 0.13f, 0.02f),
    "Assets/_Game/Materials/Swing_Bar_Fill.mat");

var indicator = barRoot.AddComponent<ArcadeTennis.Presentation.SwingIndicator>();
var indicatorSo = new UnityEditor.SerializedObject(indicator);
indicatorSo.FindProperty("swing").objectReferenceValue =
    player.GetComponent<ArcadeTennis.Characters.SwingController>();
indicatorSo.FindProperty("serve").objectReferenceValue =
    player.GetComponent<ArcadeTennis.Characters.ServeController>();
indicatorSo.FindProperty("character").objectReferenceValue = player.transform;
indicatorSo.FindProperty("fill").objectReferenceValue = barFill.transform;
indicatorSo.FindProperty("background").objectReferenceValue = barBack.transform;
indicatorSo.ApplyModifiedPropertiesWithoutUndo();

// --- Aim zone marker ----------------------------------------------------
// Ring around the chosen zone plus a line to it, drawn from the same numbers
// the solver aims with. Own root rather than a child of the player: it lives
// on the far side of the court, not on the character.
var aimGo = new UnityEngine.GameObject("Aim Zone");

var ringGo2 = new UnityEngine.GameObject("Zone Ring");
ringGo2.transform.SetParent(aimGo.transform, false);
ringGo2.AddComponent<UnityEngine.MeshFilter>();
ringGo2.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial =
    UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Game/Materials/Aim_Zone.mat");

var aimLine = makePart("Aim Line", UnityEngine.PrimitiveType.Cube, aimGo.transform,
    UnityEngine.Vector3.zero, UnityEngine.Vector3.one, "Assets/_Game/Materials/Aim_Zone.mat");

var aimIndicator = aimGo.AddComponent<ArcadeTennis.Presentation.AimZoneIndicator>();
var aimSo = new UnityEditor.SerializedObject(aimIndicator);
aimSo.FindProperty("character").objectReferenceValue = player;
aimSo.FindProperty("swing").objectReferenceValue =
    player.GetComponent<ArcadeTennis.Characters.SwingController>();
aimSo.FindProperty("serve").objectReferenceValue =
    player.GetComponent<ArcadeTennis.Characters.ServeController>();
aimSo.FindProperty("court").objectReferenceValue = courtDef;
aimSo.FindProperty("swingConfig").objectReferenceValue = swingConfig;
aimSo.FindProperty("serveConfig").objectReferenceValue = serveConfig;
aimSo.FindProperty("ring").objectReferenceValue = ringGo2.transform;
aimSo.FindProperty("line").objectReferenceValue = aimLine.transform;
aimSo.ApplyModifiedPropertiesWithoutUndo();

// --- Serve practice driver (milestone 5 rig) ---------------------------
// Decides only when the next serve happens. The rule engine in milestone 6
// takes that over and this goes.
var driver = systemsGo.AddComponent<ArcadeTennis.DebugTools.ServePracticeDriver>();
var driverSo = new UnityEditor.SerializedObject(driver);
driverSo.FindProperty("serve").objectReferenceValue =
    player.GetComponent<ArcadeTennis.Characters.ServeController>();
driverSo.FindProperty("ball").objectReferenceValue = ball;
driverSo.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

var generated = courtGo.transform.Find("_Generated");
return "roots=" + scene.GetRootGameObjects().Length
     + " playerAt=" + player.transform.position.ToString("0.0")
     + " opponentAt=" + opponent.transform.position.ToString("0.0")
     + " camAt=" + camGo.transform.position.ToString("0.0")
     + " generatedParts=" + generated.GetComponentsInChildren<UnityEngine.Transform>().Length;
