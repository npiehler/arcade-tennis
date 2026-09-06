using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.Characters
{
    /// <summary>Position and velocity of a moving character, separated from the scene object.</summary>
    public struct CharacterMotionState
    {
        public Vector3 Position;
        public Vector3 Velocity;

        public CharacterMotionState(Vector3 position)
        {
            Position = position;
            Velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Movement as a pure function, for the same reason the ball has one: the
    /// live character and anything that needs to reason about where a character
    /// can get to run identical code, and the rules can be tested without
    /// entering play mode.
    /// </summary>
    public static class CharacterMotion
    {
        /// <summary>
        /// Turns stick input into a world direction. Input is read in screen
        /// space -- x right, y towards the net -- and both axes flip for the far
        /// side, so each player steers the same way from their own camera.
        /// </summary>
        public static Vector3 ToWorldIntent(Vector2 intent, int side, CharacterConfig config)
        {
            if (config == null) return Vector3.zero;
            if (intent.magnitude < config.InputDeadzone) return Vector3.zero;

            float facing = side < 0 ? 1f : -1f;
            Vector3 world = new Vector3(intent.x, 0f, intent.y) * facing;
            return Vector3.ClampMagnitude(world, 1f);
        }

        /// <summary>Advances one tick: accelerate, move, then keep inside the play area.</summary>
        public static void Step(ref CharacterMotionState state, Vector2 intent, int side,
                                CharacterConfig config, CourtDefinition court, float deltaTime)
        {
            if (config == null) return;

            Vector3 desired = ToWorldIntent(intent, side, config) * config.MaxSpeed;

            // Stopping is snappier than starting, which is what makes the
            // character feel planted rather than skating.
            float rate = desired.sqrMagnitude > 0.0001f ? config.Acceleration : config.Deceleration;
            state.Velocity = Vector3.MoveTowards(state.Velocity, desired, rate * deltaTime);

            Vector3 next = state.Position + state.Velocity * deltaTime;
            next.y = 0f;

            if (court != null)
            {
                // Own half only: a player may crowd the net but never cross it.
                Vector3 clamped = court.ClampToOwnHalf(next, side);

                // Drop the velocity component that ran into the boundary instead
                // of letting the character press into it and slide along.
                if (!Mathf.Approximately(clamped.x, next.x)) state.Velocity.x = 0f;
                if (!Mathf.Approximately(clamped.z, next.z)) state.Velocity.z = 0f;

                next = clamped;
            }

            state.Position = next;
        }
    }
}
