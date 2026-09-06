// Milestone 3 verification. CharacterMotion is a pure function, so this runs
// without entering play mode and without any timing races.
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.CharacterConfig>(
    "Assets/_Game/Settings/CharacterConfig.asset");

float dt = 0.02f;
var sb = new System.Text.StringBuilder();
int failed = 0;

System.Action<string, bool> check = (label, ok) => {
    if (!ok) failed++;
    sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
};

// Runs an intent for a number of ticks and hands back the resulting state.
System.Func<UnityEngine.Vector3, UnityEngine.Vector2, int, int, ArcadeTennis.Characters.CharacterMotionState> run =
    (start, intent, side, ticks) =>
    {
        var st = new ArcadeTennis.Characters.CharacterMotionState(start);
        for (int i = 0; i < ticks; i++)
            ArcadeTennis.Characters.CharacterMotion.Step(ref st, intent, side, cfg, court, dt);
        return st;
    };

// --- steering is mirrored per side --------------------------------------
// "Up" on the stick must mean "towards the net" for both players.
var nearUp = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(0f, 1f), -1, 30);
check("near player pushing up runs towards the net (+z)",
    nearUp.Position.z > court.GetBaselineCentre(-1).z + 0.5f);

var farUp = run(court.GetBaselineCentre(1), new UnityEngine.Vector2(0f, 1f), 1, 30);
check("far player pushing up also runs towards the net (-z)",
    farUp.Position.z < court.GetBaselineCentre(1).z - 0.5f);

var nearRight = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(1f, 0f), -1, 30);
check("near player pushing right moves +x", nearRight.Position.x > 0.5f);

var farRight = run(court.GetBaselineCentre(1), new UnityEngine.Vector2(1f, 0f), 1, 30);
check("far player pushing right moves -x, mirrored", farRight.Position.x < -0.5f);

// --- speed limits -------------------------------------------------------
// Run these without a court: with one, a long sprint reaches the side
// boundary and the velocity is (correctly) cancelled there, which would
// measure the clamp rather than the speed cap.
System.Func<UnityEngine.Vector2, int, ArcadeTennis.Characters.CharacterMotionState> runFree =
    (intent, ticks) =>
    {
        var st = new ArcadeTennis.Characters.CharacterMotionState(UnityEngine.Vector3.zero);
        for (int i = 0; i < ticks; i++)
            ArcadeTennis.Characters.CharacterMotion.Step(ref st, intent, -1, cfg, null, dt);
        return st;
    };

var sprint = runFree(new UnityEngine.Vector2(1f, 0f), 300);
check("top speed settles at the configured maximum",
    UnityEngine.Mathf.Abs(sprint.Velocity.magnitude - cfg.MaxSpeed) < 0.01f);

var diagonal = runFree(new UnityEngine.Vector2(1f, 1f), 300);
check("diagonal input is not faster than straight input",
    diagonal.Velocity.magnitude <= cfg.MaxSpeed + 0.01f);

// Reaching top speed should take about maxSpeed / acceleration seconds.
float expectedRampTicks = cfg.MaxSpeed / cfg.Acceleration / dt;
var partway = runFree(new UnityEngine.Vector2(1f, 0f), UnityEngine.Mathf.CeilToInt(expectedRampTicks));
check("acceleration ramp matches the configured rate",
    UnityEngine.Mathf.Abs(partway.Velocity.magnitude - cfg.MaxSpeed) < 0.05f);

// --- deadzone -----------------------------------------------------------
var deadzoneStart = court.GetBaselineCentre(-1);
var drift = run(deadzoneStart, new UnityEngine.Vector2(0.05f, 0.05f), -1, 60);
check("input inside the deadzone produces no movement",
    UnityEngine.Vector3.Distance(drift.Position, deadzoneStart) < 0.0001f);

// --- braking ------------------------------------------------------------
var moving = runFree(new UnityEngine.Vector2(1f, 0f), 300);
var braking = moving;
int brakeTicks = 0;
while (braking.Velocity.magnitude > 0.001f && brakeTicks < 500)
{
    ArcadeTennis.Characters.CharacterMotion.Step(
        ref braking, UnityEngine.Vector2.zero, -1, cfg, null, dt);
    brakeTicks++;
}
check("releasing the stick brings the character to a stop", braking.Velocity.magnitude <= 0.001f);
check("braking is quicker than accelerating",
    brakeTicks * dt < cfg.MaxSpeed / cfg.Acceleration + 0.001f);

// --- boundaries ---------------------------------------------------------
var wide = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(1f, 0f), -1, 600);
float maxX = court.DoublesHalfWidth + court.RunOffSide;
check("character cannot run off the side of the play area",
    wide.Position.x <= maxX + 0.001f);
check("velocity into the boundary is cancelled, not held",
    UnityEngine.Mathf.Abs(wide.Velocity.x) < 0.001f);

// A player must stay on their own side of the net, however long they push.
var atNet = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(0f, 1f), -1, 600);
check("near player cannot cross the net", atNet.Position.z < 0f);
check("near player can crowd the net", atNet.Position.z > -0.5f);

var farAtNet = run(court.GetBaselineCentre(1), new UnityEngine.Vector2(0f, 1f), 1, 600);
check("far player cannot cross the net", farAtNet.Position.z > 0f);

var cornerRun = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(1f, 1f), -1, 900);
check("running into the corner stays on the near half",
    cornerRun.Position.z < 0f && cornerRun.Position.x <= court.DoublesHalfWidth + court.RunOffSide + 0.001f);

var deep = run(court.GetBaselineCentre(-1), new UnityEngine.Vector2(0f, -1f), -1, 600);
float maxZ = court.HalfLength + court.RunOffBack;
check("character cannot run off the back of the play area",
    UnityEngine.Mathf.Abs(deep.Position.z) <= maxZ + 0.001f);

// --- scene wiring -------------------------------------------------------
var characters = UnityEngine.Object.FindObjectsByType<ArcadeTennis.Characters.TennisCharacter>(
    UnityEngine.FindObjectsSortMode.None);
check("scene holds two characters", characters.Length == 2);

bool sidesOk = false;
bool visualsOk = true;
foreach (var c in characters)
{
    var cso = new UnityEditor.SerializedObject(c);
    if (cso.FindProperty("visual").objectReferenceValue == null) visualsOk = false;
}
foreach (var a in characters)
    foreach (var b in characters)
        if (a != b && a.Side != b.Side) sidesOk = true;

check("the two characters play opposite sides", sidesOk);
check("both characters keep their visuals on a separate child", visualsOk);

var cam = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Presentation.MatchCamera>();
check("match camera exists and sits behind the near baseline",
    cam != null && cam.transform.position.z < -court.HalfLength);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
