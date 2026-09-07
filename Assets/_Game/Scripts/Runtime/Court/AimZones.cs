using UnityEngine;

namespace ArcadeTennis.Court
{
    /// <summary>
    /// The three places a shot can be sent, named from the player's own view of
    /// the screen so that "left" is left for whichever end is being played.
    ///
    /// Not three lanes across but a deep one and two short ones, which is what
    /// gives a shot both a length and a side to choose without a second control
    /// for power. The split follows the service line, so the zones sit on
    /// markings the court already has.
    /// </summary>
    public enum AimZone
    {
        /// <summary>Short and to the player's left. Arrow left.</summary>
        ShortLeft = -1,

        /// <summary>Deep, down the middle. Arrow up, and what an untouched stick means.</summary>
        Deep = 0,

        /// <summary>Short and to the player's right. Arrow right.</summary>
        ShortRight = 1,
    }

    /// <summary>
    /// Turns a chosen zone into a spot on the court and into the patch of ground
    /// a marker has to draw.
    ///
    /// Pure geometry over <see cref="CourtDefinition"/>, so the marker on screen
    /// and the target the ball is sent to cannot drift apart: both read these
    /// same functions.
    ///
    /// Everything here works in "depth" -- distance from the net towards the
    /// receiver -- and only converts to a world z at the very end, which keeps
    /// the sign handling in one place instead of scattered through the callers.
    /// </summary>
    public static class AimZones
    {
        /// <summary>Screen space to world space: a player on the far side sees x mirrored.</summary>
        static float Mirror(int side) => side < 0 ? 1f : -1f;

        static int Sign(int side) => side >= 0 ? 1 : -1;

        // --- rally -----------------------------------------------------------

        /// <summary>Centre of a rally zone as (x, depth from the net).</summary>
        public static Vector2 GroundZoneCentre(CourtDefinition court, int side, AimZone zone)
        {
            if (court == null) return Vector2.zero;

            float serviceLine = court.ServiceLineDistance;
            float baseline = court.HalfLength;

            if (zone == AimZone.Deep)
                return new Vector2(0f, (serviceLine + baseline) * 0.5f);

            float lateral = court.SinglesHalfWidth * 0.5f * (int)zone * Mirror(side);
            return new Vector2(lateral, serviceLine * 0.58f);
        }

        /// <summary>Half width and half depth of a rally zone.</summary>
        public static Vector2 GroundZoneExtents(CourtDefinition court, AimZone zone)
        {
            if (court == null) return Vector2.one;

            if (zone == AimZone.Deep)
                return new Vector2(court.SinglesHalfWidth * 0.62f,
                    (court.HalfLength - court.ServiceLineDistance) * 0.5f);

            return new Vector2(court.SinglesHalfWidth * 0.5f, court.ServiceLineDistance * 0.42f);
        }

        // --- serve -----------------------------------------------------------

        /// <summary>Centre of a serve zone as (x, depth from the net), inside the required box.</summary>
        public static Vector2 ServeZoneCentre(CourtDefinition court, int serverSide, bool deuceCourt,
                                              AimZone zone)
        {
            if (court == null) return Vector2.zero;

            Rect box = court.GetServiceBox(-Sign(serverSide), deuceCourt);
            float boxCentre = box.center.x;
            float boxHalf = box.width * 0.5f;

            if (zone == AimZone.Deep)
                return new Vector2(boxCentre, court.ServiceLineDistance * 0.80f);

            float lateral = boxCentre + boxHalf * 0.5f * (int)zone * Mirror(serverSide);
            return new Vector2(lateral, court.ServiceLineDistance * 0.45f);
        }

        /// <summary>Half width and half depth of a serve zone.</summary>
        public static Vector2 ServeZoneExtents(CourtDefinition court, AimZone zone)
        {
            if (court == null) return Vector2.one;

            Rect box = court.GetServiceBox(1, true);
            float boxHalf = box.width * 0.5f;

            if (zone == AimZone.Deep)
                return new Vector2(boxHalf * 0.85f, court.ServiceLineDistance * 0.18f);

            return new Vector2(boxHalf * 0.5f, court.ServiceLineDistance * 0.26f);
        }

        // --- shared ----------------------------------------------------------

        /// <summary>Turns a zone centre and a depth into a world point on the far side.</summary>
        public static Vector3 ToWorld(int side, float x, float depth) =>
            new Vector3(x, 0f, -Sign(side) * depth);

        /// <summary>
        /// Reads the direction keys as a zone while the swing button is held.
        /// Up asks for the deep zone, left and right for the short ones; the
        /// stronger axis wins so a diagonal cannot mean two things at once.
        /// </summary>
        public static AimZone FromInput(Vector2 input, AimZone fallback)
        {
            if (Mathf.Abs(input.y) > Mathf.Abs(input.x))
                return input.y >= 0.5f ? AimZone.Deep : fallback;

            if (input.x <= -0.5f) return AimZone.ShortLeft;
            if (input.x >= 0.5f) return AimZone.ShortRight;
            return fallback;
        }
    }
}
