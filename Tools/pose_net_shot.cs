// A flat drive that arrives at the cord below net height.
UnityEngine.Time.timeScale = 0f;
var ball = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.BallPhysics.Ball>();

var origin = new UnityEngine.Vector3(0.9f, 1.0f, -4.2f);
var velocity = new UnityEngine.Vector3(-0.4f, 0.5f, 14f);

var state = new ArcadeTennis.BallPhysics.BallState(origin, velocity);
for (int i = 0; i < 9; i++)
    ArcadeTennis.BallPhysics.BallSimulation.Advance(ref state, ball.Config, ball.CourtDefinition, 0.02f, null);

ball.Launch(state.Position, state.Velocity);
var p = ball.PredictLanding();
var marker = UnityEngine.GameObject.Find("Landing Marker");

return "ballPos=" + ball.transform.position.ToString("0.00")
     + " predictionType=" + p.Type
     + " at=" + p.Position.ToString("0.00")
     + " netHeightThere=" + ball.CourtDefinition.NetHeightAt(p.Position.x).ToString("0.000")
     + " markerPos=" + marker.transform.position.ToString("0.00");
