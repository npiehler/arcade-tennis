// Tuning aid for both strokes. Walks the contact quality from nothing to
// flawless and reports what the ball actually does, through the real solvers
// and the real BallSimulation -- no second model that could flatter the numbers.
//
//   unity cmd eval_file --file "Tools/sweep_shots.cs" --timeout 300000 --json
//
// Since the charge was removed, quality is the only thing that decides depth,
// so this table is the whole difficulty curve of the game in one place.
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var charCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.CharacterConfig>(
    "Assets/_Game/Settings/CharacterConfig.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var srv = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.ServeConfig>(
    "Assets/_Game/Settings/ServeConfig.asset");
var ballCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = 0.02f;
var sb = new System.Text.StringBuilder();
var zones = new ArcadeTennis.Court.AimZone[] {
    ArcadeTennis.Court.AimZone.ShortLeft,
    ArcadeTennis.Court.AimZone.Deep,
    ArcadeTennis.Court.AimZone.ShortRight };

sb.AppendLine("Trefferqualitaet 0.00 -> 1.00 in 0.05-Schritten   n = Netz, . = drin, o = daneben");
sb.AppendLine();
sb.AppendLine("GRUNDSCHLAG   Rueckstand bei schlechtem Kontakt " + cfg.DepthShortfall.ToString("0.0")
    + " m   Bogen " + cfg.ApexWeak.ToString("0.00") + " .. " + cfg.ApexFull.ToString("0.00")
    + "   Streuung " + cfg.MaxSpread.ToString("0.0") + " .. " + cfg.MinSpread.ToString("0.00")
    + " (seitlich " + (cfg.LateralSpreadFactor * 100f).ToString("0") + " %)");
foreach (var z in zones)
    sb.AppendLine("    Zone " + z.ToString().PadRight(11)
        + "Mitte " + ArcadeTennis.Court.AimZones.GroundZoneCentre(court, -1, z).ToString("0.00")
        + "  Ausdehnung " + ArcadeTennis.Court.AimZones.GroundZoneExtents(court, z).ToString("0.00"));

foreach (float cz in new float[] { -6f, -11f, -13.5f })
foreach (var zone in zones)
{
    var from = new UnityEngine.Vector3(0f, charCfg.HitHeight, cz);
    var line = new System.Text.StringBuilder();
    for (float q = 0f; q <= 1.0001f; q += 0.05f)
    {
        var c = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = q };
        var t = ArcadeTennis.Characters.SwingSolver.ResolveTarget(-1, zone, c, cfg, court,
            UnityEngine.Vector2.zero);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(from, t,
            ArcadeTennis.Characters.SwingSolver.ResolveApex(zone, c, from, cfg), ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);
        line.Append(!p.HasResult ? "?" : p.IsNetHit ? "n"
            : (court.IsInBounds(p.Position) && p.Position.z > 0f ? "." : "o"));
    }
    sb.AppendLine("  z=" + cz.ToString("0.0").PadLeft(6) + "  " + zone.ToString().PadRight(11) + line);
}

sb.AppendLine();
sb.AppendLine("AUFSCHLAG     Rueckstand " + srv.DepthShortfall.ToString("0.0")
    + " m   Bogen " + srv.ApexWeak.ToString("0.00") + " .. " + srv.ApexFull.ToString("0.00")
    + "   Aufschlaglinie bei " + court.ServiceLineDistance + " m");
foreach (var z in zones)
    sb.AppendLine("    Zone " + z.ToString().PadRight(11)
        + "Mitte " + ArcadeTennis.Court.AimZones.ServeZoneCentre(court, -1, true, z).ToString("0.00"));

foreach (bool deuce in new bool[] { true, false })
foreach (var zone in zones)
{
    var from = ArcadeTennis.Characters.ServeSolver.HitCentre(
        court.GetServePosition(-1, deuce), -1, srv);
    var line = new System.Text.StringBuilder();
    for (float q = 0f; q <= 1.0001f; q += 0.05f)
    {
        var c = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = q };
        var t = ArcadeTennis.Characters.ServeSolver.ResolveTarget(-1, deuce, zone, c, srv, court,
            UnityEngine.Vector2.zero);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(from, t,
            ArcadeTennis.Characters.ServeSolver.ResolveApex(zone, c, srv), ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(from, v), ballCfg, court, dt);
        line.Append(!p.HasResult ? "?" : p.IsNetHit ? "n"
            : (court.IsInServiceBox(p.Position, 1, deuce) ? "." : "o"));
    }
    sb.AppendLine("  " + (deuce ? "Einstand" : "Vorteil ") + "  " + zone.ToString().PadRight(11) + line);
}

// Der Wurfrhythmus: wann der zweite Tastendruck kommt, entscheidet die Qualitaet.
sb.AppendLine();
sb.AppendLine("WURFRHYTHMUS  Kontakt " + srv.ContactDelay.ToString("0.00") + " s nach dem zweiten Druck");
sb.AppendLine("  2. Druck   Ballhoehe  Note      Ergebnis");

var standDeuce = court.GetServePosition(-1, true);
var tossOrigin = ArcadeTennis.Characters.ServeSolver.TossOrigin(standDeuce, -1, srv);
var hitCentre = ArcadeTennis.Characters.ServeSolver.HitCentre(standDeuce, -1, srv);

for (float strike = 0f; strike <= 1.05f; strike += 0.05f)
{
    var toss = new ArcadeTennis.BallPhysics.BallState(
        tossOrigin, ArcadeTennis.Characters.ServeSolver.TossVelocity(srv));
    for (float t = 0f; t < strike + srv.ContactDelay - 0.0001f; t += dt)
        ArcadeTennis.BallPhysics.BallSimulation.Advance(ref toss, ballCfg, null, dt);

    var contact = ArcadeTennis.Characters.SwingSolver.Evaluate(
        toss.Position, toss.Velocity, hitCentre, UnityEngine.Vector3.zero, srv.Contact());

    string word = "verfehlt";
    if (contact.Made)
    {
        var t2 = ArcadeTennis.Characters.ServeSolver.ResolveTarget(-1, true,
            ArcadeTennis.Court.AimZone.Deep, contact, srv, court, UnityEngine.Vector2.zero);
        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(hitCentre, t2,
            ArcadeTennis.Characters.ServeSolver.ResolveApex(ArcadeTennis.Court.AimZone.Deep, contact, srv), ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(hitCentre, v), ballCfg, court, dt);
        word = !p.HasResult ? "?" : p.IsNetHit ? "Netz"
            : (court.IsInServiceBox(p.Position, 1, true) ? "DRIN" : "daneben");
    }

    sb.AppendLine("   " + strike.ToString("0.00") + " s"
        + "     " + toss.Position.y.ToString("0.00") + " m"
        + "    " + contact.Grade.ToString().PadRight(8) + "  " + word);
}

return sb.ToString();
