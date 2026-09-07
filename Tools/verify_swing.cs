// Milestone 4 verification. The stroke is built out of pure functions -- the
// state machine, the contact judgement and the target -- so all of this runs in
// edit mode with no timing races and no play mode.
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
// State machine
// =========================================================================
var idle = new ArcadeTennis.Characters.SwingState();
bool due;
for (int i = 0; i < 50; i++)
    ArcadeTennis.Characters.SwingSolver.Tick(ref idle, false, cfg, dt, out due);
check("an untouched button leaves the stroke idle",
    idle.Phase == ArcadeTennis.Characters.SwingPhase.Idle && idle.Charge == 0f);

// Holds for a number of ticks, then reports where the charge got to.
System.Func<int, ArcadeTennis.Characters.SwingState> hold = (ticks) => {
    var st = new ArcadeTennis.Characters.SwingState();
    bool d;
    for (int i = 0; i < ticks; i++)
        ArcadeTennis.Characters.SwingSolver.Tick(ref st, true, cfg, dt, out d);
    return st;
};

var pressed = hold(1);
check("pressing the button starts charging",
    pressed.Phase == ArcadeTennis.Characters.SwingPhase.Charging);

// One extra tick because the press itself only enters the charging phase.
int fullTicks = 1 + UnityEngine.Mathf.RoundToInt(cfg.ChargeTime / dt);
var charged = hold(fullTicks);
check("charge reaches full after the configured charge time", charged.Charge > 0.999f);

var halfway = hold(1 + UnityEngine.Mathf.RoundToInt(cfg.ChargeTime / dt / 2f));
check("charge builds evenly over that time",
    UnityEngine.Mathf.Abs(halfway.Charge - 0.5f) < 0.02f);

var overCharged = hold(fullTicks + 60);
check("charge does not grow past full", overCharged.Charge <= 1f + 1e-5f);

var tapped = hold(1);
check("a bare tap carries next to no power", tapped.Power(cfg) < 0.05f);
check("a full hold carries full power", charged.Power(cfg) > 0.999f);

// Runs a whole stroke: hold, release, then idle input until it settles.
var run = hold(fullTicks);
int contactCount = 0;
int ticksToContact = -1;
int ticksToIdle = -1;
var phaseAfterContact = ArcadeTennis.Characters.SwingPhase.Idle;
for (int i = 1; i <= 200; i++)
{
    ArcadeTennis.Characters.SwingSolver.Tick(ref run, false, cfg, dt, out due);
    if (due)
    {
        contactCount++;
        if (ticksToContact < 0) ticksToContact = i;
        phaseAfterContact = run.Phase;
    }
    if (ticksToIdle < 0 && ticksToContact > 0
        && run.Phase == ArcadeTennis.Characters.SwingPhase.Idle) ticksToIdle = i;
}

check("releasing the button swings exactly once", contactCount == 1);
// The release tick only enters the swing; the swing clock starts on the
// tick after, so one tick comes off the measurement.
float swingElapsed = (ticksToContact - 1) * dt;
check("contact happens after the configured delay, not at once",
    ticksToContact > 0
    && swingElapsed >= cfg.ContactDelay - 1e-6f
    && swingElapsed < cfg.ContactDelay + dt + 1e-6f);
check("the racket is still mid-swing at the moment of contact",
    phaseAfterContact == ArcadeTennis.Characters.SwingPhase.Swinging);
check("the stroke returns to idle on its own",
    run.Phase == ArcadeTennis.Characters.SwingPhase.Idle && run.Charge == 0f);
check("swing and recovery together take the configured time",
    ticksToIdle > 0
    && UnityEngine.Mathf.Abs(ticksToIdle * dt - (cfg.SwingDuration + cfg.RecoverDuration)) < 3f * dt);

// A player who never lets go never swings: holding at full charge is a wait,
// not an auto-release.
var stillHolding = hold(fullTicks + 200);
check("holding at full charge waits instead of firing",
    stillHolding.Phase == ArcadeTennis.Characters.SwingPhase.Charging);

// =========================================================================
// Contact judgement
// =========================================================================
var hitCentre = new UnityEngine.Vector3(0f, charCfg.HitHeight, -11f);
var incoming = new UnityEngine.Vector3(0f, 0f, -20f);   // ball running at the near player

System.Func<UnityEngine.Vector3, ArcadeTennis.Characters.SwingContact> meet = (offset) =>
    ArcadeTennis.Characters.SwingSolver.Evaluate(
        hitCentre + offset, incoming, hitCentre, UnityEngine.Vector3.zero, reach, cfg);

var dead = meet(new UnityEngine.Vector3(0f, 0f, 1.0f));
check("a ball on line and on time grades Perfect",
    dead.Made && dead.Grade == ArcadeTennis.Characters.SwingGrade.Perfect);
check("that contact reports no miss distance", dead.MissDistance < 0.001f);

var wide = meet(new UnityEngine.Vector3(3f, 0f, 1.0f));
check("a ball out of reach is a miss",
    !wide.Made && wide.Grade == ArcadeTennis.Characters.SwingGrade.Miss);

var early = meet(new UnityEngine.Vector3(0f, 0f, 2.4f));
check("swinging before the ball arrives reads Early",
    early.Made && early.Grade == ArcadeTennis.Characters.SwingGrade.Early);
check("an early swing has a negative timing offset", early.TimingOffset < 0f);

var late = meet(new UnityEngine.Vector3(0f, 0f, -2.4f));
check("swinging after the ball has gone by reads Late",
    late.Made && late.Grade == ArcadeTennis.Characters.SwingGrade.Late);
check("a late swing has a positive timing offset", late.TimingOffset > 0f);

var tooEarly = meet(new UnityEngine.Vector3(0f, 0f, 5f));
check("timing beyond the window misses even on a perfect line", !tooEarly.Made);

// Quality has to fall off with distance, not step.
float previousQuality = 2f;
bool monotonic = true;
for (float d = 0f; d <= 1.2f; d += 0.15f)
{
    var q = meet(new UnityEngine.Vector3(d, 0f, 1.0f)).Quality;
    if (q > previousQuality + 1e-5f) monotonic = false;
    previousQuality = q;
}
check("quality falls as the ball passes further from the sweet spot", monotonic);

var still = ArcadeTennis.Characters.SwingSolver.Evaluate(
    hitCentre + new UnityEngine.Vector3(0.3f, 0f, 0f), UnityEngine.Vector3.zero,
    hitCentre, UnityEngine.Vector3.zero, reach, cfg);
check("a motionless ball inside the reach can still be struck", still.Made);

// The racket moving with the ball is what a running player does; judging
// against the relative path is the whole point of the closed form.
var chasing = ArcadeTennis.Characters.SwingSolver.Evaluate(
    hitCentre + new UnityEngine.Vector3(0f, 0f, 3.6f), incoming,
    hitCentre, new UnityEngine.Vector3(0f, 0f, 6f), reach, cfg);
var planted = meet(new UnityEngine.Vector3(0f, 0f, 3.6f));
check("running towards the ball buys timing a standing player does not have",
    chasing.Made && !planted.Made);

// =========================================================================
// Target and arc
// =========================================================================
var perfect = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 1f };
var sloppy = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 0.15f };
var noSpread = UnityEngine.Vector2.zero;

System.Func<int, float, UnityEngine.Vector2, ArcadeTennis.Characters.SwingContact,
            UnityEngine.Vector2, UnityEngine.Vector3> aimAt =
    (side, power, aim, contact, spread) =>
        ArcadeTennis.Characters.SwingSolver.ResolveTarget(side, power, aim, contact, cfg, court, spread);

var deepShot = aimAt(-1, 1f, UnityEngine.Vector2.zero, perfect, noSpread);
var shortShot = aimAt(-1, 0f, UnityEngine.Vector2.zero, perfect, noSpread);
var midShot = aimAt(-1, 0.55f, UnityEngine.Vector2.zero, perfect, noSpread);
check("a full charge aims deeper than a tap", deepShot.z > shortShot.z + 3f);
check("a full charge is aimed past the baseline, not safely inside it",
    deepShot.z > court.HalfLength && !court.IsInBounds(deepShot));
check("a tap is aimed at the foot of the net", shortShot.z < 1.0f);
check("a middling charge is aimed inside the court", court.IsInBounds(midShot));

var weakShot = aimAt(-1, 1f, UnityEngine.Vector2.zero, sloppy, noSpread);
check("poor contact lands shorter than clean contact at the same charge",
    weakShot.z < deepShot.z - 1f);

var rightNear = aimAt(-1, 0.55f, new UnityEngine.Vector2(1f, 0f), perfect, noSpread);
var rightFar = aimAt(1, 0.55f, new UnityEngine.Vector2(1f, 0f), perfect, noSpread);
check("aiming right sends the near player's ball to +x", rightNear.x > 2f);
check("aiming right is mirrored for the far player", rightFar.x < -2f);
check("the near player hits into the far half", rightNear.z > 0f);
check("the far player hits into the near half", rightFar.z < 0f);
check("a wide clean shot still lands in", court.IsInBounds(rightNear));

// --- aim deadzone ---------------------------------------------------
// The stroke borrows the movement deadzone rather than keeping its own, so
// that "standing still" means one thing and not two.
float deadzone = charCfg.InputDeadzone;

var idleStick = new UnityEngine.Vector2(deadzone * 0.6f, deadzone * 0.4f);
check("aim inside the movement deadzone is discarded",
    ArcadeTennis.Characters.SwingSolver.ApplyAimDeadzone(idleStick, deadzone)
        == UnityEngine.Vector2.zero);

var realPush = new UnityEngine.Vector2(0.8f, 0f);
check("aim outside the deadzone is passed through untouched",
    ArcadeTennis.Characters.SwingSolver.ApplyAimDeadzone(realPush, deadzone) == realPush);

// The effect that matters: a resting stick must not pull the ball off centre.
var driftTarget = aimAt(-1, 0.55f,
    ArcadeTennis.Characters.SwingSolver.ApplyAimDeadzone(idleStick, deadzone),
    perfect, noSpread);
check("a resting stick aims straight down the middle",
    UnityEngine.Mathf.Abs(driftTarget.x) < 0.001f);

// Without the deadzone that same stick would have moved the target by an
// amount a player can see, which is why this check exists at all.
var undamped = aimAt(-1, 0.55f, idleStick, perfect, noSpread);
check("the deadzone is doing real work, not rounding noise",
    UnityEngine.Mathf.Abs(undamped.x) > 0.2f);

// --- how far a full sideways aim reaches ----------------------------
// The keyboard only ever gives a full press, so the widest aim has to stay
// a shot a player can rely on when they strike it cleanly.
float widestPerfect = 0f;
for (int i = 0; i < 64; i++)
{
    float a = i / 64f * UnityEngine.Mathf.PI * 2f;
    var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));
    var t = aimAt(-1, 0.55f, new UnityEngine.Vector2(1f, 0f), perfect, spread);
    widestPerfect = UnityEngine.Mathf.Max(widestPerfect, UnityEngine.Mathf.Abs(t.x));
}
check("a flawless full-width aim stays inside the sideline",
    widestPerfect <= court.SinglesHalfWidth + court.LineWidth * 0.5f);
check("but it does reach for the line, not the middle of the court",
    widestPerfect > court.SinglesHalfWidth * 0.9f);

var edgeSpread = new UnityEngine.Vector2(1f, 0f);
float cleanScatter = UnityEngine.Mathf.Abs(
    aimAt(-1, 1f, UnityEngine.Vector2.zero, perfect, edgeSpread).x);
float sloppyScatter = UnityEngine.Mathf.Abs(
    aimAt(-1, 1f, UnityEngine.Vector2.zero, sloppy, edgeSpread).x);
check("clean contact scatters less than poor contact", cleanScatter < sloppyScatter - 1f);
check("even flawless contact scatters a little", cleanScatter > 0.01f);

float maxX = court.SinglesHalfWidth + cfg.OutMargin;
float maxZ = court.HalfLength + cfg.OutMargin;
bool clamped = true;
for (int i = 0; i < 64; i++)
{
    float a = i / 64f * UnityEngine.Mathf.PI * 2f;
    var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));
    var t = aimAt(-1, 1f, new UnityEngine.Vector2(1f, 0f), sloppy, spread);
    if (UnityEngine.Mathf.Abs(t.x) > maxX + 1e-4f || UnityEngine.Mathf.Abs(t.z) > maxZ + 1e-4f)
        clamped = false;
}
check("no amount of spread aims a ball off the premises", clamped);

var high = new UnityEngine.Vector3(0f, 1f, -11f);
float apexWeak = ArcadeTennis.Characters.SwingSolver.ResolveApex(0f, perfect, high, cfg);
float apexFull = ArcadeTennis.Characters.SwingSolver.ResolveApex(1f, perfect, high, cfg);
check("power lifts the arc; a weak shot is barely raised", apexFull > apexWeak);

var low = new UnityEngine.Vector3(0f, 0.08f, -11f);
float apexLowFull = ArcadeTennis.Characters.SwingSolver.ResolveApex(1f, perfect, low, cfg);
check("a full swing off the ground is still given an arc that can clear the net",
    apexLowFull + low.y >= cfg.MinPeakHeight - 1e-4f);

// The counterpart: that guarantee is earned by the swing, not handed out. A
// stab off the ground has to be allowed to fail.
float apexLowWeak = ArcadeTennis.Characters.SwingSolver.ResolveApex(0f, perfect, low, cfg);
check("a weak scoop off the ground gets no such guarantee",
    apexLowWeak + low.y < cfg.MinPeakHeight - 0.1f);

// =========================================================================
// End to end, through the real ball simulation
// =========================================================================
System.Func<UnityEngine.Vector3, UnityEngine.Vector3, float, ArcadeTennis.BallPhysics.BallPrediction> playOut =
    (from, target, apex) =>
    {
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
            from, target, apex, ballCfg, court, dt);
        return ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);
    };

var contactPoint = new UnityEngine.Vector3(0f, charCfg.HitHeight, -11f);
var driveTarget = aimAt(-1, 0.55f, UnityEngine.Vector2.zero, perfect, noSpread);
var drive = playOut(contactPoint, driveTarget,
    ArcadeTennis.Characters.SwingSolver.ResolveApex(0.55f, perfect, contactPoint, cfg));

check("a clean drive clears the net", drive.IsBounce);
check("a clean drive lands in the opponent's court",
    drive.IsBounce && court.IsInBounds(drive.Position) && drive.Position.z > 0f);
check("it lands where it was aimed",
    drive.HasResult && UnityEngine.Vector3.Distance(
        new UnityEngine.Vector3(drive.Position.x, 0f, drive.Position.z), driveTarget) < 0.25f);

var wideTarget = aimAt(-1, 0.55f, new UnityEngine.Vector2(1f, 0f), perfect, noSpread);
var wideDrive = playOut(contactPoint, wideTarget,
    ArcadeTennis.Characters.SwingSolver.ResolveApex(0.55f, perfect, contactPoint, cfg));
check("a wide drive also clears the net and lands in",
    wideDrive.IsBounce && court.IsInBounds(wideDrive.Position) && wideDrive.Position.x > 2f);

// The ball met at ankle height is the case the arc floor exists for.
var lowContact = new UnityEngine.Vector3(0f, 0.10f, -11f);
var lowDrive = playOut(lowContact, aimAt(-1, 1f, UnityEngine.Vector2.zero, perfect, noSpread),
    ArcadeTennis.Characters.SwingSolver.ResolveApex(1f, perfect, lowContact, cfg));
check("a ball hit hard off the ground still gets over the net", !lowDrive.IsNetHit);

// ---------------------------------------------------------------------
// The charge has to be able to lose the point at both ends. This is the
// check that would have caught the first tuning, where every clean shot
// landed in however long the button was held.
var bands = new System.Text.StringBuilder();
int netBand = 0, inBand = 0, outBand = 0, previousBand = 0;
bool ordered = true;

for (float power = 0f; power <= 1.0001f; power += 0.05f)
{
    var t = aimAt(-1, power, UnityEngine.Vector2.zero, perfect, noSpread);
    var shot = playOut(contactPoint, t,
        ArcadeTennis.Characters.SwingSolver.ResolveApex(power, perfect, contactPoint, cfg));

    int band = shot.IsNetHit ? 0
             : (shot.IsBounce && court.IsInBounds(shot.Position) && shot.Position.z > 0f ? 1 : 2);

    if (band < previousBand) ordered = false;
    previousBand = band;

    if (band == 0) netBand++; else if (band == 1) inBand++; else outBand++;
    bands.Append(band == 0 ? "n" : (band == 1 ? "." : "o"));
}

check("too little charge drops the ball into the net [" + bands + "]", netBand >= 2);
check("too much charge sends the ball past the baseline", outBand >= 2);
check("most of the charge range is still playable", inBand >= 10);
check("the outcomes come in order: net, then in, then out", ordered);

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

var indicator = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Presentation.SwingIndicator>();
bool indicatorWired = indicator != null;
if (indicatorWired)
{
    var iso = new UnityEditor.SerializedObject(indicator);
    indicatorWired = iso.FindProperty("swing").objectReferenceValue != null
                  && iso.FindProperty("fill").objectReferenceValue != null
                  && iso.FindProperty("character").objectReferenceValue != null;
}
check("the charge bar exists and is wired to the player", indicatorWired);

var feeder = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.DebugTools.BallFeeder>();
check("a feeder puts balls into play until the serve exists", feeder != null);

var input = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Characters.PlayerInputController>();
bool swingBound = false;
if (input != null)
{
    var iso = new UnityEditor.SerializedObject(input);
    var asset = iso.FindProperty("controls").objectReferenceValue
        as UnityEngine.InputSystem.InputActionAsset;
    swingBound = asset != null
        && asset.FindActionMap("Match", false) != null
        && asset.FindActionMap("Match", false).FindAction("Swing", false) != null;
}
check("the player's input asset carries a Swing action", swingBound);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
