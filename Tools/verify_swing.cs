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
// State machine: hold to aim, release to swing
// =========================================================================
var idle = new ArcadeTennis.Characters.SwingState();
bool due;
for (int i = 0; i < 50; i++)
    ArcadeTennis.Characters.SwingSolver.Tick(ref idle, false, cfg, dt, out due);
check("an untouched button leaves the stroke idle",
    idle.Phase == ArcadeTennis.Characters.SwingPhase.Idle);

var aiming = new ArcadeTennis.Characters.SwingState();
for (int i = 0; i < 200; i++)
    ArcadeTennis.Characters.SwingSolver.Tick(ref aiming, true, cfg, dt, out due);
check("holding the button aims and waits, however long it is held",
    aiming.Phase == ArcadeTennis.Characters.SwingPhase.Aiming);

// Holding costs nothing: the marker is up, but no stroke has started.
int strokes = 0;
var run = new ArcadeTennis.Characters.SwingState();
int contactTicks = -1, idleTicks = -1;
var phaseAtContact = ArcadeTennis.Characters.SwingPhase.Idle;
for (int i = 1; i <= 200; i++)
{
    // Held for the first ten ticks, then released.
    ArcadeTennis.Characters.SwingSolver.Tick(ref run, i <= 10, cfg, dt, out due);
    if (due)
    {
        strokes++;
        if (contactTicks < 0) contactTicks = i - 10;
        phaseAtContact = run.Phase;
    }
    if (idleTicks < 0 && contactTicks > 0
        && run.Phase == ArcadeTennis.Characters.SwingPhase.Idle) idleTicks = i - 10;
}
check("letting go swings exactly once", strokes == 1);
check("contact happens after the configured delay, not at once",
    contactTicks > 0
    && (contactTicks - 1) * dt >= cfg.ContactDelay - 1e-6f
    && (contactTicks - 1) * dt < cfg.ContactDelay + dt + 1e-6f);
check("the racket is still mid-swing at the moment of contact",
    phaseAtContact == ArcadeTennis.Characters.SwingPhase.Swinging);
check("the stroke returns to idle on its own",
    run.Phase == ArcadeTennis.Characters.SwingPhase.Idle);
check("swing and recovery together take the configured time",
    idleTicks > 0
    && UnityEngine.Mathf.Abs(idleTicks * dt - (cfg.SwingDuration + cfg.RecoverDuration)) < 3f * dt);

// A player who never lets go of the button drops straight back into aiming
// once the recovery is over, which is what a rally needs.
var reAim = new ArcadeTennis.Characters.SwingState();
for (int i = 1; i <= 300; i++)
    ArcadeTennis.Characters.SwingSolver.Tick(ref reAim, i > 12, cfg, dt, out due);
check("holding again after the stroke returns to aiming",
    reAim.Phase == ArcadeTennis.Characters.SwingPhase.Aiming);

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
// The three zones: one deep, two short
// =========================================================================
var deepZone = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, ArcadeTennis.Court.AimZone.Deep);
var leftZone = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, ArcadeTennis.Court.AimZone.ShortLeft);
var rightZone = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, ArcadeTennis.Court.AimZone.ShortRight);

check("the deep zone sits behind the service line, the short ones in front",
    deepZone.y > court.ServiceLineDistance && leftZone.y < court.ServiceLineDistance
    && rightZone.y < court.ServiceLineDistance);
check("the deep zone is down the middle", UnityEngine.Mathf.Abs(deepZone.x) < 0.001f);
check("the two short zones sit either side of the centre line",
    leftZone.x < -1f && rightZone.x > 1f
    && UnityEngine.Mathf.Abs(leftZone.x + rightZone.x) < 0.001f);

var leftFar = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, 1, ArcadeTennis.Court.AimZone.ShortLeft);
check("left is left on screen for the far player too", leftFar.x > 1f);

bool zonesInside = true;
foreach (var z in new ArcadeTennis.Court.AimZone[] { ArcadeTennis.Court.AimZone.ShortLeft,
    ArcadeTennis.Court.AimZone.Deep, ArcadeTennis.Court.AimZone.ShortRight })
{
    var c = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, z);
    var e = ArcadeTennis.Court.AimZones.GroundZoneExtents(court, z);
    if (UnityEngine.Mathf.Abs(c.x) + e.x > court.SinglesHalfWidth + 0.001f) zonesInside = false;
    if (c.y + e.y > court.HalfLength + 0.001f || c.y - e.y < 0.2f) zonesInside = false;
}
check("every zone lies wholly inside the opponent's half", zonesInside);

check("arrow up asks for the deep zone",
    ArcadeTennis.Court.AimZones.FromInput(new UnityEngine.Vector2(0f, 1f),
        ArcadeTennis.Court.AimZone.ShortLeft) == ArcadeTennis.Court.AimZone.Deep);
check("arrow left asks for the short left zone",
    ArcadeTennis.Court.AimZones.FromInput(new UnityEngine.Vector2(-1f, 0f),
        ArcadeTennis.Court.AimZone.Deep) == ArcadeTennis.Court.AimZone.ShortLeft);
check("arrow right asks for the short right zone",
    ArcadeTennis.Court.AimZones.FromInput(new UnityEngine.Vector2(1f, 0f),
        ArcadeTennis.Court.AimZone.Deep) == ArcadeTennis.Court.AimZone.ShortRight);
check("no key keeps whatever was already chosen",
    ArcadeTennis.Court.AimZones.FromInput(UnityEngine.Vector2.zero,
        ArcadeTennis.Court.AimZone.ShortRight) == ArcadeTennis.Court.AimZone.ShortRight);
check("a diagonal is decided by its stronger axis, not by both at once",
    ArcadeTennis.Court.AimZones.FromInput(new UnityEngine.Vector2(0.9f, 0.3f),
        ArcadeTennis.Court.AimZone.Deep) == ArcadeTennis.Court.AimZone.ShortRight);

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

var deep = aimAt(-1, ArcadeTennis.Court.AimZone.Deep, perfect, noSpread);
var shortLeft = aimAt(-1, ArcadeTennis.Court.AimZone.ShortLeft, perfect, noSpread);
var shortRight = aimAt(-1, ArcadeTennis.Court.AimZone.ShortRight, perfect, noSpread);

check("the deep zone is aimed deep and in", deep.z > court.ServiceLineDistance
    && court.IsInBounds(deep));
check("the short zones are aimed short and in",
    shortLeft.z < court.ServiceLineDistance && shortRight.z < court.ServiceLineDistance
    && court.IsInBounds(shortLeft) && court.IsInBounds(shortRight));
check("the short left zone goes left, the short right one right",
    shortLeft.x < -1f && shortRight.x > 1f);
check("even the cleanest stroke stays inside the baseline", deep.z < court.HalfLength);

var deepSloppy = aimAt(-1, ArcadeTennis.Court.AimZone.Deep, sloppy, noSpread);
check("poor contact falls short of the zone it was aimed at", deepSloppy.z < deep.z - 1f);
check("but it is still sent to the same side of the court",
    UnityEngine.Mathf.Abs(deepSloppy.x - deep.x) < 0.001f);

// The aim has to survive the spread: sideways slip is held far below length
// error, because a shot that ignores the chosen zone reads as a broken game.
float worstDeepX = 0f, nearestLeftX = 99f, worstDepthSlip = 0f;
bool onPremises = true;
float maxX = court.SinglesHalfWidth + cfg.OutMargin;
float maxZ = court.HalfLength + cfg.OutMargin;

for (float q = 0f; q <= 1.0001f; q += 0.1f)
{
    var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = q };
    var nominal = aimAt(-1, ArcadeTennis.Court.AimZone.Deep, judged, noSpread);

    for (int i = 0; i < 64; i++)
    {
        float a = i / 64f * UnityEngine.Mathf.PI * 2f;
        var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));

        var centred = aimAt(-1, ArcadeTennis.Court.AimZone.Deep, judged, spread);
        var leftward = aimAt(-1, ArcadeTennis.Court.AimZone.ShortLeft, judged, spread);

        worstDeepX = UnityEngine.Mathf.Max(worstDeepX, UnityEngine.Mathf.Abs(centred.x));
        nearestLeftX = UnityEngine.Mathf.Min(nearestLeftX, UnityEngine.Mathf.Abs(leftward.x));
        worstDepthSlip = UnityEngine.Mathf.Max(worstDepthSlip,
            UnityEngine.Mathf.Abs(centred.z - nominal.z));

        if (UnityEngine.Mathf.Abs(centred.x) > maxX + 1e-4f
            || UnityEngine.Mathf.Abs(centred.z) > maxZ + 1e-4f) onPremises = false;
    }
}
check("the deep zone goes essentially straight at any contact quality", worstDeepX < 1.0f);
check("a short zone never lands where the deep one could have", nearestLeftX > worstDeepX);
check("length is punished harder than direction", worstDepthSlip > worstDeepX * 2f);
check("no amount of spread aims a ball off the premises", onPremises);

// =========================================================================
// Arc and flight, through the real ball simulation
// =========================================================================
var high = new UnityEngine.Vector3(0f, 1f, -11f);
System.Func<ArcadeTennis.Court.AimZone, ArcadeTennis.Characters.SwingContact, float> apexOf =
    (zone, contact) => ArcadeTennis.Characters.SwingSolver.ResolveApex(zone, contact, high, cfg);

check("a mishit short ball is lofted far more than a mishit deep one",
    apexOf(ArcadeTennis.Court.AimZone.ShortLeft, sloppy)
        > apexOf(ArcadeTennis.Court.AimZone.Deep, sloppy) + 1f);
check("clean contact flattens the short ball into a crisp one",
    apexOf(ArcadeTennis.Court.AimZone.ShortLeft, perfect)
        < apexOf(ArcadeTennis.Court.AimZone.ShortLeft, sloppy));
check("clean contact lifts the deep drive instead",
    apexOf(ArcadeTennis.Court.AimZone.Deep, perfect)
        > apexOf(ArcadeTennis.Court.AimZone.Deep, sloppy));

var low = new UnityEngine.Vector3(0f, 0.08f, -11f);
check("a clean stroke off the ground is still given an arc that can clear the net",
    ArcadeTennis.Characters.SwingSolver.ResolveApex(
        ArcadeTennis.Court.AimZone.Deep, perfect, low, cfg) + low.y
        >= cfg.MinPeakHeight - 1e-4f);

System.Func<UnityEngine.Vector3, float, ArcadeTennis.Court.AimZone,
            ArcadeTennis.BallPhysics.BallPrediction> playOut =
    (from, quality, zone) =>
    {
        var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
        var target = aimAt(-1, zone, judged, noSpread);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(from, target,
            ArcadeTennis.Characters.SwingSolver.ResolveApex(zone, judged, from, cfg),
            ballCfg, court, dt);
        return ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);
    };

var contactPoint = new UnityEngine.Vector3(0f, charCfg.HitHeight, -11f);

foreach (var z in new ArcadeTennis.Court.AimZone[] { ArcadeTennis.Court.AimZone.ShortLeft,
    ArcadeTennis.Court.AimZone.Deep, ArcadeTennis.Court.AimZone.ShortRight })
{
    var shot = playOut(contactPoint, 1f, z);
    check("a clean stroke into the " + z + " zone clears the net and lands in",
        shot.IsBounce && court.IsInBounds(shot.Position) && shot.Position.z > 0f);
    check("and it lands in that zone", shot.HasResult
        && UnityEngine.Vector3.Distance(
            new UnityEngine.Vector3(shot.Position.x, 0f, shot.Position.z),
            aimAt(-1, z, perfect, noSpread)) < 0.35f);

    // Every zone has to stay reachable however badly the ball is met -- that is
    // the whole point of taking the charge out.
    int playable = 0;
    for (float q = 0f; q <= 1.0001f; q += 0.05f)
    {
        var s2 = playOut(contactPoint, q, z);
        if (s2.IsBounce && court.IsInBounds(s2.Position) && s2.Position.z > 0f) playable++;
    }
    check("the " + z + " zone stays playable across the quality range", playable >= 19);
}

// The marker draws the ring from the zone's centre and extents; the ball is
// sent by the solver. If those two ever disagree, the marker promises ground
// the ball cannot reach -- which is exactly what went wrong on the serve, and
// only showed up by measuring rather than by playing.
bool markerHonest = true;
foreach (var z in new ArcadeTennis.Court.AimZone[] { ArcadeTennis.Court.AimZone.ShortLeft,
    ArcadeTennis.Court.AimZone.Deep, ArcadeTennis.Court.AimZone.ShortRight })
{
    var ringCentre = ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, z);
    var ringExtents = ArcadeTennis.Court.AimZones.GroundZoneExtents(court, z);

    for (float q = 0f; q <= 1.0001f; q += 0.1f)
    {
        var shot = playOut(contactPoint, q, z);
        if (!shot.IsBounce) { markerHonest = false; continue; }

        float nx = (shot.Position.x - ringCentre.x) / ringExtents.x;
        float nz = (UnityEngine.Mathf.Abs(shot.Position.z) - ringCentre.y) / ringExtents.y;
        if (nx * nx + nz * nz > 1f) markerHonest = false;
    }
}
check("the ball always lands inside the ring the marker drew", markerHonest);

var deepWeak = playOut(contactPoint, 0f, ArcadeTennis.Court.AimZone.Deep);
check("a badly met deep ball still lands, but short of where it was aimed",
    deepWeak.IsBounce && deepWeak.Position.z < deep.z - 1f);

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
    foreach (var field in new string[] { "character", "swing", "serve", "court", "ring", "line" })
    {
        var prop = mso.FindProperty(field);
        if (prop == null || prop.objectReferenceValue == null) markerWired = false;
    }
}
check("the target zone marker exists and is fully wired", markerWired);

var input = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Characters.PlayerInputController>();
bool actionsBound = false;
if (input != null)
{
    var asset = new UnityEditor.SerializedObject(input).FindProperty("controls").objectReferenceValue
        as UnityEngine.InputSystem.InputActionAsset;
    var map = asset != null ? asset.FindActionMap("Match", false) : null;
    var moveAction = map != null ? map.FindAction("Move", false) : null;

    // Move does double duty now -- steering when the button is free, choosing a
    // zone while it is held -- so the arrow keys have to be on it again.
    bool arrowsOnMove = false;
    if (moveAction != null)
        foreach (var b in moveAction.bindings)
            if (b.path != null && b.path.Contains("upArrow")) arrowsOnMove = true;

    actionsBound = map != null && moveAction != null
        && map.FindAction("Swing", false) != null && arrowsOnMove;
}
check("the arrow keys steer and, with the button down, choose the zone", actionsBound);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
