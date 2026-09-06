using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcadeTennis.Characters
{
    /// <summary>
    /// Feeds a <see cref="TennisCharacter"/> from the Match action map. Keeping
    /// input in its own component means the AI can drive the very same body in
    /// milestone 7 by pushing intents the same way.
    /// </summary>
    [RequireComponent(typeof(TennisCharacter))]
    public class PlayerInputController : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMapName = "Match";
        [SerializeField] string moveActionName = "Move";

        TennisCharacter character;
        InputActionMap matchMap;
        InputAction moveAction;

        void Awake()
        {
            character = GetComponent<TennisCharacter>();

            if (controls == null)
            {
                Debug.LogError("PlayerInputController: no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            matchMap = controls.FindActionMap(actionMapName, true);
            moveAction = matchMap.FindAction(moveActionName, true);
        }

        void OnEnable() => matchMap?.Enable();
        void OnDisable() => matchMap?.Disable();

        void Update()
        {
            if (moveAction == null) return;
            character.SetMoveIntent(moveAction.ReadValue<Vector2>());
        }
    }
}
