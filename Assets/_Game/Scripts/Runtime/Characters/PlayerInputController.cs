using UnityEngine;
using UnityEngine.InputSystem;

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
            character.SetMoveIntent(move);

            bool held = swingAction != null && swingAction.IsPressed();

            // One stick does both jobs, as it does in every arcade tennis game:
            // where you run is where you aim. Splitting them would need a second
            // stick the keyboard does not have.
            //
            // Both strokes are fed unconditionally; which of them is listening is
            // the serve's business, not the input's.
            if (swing != null)
            {
                swing.SetAim(move);
                swing.SetSwingHeld(held);
            }

            if (serve != null)
            {
                serve.SetAim(move);
                serve.SetSwingHeld(held);
            }
        }
    }
}
