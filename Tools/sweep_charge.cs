// Tuning aid for the charge. Walks the hold from nothing to full and reports
// what the ball actually does, through the real SwingSolver and the real
// BallSimulation -- no second model that could flatter the numbers.
//
// Run it after touching anything in SwingConfig.asset:
//   unity cmd eval_file --file "Tools/sweep_charge.cs" --timeout 120000 --json
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var charCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.CharacterConfig>(
    "Assets/_Game/Settings/CharacterConfig.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var ballCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = 0.02f;
var sb = new System.Text.StringBuilder();

sb.AppendLine("charge " + cfg.ChargeTime.ToString("0.00") + "s   depth "
    + cfg.MinTargetDepth.ToString("0.00") + " .. "
    + (court.HalfLength + cfg.BaselineOvershoot).ToString("0.00")
    + "   apex " + cfg.ApexWeak.ToString("0.00") + " .. " + cfg.ApexFull.ToString("0.00")
    + "   minQualityPower " + cfg.MinQualityPower.ToString("0.00"));
sb.AppendLine();

System.Func<UnityEngine.Vector3, float, float, string> band =
    (contact, power, quality) =>
    {
        var judged = new ArcadeTennis.Characters.SwingContact { Made = true, Quality = quality };
        var target = ArcadeTennis.Characters.SwingSolver.ResolveTarget(
            -1, power, UnityEngine.Vector2.zero, judged, cfg, court, UnityEngine.Vector2.zero);
        float apex = ArcadeTennis.Characters.SwingSolver.ResolveApex(power, judged, contact, cfg);

        var v = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
            contact, target, apex, ballCfg, court, dt);
        var p = ArcadeTennis.BallPhysics.BallSimulation.Predict(
            new ArcadeTennis.BallPhysics.BallState(contact, v), ballCfg, court, dt);

        if (!p.HasResult) return "?";
        if (p.IsNetHit) return "n";
        return court.IsInBounds(p.Position) && p.Position.z > 0f ? "." : "o";
    };

System.Action<string, float> row = (label, quality) =>
{
    foreach (float cz in new float[] { -6.0f, -11.0f, -13.5f })
    {
        var contact = new UnityEngine.Vector3(0f, charCfg.HitHeight, cz);
        var line = new System.Text.StringBuilder();
        float firstIn = -1f, lastIn = -1f;

        for (float power = 0f; power <= 1.0001f; power += 0.05f)
        {
            string b = band(contact, power, quality);
            line.Append(b);
            if (b == ".")
            {
                if (firstIn < 0f) firstIn = power;
                lastIn = power;
            }
        }

        sb.AppendLine("  " + label.PadRight(18) + "z=" + cz.ToString("0.0").PadLeft(6) + "  " + line
            + "   drin " + (firstIn < 0f ? "-" : firstIn.ToString("0.00"))
            + " .. " + (lastIn < 0f ? "-" : lastIn.ToString("0.00"))
            + "  = " + (firstIn < 0f ? "-" : (firstIn * cfg.ChargeTime).ToString("0.00") + "s")
            + " .. " + (lastIn < 0f ? "-" : (lastIn * cfg.ChargeTime).ToString("0.00") + "s"));
    }
};

sb.AppendLine("Aufladung 0.00 -> 1.00 in 0.05-Schritten   n = Netz, . = drin, o = Aus");
sb.AppendLine();
row("Perfect", 1.0f);
sb.AppendLine();
row("Gut", 0.6f);
sb.AppendLine();
row("gerade getroffen", 0.0f);

return sb.ToString();
