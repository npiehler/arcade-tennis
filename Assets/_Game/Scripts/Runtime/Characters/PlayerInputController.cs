using UnityEngine;
using UnityEngine.InputSystem;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Feeds a <see cref="TennisCharacter"/> and its <see cref="SwingController"/>
    /// from the Match action map. Keeping input in its own component means the AI
    /// can drive the very same body in milestone 7 by pushing intents the same way.
    /// </summary>
    [RequireComponent(typeof(TennisCharacter))]
    public class PlayerInputController : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMapName = "Match";
        [SerializeField] string moveActionName = "Move";
        [SerializeField] string swingActionName = "Swing";
        [SerializeField] string aimActionName = "Aim";

        TennisCharacter character;
        SwingController swing;
        ServeController serve;
        InputActionMap matchMap;
        InputAction moveAction;
        InputAction swingAction;
        InputAction aimAction;

        void Awake()
        {
            character = GetComponent<TennisCharacter>();
            swing = GetComponent<SwingController>();
            serve = GetComponent<ServeController>();

            if (controls == null)
            {
                Debug.LogError("PlayerInputController: no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            matchMap = controls.FindActionMap(actionMapName, true);
            moveAction = matchMap.FindAction(moveActionName, true);
            swingAction = matchMap.FindAction(swingActionName, true);
            aimAction = matchMap.FindAction(aimActionName, true);
        }

        void OnEnable() => matchMap?.Enable();
        void OnDisable() => matchMap?.Disable();

        void Update()
        {
            if (moveAction == null) return;

            Vector2 move = moveAction.ReadValue<Vector2>();
            character.SetMoveIntent(move);

            bool held = swingAction != null && swingAction.IsPressed();

            // Aiming has its own keys rather than sharing the ones that run.
            // Sharing them meant the direction you ran to reach the ball was the
            // direction you hit it, and correcting that during the swing braked
            // the run. The arrow keys used to be a second copy of WASD, so this
            // costs nothing.
            Vector2 aim = aimAction != null ? aimAction.ReadValue<Vector2>() : Vector2.zero;
            AimZone zone = AimZones.FromInput(aim.x, AimZone.Centre);

            // Both strokes are fed unconditionally; which of them is listening is
            // the serve's business, not the input's.
            if (swing != null)
            {
                swing.SetZone(zone);
                swing.SetSwingHeld(held);
            }

            if (serve != null)
            {
                serve.SetZone(zone);
                serve.SetSwingHeld(held);
            }
        }
    }
}
