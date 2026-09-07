// Milestone 5 verification. The serve is built out of pure functions, so all of
// this runs in edit mode: the toss is flown through the same BallSimulation the
// live ball uses, and the serve is judged by the same CourtDefinition the rules
// will use in milestone 6.
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.ServeConfig>(
    "Assets/_Game/Settings/ServeConfig.asset");
var ballCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = 0.02f;
var sb = new System.Text.StringBuilder();
int failed = 0;

System.Action<string, bool> check = (label, ok) => {
    if (!ok) failed++;
    sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
};

var perfect = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = 1f };
var noSpread = UnityEngine.Vector2.zero;

// =========================================================================
// Where the server stands and what it has to hit
// =========================================================================
foreach (int server in new int[] { -1, 1 })
foreach (bool deuce in new bool[] { true, false })
{
    var stand = court.GetServePosition(server, deuce);
    var box = court.GetServiceBox(-server, deuce);
    string who = "Seite " + server + (deuce ? " Einstand" : " Vorteil");

    check(who + ": Aufschlaeger steht hinter der eigenen Grundlinie",
        UnityEngine.Mathf.Abs(stand.z) > court.HalfLength);
    check(who + ": das Feld liegt diagonal gegenueber",
        UnityEngine.Mathf.Sign(box.center.y) == -UnityEngine.Mathf.Sign(stand.z)
        && UnityEngine.Mathf.Sign(box.center.x) == -UnityEngine.Mathf.Sign(stand.x));
}

var standDeuce = court.GetServePosition(-1, true);
var standAd = court.GetServePosition(-1, false);
check("Einstand und Vorteil werden von verschiedenen Seiten aufgeschlagen",
    UnityEngine.Mathf.Sign(standDeuce.x) != UnityEngine.Mathf.Sign(standAd.x));

var tossOrigin = ArcadeTennis.Characters.ServeSolver.TossOrigin(standDeuce, -1, cfg);
var hitCentre = ArcadeTennis.Characters.ServeSolver.HitCentre(standDeuce, -1, cfg);
check("der Treffpunkt liegt senkrecht ueber dem Wurf",
    UnityEngine.Mathf.Abs(tossOrigin.x - hitCentre.x) < 0.001f
    && UnityEngine.Mathf.Abs(tossOrigin.z - hitCentre.z) < 0.001f);
check("der Treffpunkt liegt ueber dem Kopf", hitCentre.y > 2.2f);
check("der Wurf beginnt vor dem Aufschlaeger, nicht in ihm",
    UnityEngine.Mathf.Abs(tossOrigin.z) < UnityEngine.Mathf.Abs(standDeuce.z));

// =========================================================================
// Der Wurf, geflogen mit der echten Ballsimulation
// =========================================================================
// Liefert Ballzustand und Zeit fuer einen Kontakt zum Zeitpunkt release+delay.
System.Func<float, ArcadeTennis.BallPhysics.BallState> tossAt = (time) =>
{
    var st = new ArcadeTennis.BallPhysics.BallState(
        tossOrigin, ArcadeTennis.Characters.ServeSolver.TossVelocity(cfg));
    for (float t = 0f; t < time - 0.0001f; t += dt)
        ArcadeTennis.BallPhysics.BallSimulation.Advance(ref st, ballCfg, null, dt);
    return st;
};

float tossPeak = 0f;
float dropsBelowCatch = -1f;
for (float t = 0f; t <= 2.5f; t += dt)
{
    var st = tossAt(t);
    tossPeak = UnityEngine.Mathf.Max(tossPeak, st.Position.y);
    if (dropsBelowCatch < 0f && t > 0.2f && st.Position.y < cfg.CatchHeight) dropsBelowCatch = t;
}

check("der Wurf steigt ueber den Treffpunkt hinaus", tossPeak > cfg.HitHeight + 0.2f);
check("der Wurf kommt wieder herunter, ein liegengelassener Ball ist erkennbar",
    dropsBelowCatch > 0.5f);
check("der Wurf bleibt lange genug oben, um ihn zu treffen",
    dropsBelowCatch > cfg.ContactDelay + 0.4f);

// =========================================================================
// Zustandsautomat
// =========================================================================
var state = ArcadeTennis.Characters.ServeState.New();
ArcadeTennis.Characters.ServeAction act;

check("ein frischer Aufschlag beginnt mit dem ersten und im Einstandsfeld",
    state.Phase == ArcadeTennis.Characters.ServePhase.Ready
    && state.ServeNumber == 1 && state.DeuceCourt);

for (int i = 0; i < 50; i++)
    ArcadeTennis.Characters.ServeSolver.Tick(ref state, false, false, cfg, dt, out act);
check("ohne Tastendruck passiert nichts",
    state.Phase == ArcadeTennis.Characters.ServePhase.Ready);

ArcadeTennis.Characters.ServeSolver.Tick(ref state, true, false, cfg, dt, out act);
check("der Tastendruck wirft den Ball hoch",
    act == ArcadeTennis.Characters.ServeAction.Toss
    && state.Phase == ArcadeTennis.Characters.ServePhase.Tossing);

int chargeTicks = UnityEngine.Mathf.RoundToInt(cfg.ChargeTime / dt);
for (int i = 0; i < chargeTicks; i++)
    ArcadeTennis.Characters.ServeSolver.Tick(ref state, true, true, cfg, dt, out act);
check("die Aufladung erreicht in der vorgesehenen Zeit ihr Maximum", state.Charge > 0.999f);

ArcadeTennis.Characters.ServeSolver.Tick(ref state, false, true, cfg, dt, out act);
check("das Loslassen startet den Schwung",
    state.Phase == ArcadeTennis.Characters.ServePhase.Swinging);

int contactTicks = 0;
int contacts = 0;
for (int i = 1; i <= 100; i++)
{
    ArcadeTennis.Characters.ServeSolver.Tick(ref state, false, true, cfg, dt, out act);
    if (act == ArcadeTennis.Characters.ServeAction.Contact)
    {
        contacts++;
        if (contactTicks == 0) contactTicks = i;
    }
}
check("der Schlaeger erreicht den Ball genau einmal", contacts == 1);
check("und zwar nach der eingestellten Verzoegerung",
    contactTicks * dt >= cfg.ContactDelay - 1e-6f
    && contactTicks * dt < cfg.ContactDelay + dt + 1e-6f);

// Einen Wurf einfach fallen lassen darf nichts kosten.
var dropped = ArcadeTennis.Characters.ServeState.New();
ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, true, false, cfg, dt, out act);
for (int i = 0; i < 10; i++)
    ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, true, true, cfg, dt, out act);
ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, true, false, cfg, dt, out act);
check("ein fallengelassener Wurf meldet sich",
    act == ArcadeTennis.Characters.ServeAction.TossDropped);
check("und kostet weder Aufschlag noch Aufladung",
    dropped.Phase == ArcadeTennis.Characters.ServePhase.Ready
    && dropped.ServeNumber == 1 && dropped.Charge == 0f);

// =========================================================================
// Erster und zweiter Aufschlag
// =========================================================================
var count = ArcadeTennis.Characters.ServeState.New();
bool firstIsDouble = ArcadeTennis.Characters.ServeSolver.RegisterFault(ref count);
check("ein Fehler beim ersten Aufschlag ist kein Doppelfehler", !firstIsDouble);
check("und fuehrt zum zweiten Aufschlag", count.ServeNumber == 2 && count.IsSecondServe);

bool secondIsDouble = ArcadeTennis.Characters.ServeSolver.RegisterFault(ref count);
check("ein Fehler beim zweiten ist ein Doppelfehler", secondIsDouble);
check("danach beginnt wieder mit dem ersten", count.ServeNumber == 1);

bool courtBefore = count.DeuceCourt;
ArcadeTennis.Characters.ServeSolver.NextPoint(ref count);
check("der naechste Punkt wird aus dem anderen Feld aufgeschlagen",
    count.DeuceCourt != courtBefore);
check("und wieder mit dem ersten Aufschlag",
    count.ServeNumber == 1 && count.Phase == ArcadeTennis.Characters.ServePhase.Ready);

// =========================================================================
// Zielsetzung
// =========================================================================
System.Func<int, bool, float, float, UnityEngine.Vector2, UnityEngine.Vector3> aimAt =
    (server, deuce, power, aimX, spread) =>
        ArcadeTennis.Characters.ServeSolver.ResolveTarget(server, deuce, power,
            new UnityEngine.Vector2(aimX, 0f), perfect, cfg, court, spread);

var weak = aimAt(-1, true, 0f, 0f, noSpread);
var strong = aimAt(-1, true, 1f, 0f, noSpread);
var middling = aimAt(-1, true, 0.5f, 0f, noSpread);

check("ohne Aufladung wird an den Netzfuss gezielt",
    UnityEngine.Mathf.Abs(weak.z) < 1.0f);
check("mit voller Aufladung hinter die Aufschlaglinie",
    UnityEngine.Mathf.Abs(strong.z) > court.ServiceLineDistance);
check("eine mittlere Aufladung zielt ins Feld",
    court.IsInServiceBox(middling, 1, true));

var boxDeuce = court.GetServiceBox(1, true);
var wideAim = aimAt(-1, true, 0.5f, -1f, noSpread);
var tAim = aimAt(-1, true, 0.5f, 1f, noSpread);
check("nach links gezielt geht Richtung Seitenlinie", wideAim.x < middling.x);
check("nach rechts gezielt geht Richtung Mittellinie", tAim.x > middling.x);
check("beide bleiben bei sauberem Kontakt im Feld",
    court.IsInServiceBox(wideAim, 1, true) && court.IsInServiceBox(tAim, 1, true));

var tAimFar = aimAt(1, true, 0.5f, 1f, noSpread);
check("die Seitwaerts-Eingabe ist fuer den Gegner gespiegelt",
    UnityEngine.Mathf.Sign(tAimFar.x - aimAt(1, true, 0.5f, 0f, noSpread).x)
        == -UnityEngine.Mathf.Sign(tAim.x - middling.x));

// Volles Zielen muss bei perfektem Kontakt im Feld bleiben -- die Tastatur
// kennt nur ganz links, Mitte und ganz rechts.
bool wideStaysIn = true;
for (int i = 0; i < 48; i++)
{
    float a = i / 48f * UnityEngine.Mathf.PI * 2f;
    var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));
    if (!court.IsInServiceBox(aimAt(-1, true, 0.5f, -1f, spread), 1, true)) wideStaysIn = false;
}
check("volles Seitwaerts-Zielen bleibt bei perfektem Kontakt im Feld", wideStaysIn);

// Ohne Richtungseingabe muss der Aufschlag in die Mitte des Feldes gehen --
// bei jeder Trefferqualitaet, nicht nur bei der perfekten.
float worstCentreSlip = 0f;
for (float q = 0f; q <= 1.0001f; q += 0.1f)
{
    var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = q };
    for (int i = 0; i < 48; i++)
    {
        float a = i / 48f * UnityEngine.Mathf.PI * 2f;
        var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));
        var t = ArcadeTennis.Characters.ServeSolver.ResolveTarget(-1, true, 0.5f,
            UnityEngine.Vector2.zero, judged, cfg, court, spread);
        worstCentreSlip = UnityEngine.Mathf.Max(worstCentreSlip,
            UnityEngine.Mathf.Abs(t.x - boxDeuce.center.x));
    }
}
check("ohne Richtungseingabe zielt der Aufschlag in die Feldmitte",
    worstCentreSlip < boxDeuce.width * 0.35f);

// =========================================================================
// Flug durch die echte Simulation
// =========================================================================
System.Func<int, bool, float, float, float, string> fly =
    (server, deuce, power, aimX, quality) =>
    {
        var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
        var from = ArcadeTennis.Characters.ServeSolver.HitCentre(
            court.GetServePosition(server, deuce), server, cfg);
        var target = ArcadeTennis.Characters.ServeSolver.ResolveTarget(
            server, deuce, power, new UnityEngine.Vector2(aimX, 0f), judged, cfg, court, noSpread);
        float apex = ArcadeTennis.Characters.ServeSolver.ResolveApex(power, judged, cfg);

        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
            from, target, apex, ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);

        if (!p.HasResult) return "?";
        if (p.IsNetHit) return "n";
        return court.IsInServiceBox(p.Position, -server, deuce) ? "." : "o";
    };

foreach (int server in new int[] { -1, 1 })
foreach (bool deuce in new bool[] { true, false })
{
    var line = new System.Text.StringBuilder();
    int net = 0, inBox = 0, outBox = 0, previous = 0;
    bool ordered = true;

    for (float power = 0f; power <= 1.0001f; power += 0.05f)
    {
        string b = fly(server, deuce, power, 0f, 1f);
        line.Append(b);
        int band = b == "n" ? 0 : b == "." ? 1 : 2;
        if (band < previous) ordered = false;
        previous = band;
        if (band == 0) net++; else if (band == 1) inBox++; else outBox++;
    }

    string who = "Seite " + server + (deuce ? " Einstand" : " Vorteil");
    check(who + ": zu wenig Aufladung faellt ins Netz [" + line + "]", net >= 2);
    check(who + ": zu viel Aufladung geht hinter die Aufschlaglinie", outBox >= 2);
    check(who + ": der grosse Teil der Aufladung ist spielbar", inBox >= 10);
    check(who + ": Netz, dann drin, dann daneben", ordered);
}

// =========================================================================
// Das Urteil
// =========================================================================
var inBoxPoint = new UnityEngine.Vector3(-2.0f, 0.03f, 3.0f);
var longPoint = new UnityEngine.Vector3(-2.0f, 0.03f, 7.5f);
var wrongBox = new UnityEngine.Vector3(2.0f, 0.03f, 3.0f);

check("ein Ball im richtigen Feld ist gut",
    ArcadeTennis.Characters.ServeSolver.Judge(inBoxPoint, false, -1, true, court)
        == ArcadeTennis.Characters.ServeOutcome.In);
check("hinter der Aufschlaglinie ist Fehler",
    ArcadeTennis.Characters.ServeSolver.Judge(longPoint, false, -1, true, court)
        == ArcadeTennis.Characters.ServeOutcome.OutFault);
check("im falschen Aufschlagfeld ist Fehler",
    ArcadeTennis.Characters.ServeSolver.Judge(wrongBox, false, -1, true, court)
        == ArcadeTennis.Characters.ServeOutcome.OutFault);
check("ein Netztreffer ist Fehler, kein Let",
    ArcadeTennis.Characters.ServeSolver.Judge(inBoxPoint, true, -1, true, court)
        == ArcadeTennis.Characters.ServeOutcome.NetFault);

// =========================================================================
// Der Rhythmus: wann losgelassen wird, entscheidet
// =========================================================================
System.Func<float, string> rhythm = (release) =>
{
    var st = tossAt(release + cfg.ContactDelay);
    var contact = ArcadeTennis.Characters.SwingSolver.Evaluate(
        st.Position, st.Velocity, hitCentre, UnityEngine.Vector3.zero, cfg.Contact());
    if (!contact.Made) return "x";
    return fly(-1, true, UnityEngine.Mathf.Clamp01(release / cfg.ChargeTime), 0f, contact.Quality);
};

check("sofort losgelassen faellt der Aufschlag ins Netz", rhythm(0.05f) == "n");
check("im richtigen Moment landet er im Feld", rhythm(0.45f) == ".");
check("zu lang gehalten geht er hinter die Linie", rhythm(0.75f) == "o");
check("viel zu spaet ist der Ball nicht mehr zu erreichen", rhythm(1.0f) == "x");

// Am Scheitel steht der Ball fast still. Die geschlossene Form fuer den
// dichtesten Punkt zerfaellt dort, und ohne die Absicherung in Evaluate
// waere ein bequem erreichbarer Ball als "viel zu frueh" durchgefallen.
float apexTime = 0f;
float apexHeight = 0f;
for (float t = 0f; t <= 1.2f; t += dt)
{
    var st = tossAt(t);
    if (st.Position.y > apexHeight) { apexHeight = st.Position.y; apexTime = t; }
}
var atApex = tossAt(apexTime);
var apexContact = ArcadeTennis.Characters.SwingSolver.Evaluate(
    atApex.Position, atApex.Velocity, hitCentre, UnityEngine.Vector3.zero, cfg.Contact());
check("ein Ball, der am Scheitel steht, ist trotzdem zu treffen", apexContact.Made);
check("und wird raeumlich beurteilt, nicht zeitlich",
    UnityEngine.Mathf.Abs(apexContact.TimingOffset) < 0.001f);

// =========================================================================
// Szenenverdrahtung
// =========================================================================
var servers = UnityEngine.Object.FindObjectsByType<ArcadeTennis.Characters.ServeController>(
    UnityEngine.FindObjectsSortMode.None);
check("beide Figuren koennen aufschlagen", servers.Length == 2);

bool wired = servers.Length == 2;
foreach (var srv in servers)
{
    var so = new UnityEditor.SerializedObject(srv);
    if (so.FindProperty("config").objectReferenceValue == null) wired = false;
    if (so.FindProperty("ball").objectReferenceValue == null) wired = false;
    if (so.FindProperty("rallySwing").objectReferenceValue == null) wired = false;
}
check("jeder Aufschlag hat Einstellung, Ball und den Grundschlag, den er zurueckhaelt", wired);

var driver = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.DebugTools.ServePracticeDriver>();
bool driverWired = driver != null;
if (driverWired)
{
    var dso = new UnityEditor.SerializedObject(driver);
    driverWired = dso.FindProperty("serve").objectReferenceValue != null
               && dso.FindProperty("ball").objectReferenceValue != null;
}
check("der Uebungstreiber startet die Punkte und ist verdrahtet", driverWired);

var indicator = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Presentation.SwingIndicator>();
bool indicatorKnowsServe = indicator != null
    && new UnityEditor.SerializedObject(indicator).FindProperty("serve").objectReferenceValue != null;
check("der Ladebalken zeigt auch den Aufschlag", indicatorKnowsServe);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
