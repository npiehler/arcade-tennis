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

        TennisCharacter character;
        SwingController swing;
        ServeController serve;
        InputActionMap matchMap;
        InputAction moveAction;
        InputAction swingAction;

        AimZone zone = AimZone.Deep;
        bool wasHeld;

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
        }

        void OnEnable() => matchMap?.Enable();
        void OnDisable() => matchMap?.Disable();

        void Update()
        {
            if (moveAction == null) return;

            Vector2 move = moveAction.ReadValue<Vector2>();
            bool held = swingAction != null && swingAction.IsPressed();

            // The direction keys do one job at a time. Free, they steer. With the
            // swing button down they choose the target zone instead, and the
            // character plants its feet -- which is how a stroke is actually
            // played, and what stops the direction you ran from deciding where
            // the ball goes.
            if (held)
            {
                character.SetMoveIntent(Vector2.zero);

                // Every fresh press starts from the deep zone, so no shot is ever
                // sent somewhere left over from the last one.
                if (!wasHeld) zone = AimZone.Deep;
                zone = AimZones.FromInput(move, zone);
            }
            else
            {
                character.SetMoveIntent(move);
            }

            wasHeld = held;

            // The zone is only written while aiming. Contact lands after the
            // release, so writing it afterwards would overwrite the choice before
            // the racket ever got to use it.
            if (swing != null)
            {
                if (held) swing.SetZone(zone);
                swing.SetSwingHeld(held);
            }

            if (serve != null)
            {
                if (held) serve.SetZone(zone);
                serve.SetSwingHeld(held);
            }
        }
    }
}
