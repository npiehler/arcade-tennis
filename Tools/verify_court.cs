// Sanity check for CourtDefinition + generated geometry. Runs via eval_file.
var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var sb = new System.Text.StringBuilder();
int failed = 0;

System.Action<string, bool> check = (label, ok) => {
    if (!ok) failed++;
    sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
};

// --- bounds -------------------------------------------------------------
check("baseline corner is in (singles)",
    def.IsInBounds(new UnityEngine.Vector3(4.11f, 0f, 11.88f), false));
check("doubles alley is out for singles",
    !def.IsInBounds(new UnityEngine.Vector3(4.8f, 0f, 0f), false));
check("doubles alley is in for doubles",
    def.IsInBounds(new UnityEngine.Vector3(4.8f, 0f, 0f), true));
check("long ball is out",
    !def.IsInBounds(new UnityEngine.Vector3(0f, 0f, 12.2f), false));
check("line ball counts as in",
    def.IsInBounds(new UnityEngine.Vector3(0f, 0f, 11.9f), false));

// --- service boxes ------------------------------------------------------
var deuceFar = def.GetServiceBox(1, true);
check("deuce box on side +1 spans x -4.115..0",
    UnityEngine.Mathf.Approximately(deuceFar.xMin, -def.SinglesHalfWidth)
    && UnityEngine.Mathf.Approximately(deuceFar.xMax, 0f));
check("deuce box on side +1 spans z 0..6.40",
    UnityEngine.Mathf.Approximately(deuceFar.yMin, 0f)
    && UnityEngine.Mathf.Approximately(deuceFar.yMax, def.ServiceLineDistance));

// The serve must travel diagonally: server's x sign is opposite the target box.
var serverPos = def.GetServePosition(-1, true);
check("deuce serve runs diagonally across the centre line",
    serverPos.x > 0f && deuceFar.center.x < 0f);

check("serve landing in the deuce box is good",
    def.IsInServiceBox(new UnityEngine.Vector3(-2f, 0f, 3f), 1, true));
check("same landing is a fault for the ad box",
    !def.IsInServiceBox(new UnityEngine.Vector3(-2f, 0f, 3f), 1, false));
check("serve past the service line is a fault",
    !def.IsInServiceBox(new UnityEngine.Vector3(-2f, 0f, 7.5f), 1, true));
check("serve into the wrong half is a fault",
    !def.IsInServiceBox(new UnityEngine.Vector3(-2f, 0f, -3f), 1, true));

// --- net ----------------------------------------------------------------
check("net is 0.914 m at the centre",
    UnityEngine.Mathf.Abs(def.NetHeightAt(0f) - 0.914f) < 0.001f);
check("net is 1.07 m at the post",
    UnityEngine.Mathf.Abs(def.NetHeightAt(def.NetHalfWidth) - 1.07f) < 0.001f);
check("net rises from centre to post",
    def.NetHeightAt(2f) > def.NetHeightAt(0f) && def.NetHeightAt(5f) > def.NetHeightAt(2f));

// --- generated geometry matches the numbers -----------------------------
var builder = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.Court.CourtBuilder>();
var lines = builder.transform.Find("_Generated/Lines");
var serviceLine = lines.Find("ServiceLine+");
check("generated service line sits at z = 6.40",
    UnityEngine.Mathf.Abs(serviceLine.localPosition.z - def.ServiceLineDistance) < 0.001f);
var baseline = lines.Find("Baseline-");
check("generated baseline sits at z = -11.885",
    UnityEngine.Mathf.Abs(baseline.localPosition.z + def.HalfLength) < 0.001f);
check("no colliders on generated parts",
    builder.GetComponentsInChildren<UnityEngine.Collider>().Length == 0);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
