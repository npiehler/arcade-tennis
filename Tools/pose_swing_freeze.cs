// Stops the clock, puts the ball on a plausible approach and hands the stretched
// charge time back. Counterpart to pose_swing.cs.
UnityEngine.Time.timeScale = 0f;

var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var field = typeof(ArcadeTennis.Characters.SwingConfig).GetField(
    "chargeTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
field.SetValue(cfg, 0.6f);

var playerGo = UnityEngine.GameObject.Find("Player");
var sw = playerGo.GetComponent<ArcadeTennis.Characters.SwingController>();
var chr = playerGo.GetComponent<ArcadeTennis.Characters.TennisCharacter>();
var ball = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.BallPhysics.Ball>();

// With the clock stopped the ball stays exactly where it is put.
ball.Launch(chr.HitCentre + new UnityEngine.Vector3(0f, 0.42f, 2.4f),
            new UnityEngine.Vector3(0f, -1.2f, -7.4f));

return "frozen phase=" + sw.Phase + " charge=" + sw.Charge.ToString("0.00")
     + " ball=" + ball.State.Position.ToString("0.00");
