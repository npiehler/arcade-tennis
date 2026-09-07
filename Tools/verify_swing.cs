// Verification for the rally stroke. Everything it is built from is a pure
// function, so all of this runs in edit mode with no timing races and no play
// mode.
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var charCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.CharacterConfig>(
    "Assets/_Game/Settings/CharacterConfig.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var ballCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = 0.02f;
float reach = charCfg.ReachRadius;
var sb = new System.Text.StringBuilder();
int failed = 0;

System.Action<string, bool> check = (label, ok) => {
    if (!ok) failed++;
    sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
};

// =========================================================================
// State machine: one press, one stroke
// =========================================================================
var idle = new ArcadeTennis.Characters.SwingState();
bool due;
for (int i = 0; i < 50; i++)
    ArcadeTennis.Characters.SwingSolver.Tick(ref idle, false, cfg, dt, out due);
check("an untouched button leaves the stroke idle",
    idle.Phase == ArcadeTennis.Characters.SwingPhase.Idle);

// Runs a button pattern and reports what happened.
System.Func<bool[], int> strokesIn = (pattern) => {
    var st = new ArcadeTennis.Characters.SwingState();
    int count = 0;
    bool d;
    foreach (bool held in pattern)
    {
        ArcadeTennis.Characters.SwingSolver.Tick(ref st, held, cfg, dt, out d);
        if (d) count++;
    }
    return count;
};

var held200 = new bool[200];
for (int i = 0; i < held200.Length; i++) held200[i] = true;
check("holding the button down fires exactly one stroke, not a stream",
    strokesIn(held200) == 1);

// Press, release, press again once the stroke is over.
int strokeTicks = UnityEngine.Mathf.CeilToInt((cfg.SwingDuration + cfg.RecoverDuration) / dt) + 4;
var twoTaps = new bool[strokeTicks * 2];
twoTaps[0] = true;
twoTaps[strokeTicks] = true;
check("two taps either side of the recovery fire two strokes", strokesIn(twoTaps) == 2);

// A tap during the recovery must be swallowed.
var tapDuringRecovery = new bool[strokeTicks * 2];
tapDuringRecovery[0] = true;
tapDuringRecovery[UnityEngine.Mathf.CeilToInt(cfg.SwingDuration / dt) + 1] = true;
check("a tap during the recovery is ignored", strokesIn(tapDuringRecovery) == 1);

var run = new ArcadeTennis.Characters.SwingState();
int contactTicks = -1, idleTicks = -1, contacts = 0;
var phaseAtContact = ArcadeTennis.Characters.SwingPhase.Idle;
for (int i = 1; i <= 200; i++)
{
    ArcadeTennis.Characters.SwingSolver.Tick(ref run, i == 1, cfg, dt, out due);
    if (due)
    {
        contacts++;
        if (contactTicks < 0) contactTicks = i;
        phaseAtContact = run.Phase;
    }
    if (idleTicks < 0 && contactTicks > 0
        && run.Phase == ArcadeTennis.Characters.SwingPhase.Idle) idleTicks = i;
}
check("a single press swings exactly once", contacts == 1);
// The press tick only enters the swing; the swing clock starts on the tick
// after it, so one tick comes off the measurement.
float swingElapsed = (contactTicks - 1) * dt;
check("contact happens after the configured delay, not at once",
    contactTicks > 0
    && swingElapsed >= cfg.ContactDelay - 1e-6f
    && swingElapsed < cfg.ContactDelay + dt + 1e-6f);
check("the racket is still mid-swing at the moment of contact",
    phaseAtContact == ArcadeTennis.Characters.SwingPhase.Swinging);
check("the stroke returns to idle on its own",
    run.Phase == ArcadeTennis.Characters.SwingPhase.Idle);
check("swing and recovery together take the configured time",
    idleTicks > 0
    && UnityEngine.Mathf.Abs(idleTicks * dt - (cfg.SwingDuration + cfg.RecoverDuration)) < 3f * dt);

// =========================================================================
// Contact judgement
// =========================================================================
var hitCentre = new UnityEngine.Vector3(0f, charCfg.HitHeight, -11f);
var incoming = new UnityEngine.Vector3(0f, 0f, -20f);
var tuning = cfg.Contact(reach);

System.Func<UnityEngine.Vector3, ArcadeTennis.Characters.SwingContact> meet = (offset) =>
    ArcadeTennis.Characters.SwingSolver.Evaluate(
        hitCentre + offset, incoming, hitCentre, UnityEngine.Vector3.zero, tuning);

var dead = meet(new UnityEngine.Vector3(0f, 0f, 1.0f));
check("a ball on line and on time grades Perfect",
    dead.Made && dead.Grade == ArcadeTennis.Characters.SwingGrade.Perfect);
check("that contact reports no miss distance", dead.MissDistance < 0.001f);
check("a ball out of reach is a miss", !meet(new UnityEngine.Vector3(3f, 0f, 1.0f)).Made);

var early = meet(new UnityEngine.Vector3(0f, 0f, 2.4f));
check("swinging before the ball arrives reads Early",
    early.Made && early.Grade == ArcadeTennis.Characters.SwingGrade.Early
    && early.TimingOffset < 0f);

var late = meet(new UnityEngine.Vector3(0f, 0f, -2.4f));
check("swinging after the ball has gone by reads Late",
    late.Made && late.Grade == ArcadeTennis.Characters.SwingGrade.Late
    && late.TimingOffset > 0f);

check("timing beyond the window misses even on a perfect line",
    !meet(new UnityEngine.Vector3(0f, 0f, 5f)).Made);

float previousQuality = 2f;
bool monotonic = true;
for (float d = 0f; d <= 1.2f; d += 0.15f)
{
    float q = meet(new UnityEngine.Vector3(d, 0f, 1.0f)).Quality;
    if (q > previousQuality + 1e-5f) monotonic = false;
    previousQuality = q;
}
check("quality falls as the ball passes further from the sweet spot", monotonic);

check("a motionless ball inside the reach can still be struck",
    ArcadeTennis.Characters.SwingSolver.Evaluate(
        hitCentre + new UnityEngine.Vector3(0.3f, 0f, 0f), UnityEngine.Vector3.zero,
        hitCentre, UnityEngine.Vector3.zero, tuning).Made);

var chasing = ArcadeTennis.Characters.SwingSolver.Evaluate(
    hitCentre + new UnityEngine.Vector3(0f, 0f, 3.6f), incoming,
    hitCentre, new UnityEngine.Vector3(0f, 0f, 6f), tuning);
check("running towards the ball buys timing a standing player does not have",
    chasing.Made && !meet(new UnityEngine.Vector3(0f, 0f, 3.6f)).Made);

// =========================================================================
// The three zones
// =========================================================================
float laneHalf = ArcadeTennis.Court.AimZones.LaneHalfWidth(court);
float left = ArcadeTennis.Court.AimZones.LaneCentre(court, -1, ArcadeTennis.Court.AimZone.Left);
float centre = ArcadeTennis.Court.AimZones.LaneCentre(court, -1, ArcadeTennis.Court.AimZone.Centre);
float right = ArcadeTennis.Court.AimZones.LaneCentre(court, -1, ArcadeTennis.Court.AimZone.Right);

check("three lanes span the court, evenly spaced", left < centre && centre < right
    && UnityEngine.Mathf.Abs((centre - left) - (right - centre)) < 0.001f);
check("the middle lane is the middle of the court", UnityEngine.Mathf.Abs(centre) < 0.001f);
check("every lane lies inside the singles court",
    UnityEngine.Mathf.Abs(left) + laneHalf <= court.SinglesHalfWidth + 0.001f);
check("the lanes are mirrored for the far player",
    ArcadeTennis.Court.AimZones.LaneCentre(court, 1, ArcadeTennis.Court.AimZone.Right) < 0f);

check("a stick pushed left picks the left lane",
    ArcadeTennis.Court.AimZones.FromInput(-1f, ArcadeTennis.Court.AimZone.Centre)
        == ArcadeTennis.Court.AimZone.Left);
check("a stick pushed right picks the right lane",
    ArcadeTennis.Court.AimZones.FromInput(1f, ArcadeTennis.Court.AimZone.Centre)
        == ArcadeTennis.Court.AimZone.Right);
check("a resting stick keeps whatever it was given as the resting choice",
    ArcadeTennis.Court.AimZones.FromInput(0.2f, ArcadeTennis.Court.AimZone.Centre)
        == ArcadeTennis.Court.AimZone.Centre);
check("stepping past the outside lane stays there",
    ArcadeTennis.Court.AimZones.Step(ArcadeTennis.Court.AimZone.Right, 1)
        == ArcadeTennis.Court.AimZone.Right);

// =========================================================================
// Where the ball is sent
// =========================================================================
var perfect = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 1f };
var sloppy = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 0.15f };
var noSpread = UnityEngine.Vector2.zero;

System.Func<int, ArcadeTennis.Court.AimZone, ArcadeTennis.Characters.SwingContact,
            UnityEngine.Vector2, UnityEngine.Vector3> aimAt =
    (side, zone, contact, spread) =>
        ArcadeTennis.Characters.SwingSolver.ResolveTarget(side, zone, contact, cfg, court, spread);

var deep = aimAt(-1, ArcadeTennis.Court.AimZone.Centre, perfect, noSpread);
var shallow = aimAt(-1, ArcadeTennis.Court.AimZone.Centre, sloppy, noSpread);
check("clean contact is sent deeper than poor contact", deep.z > shallow.z + 3f);
check("a clean stroke lands inside the court", court.IsInBounds(deep) && deep.z > 0f);
check("with the charge gone, even the cleanest stroke stays inside the baseline",
    deep.z < court.HalfLength);

var leftShot = aimAt(-1, ArcadeTennis.Court.AimZone.Left, perfect, noSpread);
var rightShot = aimAt(-1, ArcadeTennis.Court.AimZone.Right, perfect, noSpread);
check("the left lane sends the ball left", leftShot.x < -2f);
check("the right lane sends the ball right", rightShot.x > 2f);
check("the middle lane sends it down the middle", UnityEngine.Mathf.Abs(deep.x) < 0.2f);
check("both outside lanes still land in",
    court.IsInBounds(leftShot) && court.IsInBounds(rightShot));

var rightFar = aimAt(1, ArcadeTennis.Court.AimZone.Right, perfect, noSpread);
check("the far player's right is right on screen too", rightFar.x < -2f && rightFar.z < 0f);

// The aim has to survive the spread: sideways slip is held far below length
// error, because a shot that ignores the chosen zone reads as a broken game.
float worstCentre = 0f, nearestLeft = 99f, worstDepthSlip = 0f;
bool onPremises = true;
float maxX = court.SinglesHalfWidth + cfg.OutMargin;
float maxZ = court.HalfLength + cfg.OutMargin;

for (float q = 0f; q <= 1.0001f; q += 0.1f)
{
    var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = q };
    var nominal = aimAt(-1, ArcadeTennis.Court.AimZone.Centre, judged, noSpread);

    for (int i = 0; i < 64; i++)
    {
        float a = i / 64f * UnityEngine.Mathf.PI * 2f;
        var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));

        var centred = aimAt(-1, ArcadeTennis.Court.AimZone.Centre, judged, spread);
        var leftward = aimAt(-1, ArcadeTennis.Court.AimZone.Left, judged, spread);

        worstCentre = UnityEngine.Mathf.Max(worstCentre, UnityEngine.Mathf.Abs(centred.x));
        nearestLeft = UnityEngine.Mathf.Min(nearestLeft, UnityEngine.Mathf.Abs(leftward.x));
        worstDepthSlip = UnityEngine.Mathf.Max(worstDepthSlip,
            UnityEngine.Mathf.Abs(centred.z - nominal.z));

        if (UnityEngine.Mathf.Abs(centred.x) > maxX + 1e-4f
            || UnityEngine.Mathf.Abs(centred.z) > maxZ + 1e-4f) onPremises = false;
    }
}
check("the middle lane goes essentially straight at any contact quality", worstCentre < 1.0f);
check("an outside lane never lands where the middle one could have", nearestLeft > worstCentre);
check("length is punished harder than direction", worstDepthSlip > worstCentre * 2f);
check("no amount of spread aims a ball off the premises", onPremises);

// =========================================================================
// Arc and flight, through the real ball simulation
// =========================================================================
var high = new UnityEngine.Vector3(0f, 1f, -11f);
check("clean contact lifts the arc",
    ArcadeTennis.Characters.SwingSolver.ResolveApex(perfect, high, cfg)
        > ArcadeTennis.Characters.SwingSolver.ResolveApex(sloppy, high, cfg));

var low = new UnityEngine.Vector3(0f, 0.08f, -11f);
check("a clean stroke off the ground is still given an arc that can clear the net",
    ArcadeTennis.Characters.SwingSolver.ResolveApex(perfect, low, cfg) + low.y
        >= cfg.MinPeakHeight - 1e-4f);
check("a mistimed scoop off the ground gets no such guarantee",
    ArcadeTennis.Characters.SwingSolver.ResolveApex(
        new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 0f }, low, cfg) + low.y
        < cfg.MinPeakHeight - 0.1f);

System.Func<UnityEngine.Vector3, float, ArcadeTennis.Court.AimZone,
            ArcadeTennis.BallPhysics.BallPrediction> playOut =
    (from, quality, zone) =>
    {
        var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
        var target = aimAt(-1, zone, judged, noSpread);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
            from, target, ArcadeTennis.Characters.SwingSolver.ResolveApex(judged, from, cfg),
            ballCfg, court, dt);
        return ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);
    };

var contactPoint = new UnityEngine.Vector3(0f, charCfg.HitHeight, -11f);
var drive = playOut(contactPoint, 1f, ArcadeTennis.Court.AimZone.Centre);
check("a clean drive clears the net and lands in",
    drive.IsBounce && court.IsInBounds(drive.Position) && drive.Position.z > 0f);
check("it lands where it was aimed", drive.HasResult
    && UnityEngine.Vector3.Distance(new UnityEngine.Vector3(drive.Position.x, 0f, drive.Position.z),
        aimAt(-1, ArcadeTennis.Court.AimZone.Centre, perfect, noSpread)) < 0.25f);

var wideDrive = playOut(contactPoint, 1f, ArcadeTennis.Court.AimZone.Left);
check("a clean drive into the left lane also lands in",
    wideDrive.IsBounce && court.IsInBounds(wideDrive.Position) && wideDrive.Position.x < -2f);

// The band the player will feel: badly met balls drop, well met balls land deep.
var bands = new System.Text.StringBuilder();
int net = 0, inCourt = 0, previous = 0;
bool ordered = true;
for (float q = 0f; q <= 1.0001f; q += 0.05f)
{
    var shot = playOut(contactPoint, q, ArcadeTennis.Court.AimZone.Centre);
    int band = shot.IsNetHit ? 0
        : (shot.IsBounce && court.IsInBounds(shot.Position) && shot.Position.z > 0f ? 1 : 2);
    if (band < previous) ordered = false;
    previous = band;
    if (band == 0) net++; else if (band == 1) inCourt++;
    bands.Append(band == 0 ? "n" : band == 1 ? "." : "o");
}
check("the worst contact that connects still drops into the net [" + bands + "]", net >= 1);
check("nearly the whole quality range is playable", inCourt >= 17);
check("net first, then in: the outcomes do not interleave", ordered);

// Deeper court positions ask more of the contact, which is what makes standing
// in a sensible place worth something.
var deepStand = new UnityEngine.Vector3(0f, charCfg.HitHeight, -13.5f);
int netFromDeep = 0;
for (float q = 0f; q <= 1.0001f; q += 0.05f)
    if (playOut(deepStand, q, ArcadeTennis.Court.AimZone.Centre).IsNetHit) netFromDeep++;
check("hitting from further back is harder, not the same", netFromDeep >= net);

// =========================================================================
// Scene wiring
// =========================================================================
var swingers = UnityEngine.Object.FindObjectsByType<ArcadeTennis.Characters.SwingController>(
    UnityEngine.FindObjectsSortMode.None);
check("both characters can swing", swingers.Length == 2);

bool wired = swingers.Length == 2;
foreach (var s in swingers)
{
    var so = new UnityEditor.SerializedObject(s);
    if (so.FindProperty("config").objectReferenceValue == null) wired = false;
    if (so.FindProperty("ball").objectReferenceValue == null) wired = false;
}
check("every swing controller has its tuning and the ball", wired);

var playerSwing = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Characters.SwingController>();
bool standsDown = false;
if (playerSwing != null)
{
    playerSwing.Suspended = true;
    standsDown = playerSwing.Phase == ArcadeTennis.Characters.SwingPhase.Idle;
    playerSwing.Suspended = false;
}
check("the rally stroke can be held back for the serve", standsDown);

var marker = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Presentation.AimZoneIndicator>();
bool markerWired = marker != null;
if (markerWired)
{
    var mso = new UnityEditor.SerializedObject(marker);
    foreach (var field in new string[] { "character", "swing", "serve", "court",
                                         "swingConfig", "serveConfig", "ring", "line" })
        if (mso.FindProperty(field).objectReferenceValue == null) markerWired = false;
}
check("the target zone marker exists and is fully wired", markerWired);

var input = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Characters.PlayerInputController>();
bool aimBound = false;
if (input != null)
{
    var asset = new UnityEditor.SerializedObject(input).FindProperty("controls").objectReferenceValue
        as UnityEngine.InputSystem.InputActionAsset;
    var map = asset != null ? asset.FindActionMap("Match", false) : null;
    aimBound = map != null && map.FindAction("Aim", false) != null
                           && map.FindAction("Move", false) != null
                           && map.FindAction("Swing", false) != null;
}
check("aiming has its own action, separate from moving", aimBound);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
