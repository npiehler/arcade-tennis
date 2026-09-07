using UnityEngine;

namespace ArcadeTennis.Court
{
    /// <summary>
    /// The three places a shot can be sent, named from the player's own view of
    /// the screen so that "left" is left for whichever end is being played.
    /// </summary>
    public enum AimZone
    {
        Left = -1,
        Centre = 0,
        Right = 1,
    }

    /// <summary>
    /// Turns a chosen zone into a spot on the court, and back out into the
    /// rectangle a marker has to draw.
    ///
    /// Three discrete zones rather than a continuous stick because that is the
    /// whole point: the player can see exactly which of three places the ball is
    /// going, and a keyboard -- which only ever says left, nothing or right --
    /// can express it without loss.
    ///
    /// Pure geometry over <see cref="CourtDefinition"/>, so the marker on screen
    /// and the target the ball is actually sent to cannot disagree.
    /// </summary>
    public static class AimZones
    {
        /// <summary>Screen space to world space: a player on the far side sees x mirrored.</summary>
        static float Mirror(int side) => side < 0 ? 1f : -1f;

        static int Sign(int side) => side >= 0 ? 1 : -1;

        /// <summary>Half width of one rally lane. Three lanes span the singles court.</summary>
        public static float LaneHalfWidth(CourtDefinition court) =>
            court == null ? 1f : court.SinglesHalfWidth / 3f;

        /// <summary>Half width of one serve lane. Three lanes span the service box.</summary>
        public static float ServeLaneHalfWidth(CourtDefinition court) =>
            court == null ? 1f : court.SinglesHalfWidth / 6f;

        /// <summary>Centre of a rally lane on the X axis, in world space.</summary>
        public static float LaneCentre(CourtDefinition court, int side, AimZone zone)
        {
            if (court == null) return 0f;
            return (int)zone * Mirror(side) * (court.SinglesHalfWidth * 2f / 3f);
        }

        /// <summary>Where a rally shot into <paramref name="zone"/> is aimed.</summary>
        public static Vector3 GroundTarget(CourtDefinition court, int side, AimZone zone, float depth)
        {
            if (court == null) return Vector3.zero;
            return new Vector3(LaneCentre(court, side, zone), 0f, -Sign(side) * depth);
        }

        /// <summary>
        /// Centre of a serve lane. Anchored to the service box the server has to
        /// hit rather than to the court, so the middle zone really is the middle
        /// of that box -- and because the box a server faces is always the
        /// diagonal one, pushing right still means right on screen.
        /// </summary>
        public static float ServeLaneCentre(CourtDefinition court, int serverSide, bool deuceCourt,
                                            AimZone zone)
        {
            if (court == null) return 0f;

            Rect box = court.GetServiceBox(-Sign(serverSide), deuceCourt);
            float t = 0.5f + (int)zone * Mirror(serverSide) / 3f;
            return Mathf.Lerp(box.xMin, box.xMax, t);
        }

        /// <summary>Where a serve into <paramref name="zone"/> is aimed.</summary>
        public static Vector3 ServeTarget(CourtDefinition court, int serverSide, bool deuceCourt,
                                          AimZone zone, float depth)
        {
            if (court == null) return Vector3.zero;
            return new Vector3(ServeLaneCentre(court, serverSide, deuceCourt, zone), 0f,
                               -Sign(serverSide) * depth);
        }

        /// <summary>Moves the selection one lane, stopping at the outside ones.</summary>
        public static AimZone Step(AimZone zone, int direction)
        {
            if (direction == 0) return zone;
            return (AimZone)Mathf.Clamp((int)zone + (direction > 0 ? 1 : -1), -1, 1);
        }

        /// <summary>
        /// Reads a stick or key pair as a lane. The deadzone is the caller's to
        /// apply; anything past half deflection counts as a full lean, which is
        /// what makes a keyboard and a stick behave the same.
        /// </summary>
        public static AimZone FromInput(float x, AimZone current)
        {
            if (x <= -0.5f) return AimZone.Left;
            if (x >= 0.5f) return AimZone.Right;
            return current;
        }
    }
}
