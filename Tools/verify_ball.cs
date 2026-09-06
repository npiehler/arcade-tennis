// Milestone 2 verification: does the prediction the marker shows actually match
// where the ball comes down, and does the launch solver hit what it aims at?
var court = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Court.CourtDefinition>(
    "Assets/_Game/Settings/CourtDefinition.asset");
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.BallPhysics.BallPhysicsConfig>(
    "Assets/_Game/Settings/BallPhysicsConfig.asset");

float dt = UnityEngine.Time.fixedDeltaTime;
var sb = new System.Text.StringBuilder();
int failed = 0;

System.Action<string, bool> check = (label, ok) => {
    if (!ok) failed++;
    sb.AppendLine((ok ? "PASS  " : "FAIL  ") + label);
};

sb.AppendLine("fixedDeltaTime = " + dt.ToString("0.####"));

// --- prediction vs. reality --------------------------------------------
// Fire a spread of shots; for each, predict the landing, then run the same
// simulation forward and compare against the bounce that really happens.
var origin = new UnityEngine.Vector3(0f, 1.1f, -12.2f);
var targets = new UnityEngine.Vector3[] {
    new UnityEngine.Vector3(0f, 0f, 11.2f),
    new UnityEngine.Vector3(-3.3f, 0f, 4.5f),
    new UnityEngine.Vector3(5.7f, 0f, 7f),
    new UnityEngine.Vector3(2.0f, 0f, 1.5f),
    new UnityEngine.Vector3(-1.0f, 0f, 9.9f),
};

float worstPredictionError = 0f;
float worstAimError = 0f;

for (int i = 0; i < targets.Length; i++)
{
    var target = targets[i];
    var velocity = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
        origin, target, 2.2f, cfg, court, dt);

    var launchState = new ArcadeTennis.BallPhysics.BallState(origin, velocity);

    // What the marker would show.
    var prediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(launchState, cfg, court, dt);

    // What actually happens, stepping exactly like the live ball does.
    var live = launchState;
    var events = new System.Collections.Generic.List<ArcadeTennis.BallPhysics.BallEvent>();
    UnityEngine.Vector3 actual = UnityEngine.Vector3.zero;
    bool got = false;
    for (int step = 0; step < 2000 && !got; step++)
    {
        events.Clear();
        ArcadeTennis.BallPhysics.BallSimulation.Advance(ref live, cfg, court, dt, events);
        if (events.Count > 0) { actual = events[0].Position; got = true; }
    }

    float predictionError = UnityEngine.Vector3.Distance(prediction.Position, actual);
    worstPredictionError = UnityEngine.Mathf.Max(worstPredictionError, predictionError);

    float aimError = UnityEngine.Vector2.Distance(
        new UnityEngine.Vector2(actual.x, actual.z),
        new UnityEngine.Vector2(target.x, target.z));
    worstAimError = UnityEngine.Mathf.Max(worstAimError, aimError);

    check("shot " + i + ": prediction matches the real bounce (" + predictionError.ToString("0.0000") + " m)",
        got && predictionError < 0.0005f);
}

sb.AppendLine("worst prediction error = " + worstPredictionError.ToString("0.00000") + " m");
sb.AppendLine("worst aim error        = " + worstAimError.ToString("0.000") + " m");
check("launch solver lands within 5 cm of the requested target", worstAimError < 0.05f);

// --- in / out classification -------------------------------------------
var deepIn = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
    origin, new UnityEngine.Vector3(0f, 0f, 11.2f), 2.2f, cfg, court, dt);
var deepPrediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(
    new ArcadeTennis.BallPhysics.BallState(origin, deepIn), cfg, court, dt);
check("deep shot is called in", court.IsInBounds(deepPrediction.Position));

var wide = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
    origin, new UnityEngine.Vector3(5.7f, 0f, 7f), 2.2f, cfg, court, dt);
var widePrediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(
    new ArcadeTennis.BallPhysics.BallState(origin, wide), cfg, court, dt);
check("wide shot is called out", !court.IsInBounds(widePrediction.Position));

// --- net ----------------------------------------------------------------
// Aimed just past the net but launched flat and low, so it cannot clear the cord.
var netShot = new ArcadeTennis.BallPhysics.BallState(
    new UnityEngine.Vector3(0f, 0.5f, -6f), new UnityEngine.Vector3(0f, 0.6f, 18f));
var netPrediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(netShot, cfg, court, dt);
check("a low flat ball is reported as a net hit", netPrediction.IsNetHit);

// A high ball over the same spot must not register as a net hit.
var clearShot = new ArcadeTennis.BallPhysics.BallState(
    new UnityEngine.Vector3(0f, 1.6f, -6f), new UnityEngine.Vector3(0f, 3.5f, 14f));
var clearPrediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(clearShot, cfg, court, dt);
check("a ball clearing the cord is not a net hit", clearPrediction.IsBounce);

// --- no tunnelling at speed --------------------------------------------
var fast = new ArcadeTennis.BallPhysics.BallState(
    new UnityEngine.Vector3(0f, 0.6f, -11f), new UnityEngine.Vector3(0f, -2f, 60f));
var fastPrediction = ArcadeTennis.BallPhysics.BallSimulation.Predict(fast, cfg, court, dt);
check("a 60 m/s ball still registers its bounce", fastPrediction.HasResult && fastPrediction.Position.y > 0f);

// --- energy is lost, not gained ----------------------------------------
var bouncer = new ArcadeTennis.BallPhysics.BallState(
    new UnityEngine.Vector3(0f, 3f, -5f), UnityEngine.Vector3.zero);
var bounceEvents = new System.Collections.Generic.List<ArcadeTennis.BallPhysics.BallEvent>();
float firstApexAfterBounce = 0f;
bool bounced = false;
for (int step = 0; step < 1500; step++)
{
    bounceEvents.Clear();
    ArcadeTennis.BallPhysics.BallSimulation.Advance(ref bouncer, cfg, court, dt, bounceEvents);
    if (bounceEvents.Count > 0) bounced = true;
    if (bounced) firstApexAfterBounce = UnityEngine.Mathf.Max(firstApexAfterBounce, bouncer.Position.y);
    if (bouncer.AtRest) break;
}
check("a dropped ball bounces lower than it fell from", bounced && firstApexAfterBounce < 3f);
check("a dropped ball eventually comes to rest", bouncer.AtRest);

sb.AppendLine(failed == 0 ? "ALL CHECKS PASSED" : failed + " CHECK(S) FAILED");
return sb.ToString();
