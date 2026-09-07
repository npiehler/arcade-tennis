// Verification for the serve. The toss is flown through the same BallSimulation
// the live ball uses and the serve is judged by the same CourtDefinition the
// rules will use, so none of this needs play mode.
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
// Wo der Aufschlaeger steht und was er treffen muss
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
check("Einstand und Vorteil werden von verschiedenen Seiten aufgeschlagen",
    UnityEngine.Mathf.Sign(standDeuce.x)
        != UnityEngine.Mathf.Sign(court.GetServePosition(-1, false).x));

var tossOrigin = ArcadeTennis.Characters.ServeSolver.TossOrigin(standDeuce, -1, cfg);
var hitCentre = ArcadeTennis.Characters.ServeSolver.HitCentre(standDeuce, -1, cfg);
check("der Treffpunkt liegt senkrecht ueber dem Wurf",
    UnityEngine.Mathf.Abs(tossOrigin.x - hitCentre.x) < 0.001f
    && UnityEngine.Mathf.Abs(tossOrigin.z - hitCentre.z) < 0.001f);
check("der Treffpunkt liegt ueber dem Kopf", hitCentre.y > 2.2f);

// =========================================================================
// Der Wurf
// =========================================================================
System.Func<float, ArcadeTennis.BallPhysics.BallState> tossAt = (time) => {
    var st = new ArcadeTennis.BallPhysics.BallState(
        tossOrigin, ArcadeTennis.Characters.ServeSolver.TossVelocity(cfg));
    for (float t = 0f; t < time - 0.0001f; t += dt)
        ArcadeTennis.BallPhysics.BallSimulation.Advance(ref st, ballCfg, null, dt);
    return st;
};

float peak = 0f, dropsBelowCatch = -1f, apexTime = 0f;
for (float t = 0f; t <= 2.5f; t += dt)
{
    var st = tossAt(t);
    if (st.Position.y > peak) { peak = st.Position.y; apexTime = t; }
    if (dropsBelowCatch < 0f && t > 0.2f && st.Position.y < cfg.CatchHeight) dropsBelowCatch = t;
}
check("der Wurf steigt ueber den Treffpunkt hinaus", peak > cfg.HitHeight + 0.2f);
check("der Wurf kommt wieder herunter, ein liegengelassener Ball ist erkennbar",
    dropsBelowCatch > 0.5f);
check("der Wurf bleibt lange genug oben, um ihn zu treffen",
    dropsBelowCatch > cfg.ContactDelay + 0.4f);

// Am Scheitel steht der Ball fast still. Ohne die Absicherung in Evaluate
// waere ein bequem erreichbarer Ball als "viel zu frueh" durchgefallen.
var atApex = tossAt(apexTime);
var apexContact = ArcadeTennis.Characters.SwingSolver.Evaluate(
    atApex.Position, atApex.Velocity, hitCentre, UnityEngine.Vector3.zero, cfg.Contact());
check("ein Ball, der am Scheitel steht, ist trotzdem zu treffen", apexContact.Made);
check("und wird raeumlich beurteilt, nicht zeitlich",
    UnityEngine.Mathf.Abs(apexContact.TimingOffset) < 0.001f);

// =========================================================================
// Zustandsautomat: zwei Tastendruecke, kein Halten
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
check("der erste Tastendruck wirft den Ball hoch",
    act == ArcadeTennis.Characters.ServeAction.Toss
    && state.Phase == ArcadeTennis.Characters.ServePhase.Tossing);

// Gedrueckt halten darf nicht sofort durchschlagen -- es sind zwei Druecke.
for (int i = 0; i < 40; i++)
    ArcadeTennis.Characters.ServeSolver.Tick(ref state, true, true, cfg, dt, out act);
check("die Taste gedrueckt zu lassen schlaegt nicht von selbst",
    state.Phase == ArcadeTennis.Characters.ServePhase.Tossing);

ArcadeTennis.Characters.ServeSolver.Tick(ref state, false, true, cfg, dt, out act);
ArcadeTennis.Characters.ServeSolver.Tick(ref state, true, true, cfg, dt, out act);
check("der zweite Tastendruck startet den Schwung",
    state.Phase == ArcadeTennis.Characters.ServePhase.Swinging);

int contactTicks = 0, contacts = 0;
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

var dropped = ArcadeTennis.Characters.ServeState.New();
ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, true, false, cfg, dt, out act);
for (int i = 0; i < 10; i++)
    ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, false, true, cfg, dt, out act);
ArcadeTennis.Characters.ServeSolver.Tick(ref dropped, false, false, cfg, dt, out act);
check("ein fallengelassener Wurf meldet sich",
    act == ArcadeTennis.Characters.ServeAction.TossDropped);
check("und kostet keinen Aufschlag",
    dropped.Phase == ArcadeTennis.Characters.ServePhase.Ready && dropped.ServeNumber == 1);

// =========================================================================
// Erster und zweiter Aufschlag
// =========================================================================
var count = ArcadeTennis.Characters.ServeState.New();
check("ein Fehler beim ersten Aufschlag ist kein Doppelfehler",
    !ArcadeTennis.Characters.ServeSolver.RegisterFault(ref count));
check("und fuehrt zum zweiten Aufschlag", count.ServeNumber == 2 && count.IsSecondServe);
check("ein Fehler beim zweiten ist ein Doppelfehler",
    ArcadeTennis.Characters.ServeSolver.RegisterFault(ref count));
check("danach beginnt wieder mit dem ersten", count.ServeNumber == 1);

bool courtBefore = count.DeuceCourt;
ArcadeTennis.Characters.ServeSolver.NextPoint(ref count);
check("der naechste Punkt wird aus dem anderen Feld aufgeschlagen",
    count.DeuceCourt != courtBefore);

// =========================================================================
// Die drei Zonen im Aufschlagfeld
// =========================================================================
var boxDeuce = court.GetServiceBox(1, true);
float wide = ArcadeTennis.Court.AimZones.ServeLaneCentre(court, -1, true, ArcadeTennis.Court.AimZone.Left);
float body = ArcadeTennis.Court.AimZones.ServeLaneCentre(court, -1, true, ArcadeTennis.Court.AimZone.Centre);
float tee = ArcadeTennis.Court.AimZones.ServeLaneCentre(court, -1, true, ArcadeTennis.Court.AimZone.Right);

check("drei Bahnen teilen das Aufschlagfeld, gleichmaessig", wide < body && body < tee
    && UnityEngine.Mathf.Abs((body - wide) - (tee - body)) < 0.001f);
check("die mittlere Bahn ist die Mitte des Feldes",
    UnityEngine.Mathf.Abs(body - boxDeuce.center.x) < 0.001f);
check("alle drei Bahnen liegen im Feld",
    wide > boxDeuce.xMin && tee < boxDeuce.xMax);
check("links zielt Richtung Seitenlinie, rechts Richtung Mittellinie",
    UnityEngine.Mathf.Abs(wide - boxDeuce.xMin) < UnityEngine.Mathf.Abs(tee - boxDeuce.xMin));
check("fuer den Gegner ist das gespiegelt",
    ArcadeTennis.Court.AimZones.ServeLaneCentre(court, 1, true, ArcadeTennis.Court.AimZone.Right)
        < ArcadeTennis.Court.AimZones.ServeLaneCentre(court, 1, true, ArcadeTennis.Court.AimZone.Left));

// =========================================================================
// Zielsetzung
// =========================================================================
System.Func<int, bool, ArcadeTennis.Court.AimZone, float, UnityEngine.Vector2, UnityEngine.Vector3> aimAt =
    (server, deuce, zone, quality, spread) =>
        ArcadeTennis.Characters.ServeSolver.ResolveTarget(server, deuce, zone,
            new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality },
            cfg, court, spread);

var weakServe = aimAt(-1, true, ArcadeTennis.Court.AimZone.Centre, 0f, noSpread);
var strongServe = aimAt(-1, true, ArcadeTennis.Court.AimZone.Centre, 1f, noSpread);
check("schlechter Kontakt zielt an den Netzfuss", UnityEngine.Mathf.Abs(weakServe.z) < 2.0f);
check("sauberer Kontakt zielt tief ins Feld",
    UnityEngine.Mathf.Abs(strongServe.z) > court.ServiceLineDistance * 0.7f);
check("und bleibt dabei im Feld", court.IsInServiceBox(strongServe, 1, true));

bool zonesLandIn = true;
foreach (int z in new int[] { -1, 0, 1 })
    if (!court.IsInServiceBox(aimAt(-1, true, (ArcadeTennis.Court.AimZone)z, 1f, noSpread), 1, true))
        zonesLandIn = false;
check("alle drei Zonen liegen bei sauberem Kontakt im Feld", zonesLandIn);

float worstCentreSlip = 0f;
for (float q = 0f; q <= 1.0001f; q += 0.1f)
for (int i = 0; i < 48; i++)
{
    float a = i / 48f * UnityEngine.Mathf.PI * 2f;
    var spread = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a), UnityEngine.Mathf.Sin(a));
    var t = aimAt(-1, true, ArcadeTennis.Court.AimZone.Centre, q, spread);
    worstCentreSlip = UnityEngine.Mathf.Max(worstCentreSlip,
        UnityEngine.Mathf.Abs(t.x - boxDeuce.center.x));
}
check("die mittlere Zone bleibt in der Feldmitte", worstCentreSlip < boxDeuce.width * 0.35f);

// =========================================================================
// Flug durch die echte Simulation
// =========================================================================
System.Func<int, bool, ArcadeTennis.Court.AimZone, float, string> fly =
    (server, deuce, zone, quality) =>
    {
        var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
        var from = ArcadeTennis.Characters.ServeSolver.HitCentre(
            court.GetServePosition(server, deuce), server, cfg);
        var target = ArcadeTennis.Characters.ServeSolver.ResolveTarget(
            server, deuce, zone, judged, cfg, court, noSpread);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
            from, target, ArcadeTennis.Characters.ServeSolver.ResolveApex(judged, cfg),
            ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);

        if (!p.HasResult) return "?";
        if (p.IsNetHit) return "n";
        return court.IsInServiceBox(p.Position, -server, deuce) ? "." : "o";
    };

foreach (int server in new int[] { -1, 1 })
foreach (bool deuce in new bool[] { true, false })
foreach (int z in new int[] { -1, 0, 1 })
{
    var line = new System.Text.StringBuilder();
    int netCount = 0, inCount = 0, previous = 0;
    bool ordered = true;

    for (float q = 0f; q <= 1.0001f; q += 0.05f)
    {
        string b = fly(server, deuce, (ArcadeTennis.Court.AimZone)z, q);
        line.Append(b);
        int band = b == "n" ? 0 : b == "." ? 1 : 2;
        if (band < previous) ordered = false;
        previous = band;
        if (band == 0) netCount++; else if (band == 1) inCount++;
    }

    string who = "Seite " + server + (deuce ? " Einstand " : " Vorteil ")
        + ((ArcadeTennis.Court.AimZone)z).ToString();
    check(who + ": schlechter Kontakt faellt ins Netz [" + line + "]", netCount >= 1);
    check(who + ": der grosse Teil ist spielbar", inCount >= 14);
    check(who + ": Netz, dann drin", ordered);
}

// =========================================================================
// Das Urteil
// =========================================================================
check("ein Ball im richtigen Feld ist gut",
    ArcadeTennis.Characters.ServeSolver.Judge(new UnityEngine.Vector3(-2f, 0.03f, 3f),
        false, -1, true, court) == ArcadeTennis.Characters.ServeOutcome.In);
check("hinter der Aufschlaglinie ist Fehler",
    ArcadeTennis.Characters.ServeSolver.Judge(new UnityEngine.Vector3(-2f, 0.03f, 7.5f),
        false, -1, true, court) == ArcadeTennis.Characters.ServeOutcome.OutFault);
check("im falschen Aufschlagfeld ist Fehler",
    ArcadeTennis.Characters.ServeSolver.Judge(new UnityEngine.Vector3(2f, 0.03f, 3f),
        false, -1, true, court) == ArcadeTennis.Characters.ServeOutcome.OutFault);
check("ein Netztreffer ist Fehler, kein Let",
    ArcadeTennis.Characters.ServeSolver.Judge(new UnityEngine.Vector3(-2f, 0.03f, 3f),
        true, -1, true, court) == ArcadeTennis.Characters.ServeOutcome.NetFault);

// =========================================================================
// Der Rhythmus: wann der zweite Druck kommt, entscheidet
// =========================================================================
System.Func<float, string> rhythm = (strike) => {
    var st = tossAt(strike + cfg.ContactDelay);
    var contact = ArcadeTennis.Characters.SwingSolver.Evaluate(
        st.Position, st.Velocity, hitCentre, UnityEngine.Vector3.zero, cfg.Contact());
    if (!contact.Made) return "x";
    return fly(-1, true, ArcadeTennis.Court.AimZone.Centre, contact.Quality);
};
check("im richtigen Moment geschlagen landet der Aufschlag im Feld", rhythm(0.65f) == ".");
check("viel zu spaet ist der Ball nicht mehr zu erreichen", rhythm(1.0f) == "x");

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
check("der Uebungstreiber startet die Punkte", driver != null
    && new UnityEditor.SerializedObject(driver).FindProperty("serve").objectReferenceValue != null);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
