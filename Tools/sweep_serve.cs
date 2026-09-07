// Tuning aid for the serve, the counterpart of sweep_charge.cs.
//
//   unity cmd eval_file --file "Tools/sweep_serve.cs" --timeout 180000 --json
//
// Two views. The charge band answers "what does hold time do to the depth"; the
// toss rhythm answers the question the player actually asks, which is "when do I
// let go" -- because pressing starts the toss AND the charge, the two are one
// decision and have to be tuned together.
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.ServeConfig>(
    "Assets/_Game/Settings/ServeConfig.asset");
var ballCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = 0.02f;
int server = -1;
bool deuce = true;
var stand = court.GetServePosition(server, deuce);
var hitCentre = ArcadeTennis.Characters.ServeSolver.HitCentre(stand, server, cfg);
var sb = new System.Text.StringBuilder();

sb.AppendLine("Treffhoehe " + cfg.HitHeight.ToString("0.00")
    + "  Wurf " + cfg.TossHeight.ToString("0.00") + " m mit " + cfg.TossSpeed.ToString("0.0") + " m/s"
    + "  Reichweite " + cfg.ReachRadius.ToString("0.00")
    + "  Sweet Spot " + cfg.SweetSpotRadius.ToString("0.00"));
sb.AppendLine("Ladezeit " + cfg.ChargeTime.ToString("0.00") + " s"
    + "  Trefferverzug " + cfg.ContactDelay.ToString("0.00") + " s"
    + "  Tiefe " + cfg.MinDepth.ToString("0.00") + " .. "
    + (court.ServiceLineDistance + cfg.ServiceLineOvershoot).ToString("0.00"));
sb.AppendLine();

System.Func<float, float, float, string> outcomeOf = (power, quality, aimX) =>
{
    var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
    var target = ArcadeTennis.Characters.ServeSolver.ResolveTarget(
        server, deuce, power, new UnityEngine.Vector2(aimX, 0f), judged, cfg, court,
        UnityEngine.Vector2.zero);
    float apex = ArcadeTennis.Characters.ServeSolver.ResolveApex(power, judged, cfg);
    var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
        hitCentre, target, apex, ballCfg, court, dt);
    var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
        new ArcadeTennis.BallPhysics.BallState(hitCentre, v), ballCfg, court, dt);

    if (!p.HasResult) return "?";
    if (p.IsNetHit) return "n";
    return court.IsInServiceBox(p.Position, -server, deuce) ? "." : "o";
};

// --- view 1: hold time alone ------------------------------------------
sb.AppendLine("Aufladung 0.00 -> 1.00    n = Netz, . = im Feld, o = daneben");
foreach (float q in new float[] { 1.0f, 0.6f })
{
    var line = new System.Text.StringBuilder();
    for (float power = 0f; power <= 1.0001f; power += 0.05f) line.Append(outcomeOf(power, q, 0f));
    sb.AppendLine("  Qualitaet " + q.ToString("0.0") + "  " + line);
}
sb.AppendLine();

// --- view 2: the rhythm -----------------------------------------------
sb.AppendLine("Wurfrhythmus: Ball fliegt ab Tastendruck, Kontakt "
    + cfg.ContactDelay.ToString("0.00") + " s nach dem Loslassen");
sb.AppendLine("  loslassen  Ballhoehe  Abstand  Aufladung  Note      Ergebnis");

for (float release = 0f; release <= 1.05f; release += 0.05f)
{
    // Fly the toss with the same simulation the live ball uses.
    var toss = new ArcadeTennis.BallPhysics.BallState(
        ArcadeTennis.Characters.ServeSolver.TossOrigin(stand, server, cfg),
        ArcadeTennis.Characters.ServeSolver.TossVelocity(cfg));

    float contactTime = release + cfg.ContactDelay;
    for (float t = 0f; t < contactTime - 0.0001f; t += dt)
        ArcadeTennis.BallPhysics.BallSimulation.Advance(ref toss, ballCfg, null, dt);

    var contact = ArcadeTennis.Characters.SwingSolver.Evaluate(
        toss.Position, toss.Velocity, hitCentre, UnityEngine.Vector3.zero, cfg.Contact());

    float charge = UnityEngine.Mathf.Clamp01(release / cfg.ChargeTime);
    string result = contact.Made ? outcomeOf(charge, contact.Quality, 0f) : "-";
    string word = result == "n" ? "Netz" : result == "." ? "DRIN" : result == "o" ? "daneben" : "verfehlt";

    sb.AppendLine("   " + release.ToString("0.00") + " s"
        + "     " + toss.Position.y.ToString("0.00") + " m"
        + "    " + (toss.Position.y - hitCentre.y).ToString("+0.00;-0.00")
        + "    " + charge.ToString("0.00")
        + "     " + contact.Grade.ToString().PadRight(8)
        + "  " + word);
}

return sb.ToString();
