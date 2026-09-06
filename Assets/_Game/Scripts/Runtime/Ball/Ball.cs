using System;
using System.Collections.Generic;
using UnityEngine;
using ArcadeTennis.Court;

namespace ArcadeTennis.BallPhysics
{
    /// <summary>
    /// The ball in the scene. It owns a <see cref="BallState"/> and hands it to
    /// <see cref="BallSimulation"/> every physics tick; the transform is only a
    /// view of that state.
    /// </summary>
    public class Ball : MonoBehaviour
    {
        [SerializeField] BallPhysicsConfig config;
        [SerializeField] CourtDefinition court;

        [Tooltip("Renders the ball larger than it physically is. A regulation ball is " +
                 "66 mm across and nearly invisible from the baseline camera; the " +
                 "simulation keeps using the real radius, only the mesh grows.")]
        [SerializeField] float visualScale = 2.6f;

        readonly List<BallEvent> stepEvents = new List<BallEvent>(4);

        BallState state;

        /// <summary>Fired for every ground contact, at the exact contact point.</summary>
        public event Action<Vector3> Bounced;

        /// <summary>Fired when the ball fails to clear the net.</summary>
        public event Action<Vector3> HitNet;

        /// <summary>Fired whenever the ball is launched, after the new state is set.</summary>
        public event Action Launched;

        public BallPhysicsConfig Config => config;
        public CourtDefinition CourtDefinition => court;
        public BallState State => state;
        public Vector3 Velocity => state.Velocity;
        public bool IsMoving => !state.AtRest;

        /// <summary>Bounces since the last launch. The rules need this to spot a double bounce.</summary>
        public int BounceCount { get; private set; }

        void Awake()
        {
            state = new BallState(transform.position, Vector3.zero) { AtRest = true };
            ApplyRadius();
        }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            ApplyRadius();
        }

        void ApplyRadius()
        {
            if (config == null) return;
            transform.localScale = Vector3.one * config.Radius * 2f * Mathf.Max(visualScale, 1f);
        }

        void FixedUpdate()
        {
            if (config == null || state.AtRest) return;

            stepEvents.Clear();
            BallSimulation.Advance(ref state, config, court, Time.fixedDeltaTime, stepEvents);
            transform.position = state.Position;

            // Raised after the state is settled so handlers always see a
            // consistent ball, never a half-resolved one.
            for (int i = 0; i < stepEvents.Count; i++)
            {
                BallEvent e = stepEvents[i];

                if (e.Type == BallEventType.Bounce)
                {
                    BounceCount++;
                    Bounced?.Invoke(e.Position);
                }
                else if (e.Type == BallEventType.NetHit)
                {
                    HitNet?.Invoke(e.Position);
                }
            }
        }

        /// <summary>Puts the ball at a position with a velocity and makes it live.</summary>
        public void Launch(Vector3 position, Vector3 velocity)
        {
            state = new BallState(position, velocity);
            BounceCount = 0;
            transform.position = position;
            Launched?.Invoke();
        }

        /// <summary>
        /// Launches at a spot on the court, arcing <paramref name="apexHeight"/>
        /// above the launch point.
        /// </summary>
        public void LaunchAt(Vector3 position, Vector3 target, float apexHeight)
        {
            Vector3 velocity = BallSimulation.SolveLaunchVelocity(
                position, target, apexHeight, config, court, Time.fixedDeltaTime);

            Launch(position, velocity);
        }

        /// <summary>Where this ball will next touch down, using the live state.</summary>
        public BallPrediction PredictLanding(float maxTime = 8f)
        {
            if (config == null || state.AtRest) return default;
            return BallSimulation.Predict(state, config, court, Time.fixedDeltaTime, maxTime);
        }

        public void Stop()
        {
            state.Velocity = Vector3.zero;
            state.AtRest = true;
        }
    }
}
