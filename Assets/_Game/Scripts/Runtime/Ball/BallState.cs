using UnityEngine;

namespace ArcadeTennis.BallPhysics
{
    /// <summary>Everything the simulation needs. A struct so it can be copied and
    /// simulated ahead of time without touching the ball in the scene.</summary>
    public struct BallState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public bool AtRest;

        public BallState(Vector3 position, Vector3 velocity)
        {
            Position = position;
            Velocity = velocity;
            AtRest = false;
        }
    }

    public enum BallEventType
    {
        None,
        Bounce,
        NetHit,
    }

    public struct BallEvent
    {
        public BallEventType Type;
        public Vector3 Position;
    }

    /// <summary>Result of looking ahead at where a ball will come down.</summary>
    public struct BallPrediction
    {
        /// <summary>False when the ball neither bounces nor hits the net within the horizon.</summary>
        public bool HasResult;
        public BallEventType Type;
        public Vector3 Position;
        public float Time;

        public bool IsBounce => HasResult && Type == BallEventType.Bounce;
        public bool IsNetHit => HasResult && Type == BallEventType.NetHit;
    }
}
