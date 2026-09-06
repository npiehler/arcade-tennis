using System.Collections.Generic;
using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.BallPhysics
{
    /// <summary>
    /// The ball's motion, written once and used twice: the live ball advances
    /// through <see cref="Advance"/> every physics tick, and the landing marker
    /// and the AI look ahead by running the very same function on a copy of the
    /// state. There is no second, approximate model that could disagree with
    /// what the player sees.
    /// </summary>
    public static class BallSimulation
    {
        /// <summary>One integration sub-step. Semi-implicit Euler with quadratic drag.</summary>
        static void Integrate(ref BallState state, BallPhysicsConfig config, float dt)
        {
            Vector3 acceleration = Vector3.down * config.Gravity;

            float speed = state.Velocity.magnitude;
            if (speed > 0.0001f)
                acceleration -= config.DragFactor * speed * state.Velocity;

            state.Velocity += acceleration * dt;
            state.Position += state.Velocity * dt;
        }

        /// <summary>
        /// Advances one sub-step and resolves the first collision it runs into.
        /// Returns true when something happened worth reporting.
        /// </summary>
        static bool Step(ref BallState state, BallPhysicsConfig config, CourtDefinition court,
                         float dt, out BallEvent ballEvent)
        {
            ballEvent = default;

            if (state.AtRest) return false;

            BallState previous = state;
            Integrate(ref state, config, dt);

            // --- net ---------------------------------------------------------
            // Checked before the ground: a ball clipping the net always resolves
            // as a net hit, even if it would have touched down in the same step.
            if (court != null && previous.Position.z != 0f
                && Mathf.Sign(previous.Position.z) != Mathf.Sign(state.Position.z))
            {
                float t = Mathf.InverseLerp(previous.Position.z, state.Position.z, 0f);
                Vector3 crossing = Vector3.Lerp(previous.Position, state.Position, t);

                bool withinPosts = Mathf.Abs(crossing.x) <= court.NetHalfWidth;
                bool belowCord = crossing.y - config.Radius <= court.NetHeightAt(crossing.x);

                if (withinPosts && belowCord)
                {
                    state.Position = crossing;
                    state.Velocity = new Vector3(
                        state.Velocity.x * config.NetRestitution,
                        state.Velocity.y * config.NetRestitution,
                        -state.Velocity.z * config.NetRestitution);

                    ballEvent = new BallEvent { Type = BallEventType.NetHit, Position = crossing };
                    return true;
                }
            }

            // --- ground ------------------------------------------------------
            if (state.Position.y <= config.Radius && previous.Position.y > config.Radius)
            {
                float t = Mathf.InverseLerp(previous.Position.y, state.Position.y, config.Radius);
                Vector3 contact = Vector3.Lerp(previous.Position, state.Position, t);
                contact.y = config.Radius;

                state.Position = contact;
                state.Velocity = new Vector3(
                    state.Velocity.x * config.SurfaceFriction,
                    -state.Velocity.y * config.Restitution,
                    state.Velocity.z * config.SurfaceFriction);

                if (state.Velocity.magnitude < config.RestSpeed)
                {
                    state.Velocity = Vector3.zero;
                    state.AtRest = true;
                }

                ballEvent = new BallEvent { Type = BallEventType.Bounce, Position = contact };
                return true;
            }

            // Never let a ball sink through the surface.
            if (state.Position.y < config.Radius)
            {
                state.Position = new Vector3(state.Position.x, config.Radius, state.Position.z);
                if (Mathf.Abs(state.Velocity.y) < config.RestSpeed)
                {
                    state.Velocity = Vector3.zero;
                    state.AtRest = true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances one physics tick, splitting it into sub-steps so a fast ball
        /// cannot tunnel through the ground or the net. Events are appended in
        /// the order they occur.
        /// </summary>
        public static void Advance(ref BallState state, BallPhysicsConfig config, CourtDefinition court,
                                   float deltaTime, List<BallEvent> events = null)
        {
            if (state.AtRest) return;

            int subSteps = Mathf.Clamp(
                Mathf.CeilToInt(state.Velocity.magnitude * deltaTime / config.MaxStepDistance),
                1, config.MaxSubSteps);

            float subDelta = deltaTime / subSteps;

            for (int i = 0; i < subSteps; i++)
            {
                if (Step(ref state, config, court, subDelta, out BallEvent ballEvent))
                    events?.Add(ballEvent);

                if (state.AtRest) return;
            }
        }

        /// <summary>
        /// Runs the simulation forward from a copy of <paramref name="state"/> and
        /// reports the first bounce or net contact. <paramref name="deltaTime"/>
        /// must be the same tick length the live ball uses, otherwise the answer
        /// drifts from what actually happens.
        /// </summary>
        public static BallPrediction Predict(BallState state, BallPhysicsConfig config, CourtDefinition court,
                                             float deltaTime, float maxTime = 8f)
        {
            var events = new List<BallEvent>(2);
            float elapsed = 0f;

            while (elapsed < maxTime)
            {
                events.Clear();
                Advance(ref state, config, court, deltaTime, events);
                elapsed += deltaTime;

                if (events.Count > 0)
                {
                    BallEvent first = events[0];
                    return new BallPrediction
                    {
                        HasResult = true,
                        Type = first.Type,
                        Position = first.Position,
                        Time = elapsed,
                    };
                }

                if (state.AtRest) break;
            }

            return default;
        }

        /// <summary>
        /// Finds the launch velocity that carries the ball from <paramref name="from"/>
        /// to <paramref name="target"/>, peaking roughly <paramref name="apexHeight"/>
        /// above the launch point.
        ///
        /// Drag rules out a closed-form answer, so this starts from the drag-free
        /// parabola and corrects the horizontal speed against a simulated range a
        /// few times. Convergence is fast because the error is close to linear in
        /// the horizontal speed.
        /// </summary>
        public static Vector3 SolveLaunchVelocity(Vector3 from, Vector3 target, float apexHeight,
                                                  BallPhysicsConfig config, CourtDefinition court,
                                                  float deltaTime, int iterations = 6)
        {
            Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
            float distance = flat.magnitude;
            if (distance < 0.001f) return Vector3.up * Mathf.Sqrt(2f * config.Gravity * apexHeight);

            Vector3 direction = flat / distance;
            float g = config.Gravity;

            // Drag-free seed: rise to the apex, then fall to the target height.
            float apex = Mathf.Max(apexHeight, 0.05f);
            float rise = Mathf.Sqrt(2f * g * apex);
            float drop = Mathf.Max(from.y + apex - target.y, 0.05f);
            float flightTime = rise / g + Mathf.Sqrt(2f * drop / g);

            float verticalSpeed = rise;
            float horizontalSpeed = distance / flightTime;

            for (int i = 0; i < iterations; i++)
            {
                Vector3 velocity = direction * horizontalSpeed + Vector3.up * verticalSpeed;

                // Predict against a net-free court: the solver aims at a landing
                // spot, and whether the net gets in the way is a gameplay outcome
                // the caller decides on, not a reason to distort the aim.
                BallPrediction prediction = Predict(new BallState(from, velocity), config, null, deltaTime);
                if (!prediction.HasResult) break;

                Vector3 landedFlat = new Vector3(prediction.Position.x - from.x, 0f, prediction.Position.z - from.z);
                float landedDistance = Vector3.Dot(landedFlat, direction);
                if (landedDistance < 0.01f) break;

                float correction = distance / landedDistance;
                horizontalSpeed *= Mathf.Clamp(correction, 0.5f, 2f);

                if (Mathf.Abs(landedDistance - distance) < 0.01f) break;
            }

            return direction * horizontalSpeed + Vector3.up * verticalSpeed;
        }
    }
}
