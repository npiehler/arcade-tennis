// Freezes time and poses the ball mid-flight so a screenshot is reproducible
// instead of racing the game loop. SHOT_INDEX / STEPS are patched in by the caller.
UnityEngine.Time.timeScale = 0f;

var launcher = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.DebugTools.BallTestLauncher>();
var ball = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.BallPhysics.Ball>();

var origin = new UnityEngine.Vector3(0f, 1.1f, -12.2f);
var target = launcher.GetTarget(SHOT_INDEX);

var velocity = ArcadeTennis.BallPhysics.BallSimulation.SolveLaunchVelocity(
    origin, target, 2.2f, ball.Config, ball.CourtDefinition, 0.02f);

// Walk the ball forward to mid-flight with the same simulation it uses live.
var state = new ArcadeTennis.BallPhysics.BallState(origin, velocity);
for (int i = 0; i < STEPS; i++)
    ArcadeTennis.BallPhysics.BallSimulation.Advance(ref state, ball.Config, ball.CourtDefinition, 0.02f, null);

ball.Launch(state.Position, state.Velocity);

var prediction = ball.PredictLanding();
var marker = UnityEngine.GameObject.Find("Landing Marker");

return "target=" + target
     + " ballPos=" + ball.transform.position.ToString("0.00")
     + " predicted=" + prediction.Position.ToString("0.00")
     + " inBounds=" + ball.CourtDefinition.IsInBounds(prediction.Position)
     + " markerActive=" + (marker != null && marker.activeInHierarchy)
     + " markerPos=" + (marker != null ? marker.transform.position.ToString("0.00") : "-");
