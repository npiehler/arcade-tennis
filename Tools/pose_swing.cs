// Poses the stroke for a screenshot instead of racing the game loop, the same
// way pose_shot.cs did for the ball in milestone 2. Run this, then the freeze
// half a second later.
//
// The charge time is stretched for the duration: a CLI round trip costs about a
// second of wall time, so at the real 0.6 s the bar is always already full by
// the time a second call can stop the clock. Restored by the freeze script.
UnityEngine.Time.timeScale = 1f;

var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<ArcadeTennis.Characters.SwingConfig>(
    "Assets/_Game/Settings/SwingConfig.asset");
var field = typeof(ArcadeTennis.Characters.SwingConfig).GetField(
    "chargeTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
field.SetValue(cfg, 1.5f);

var playerGo = UnityEngine.GameObject.Find("Player");
var input = playerGo.GetComponent<ArcadeTennis.Characters.PlayerInputController>();
if (input != null) input.enabled = false;

var feeder = UnityEngine.Object.FindFirstObjectByType<ArcadeTennis.DebugTools.BallFeeder>();
if (feeder != null) feeder.enabled = false;

var chr = playerGo.GetComponent<ArcadeTennis.Characters.TennisCharacter>();
var sw = playerGo.GetComponent<ArcadeTennis.Characters.SwingController>();

chr.SetMoveIntent(UnityEngine.Vector2.zero);
sw.SetAim(UnityEngine.Vector2.zero);
sw.SetSwingHeld(true);

return "charging, chargeTime stretched to 1.5s";
