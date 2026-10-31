using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// The arrival reveal's camera move: starts looking over the first reveal point, glides across
    /// the rest, then settles back onto the fitted gameplay pose, eased in and out as one move. The
    /// fitted pose is captured when the pan begins and written back exactly at the end. If anything
    /// else moves the camera meanwhile (the controller refits it after a screen resize), the pan
    /// stops at once and leaves that pose alone. Stepped from LateUpdate with no allocations.
    /// </summary>
    public sealed class CameraPan
    {
        /// <summary>Pitch while looking over the scenery: shallower than gameplay, so the back band reads.</summary>
        const float RevealPitch = 30f;
        /// <summary>Yaw swing across the points, for a little parallax.</summary>
        const float RevealYaw = 7f;
        /// <summary>Viewing distance as a fraction of the fitted camera's distance to the board.</summary>
        const float DistanceFactor = 0.55f;
        const float MinDistance = 7f;
        const float MaxDistance = 15f;
        const float Tolerance = 1e-4f;

        Camera cam;
        Vector3 homePosition;
        Quaternion homeRotation;
        Vector3[] positions = new Vector3[0];
        Quaternion[] rotations = new Quaternion[0];
        float[] cumulative = new float[0];
        int count;
        float start, duration;
        Vector3 lastPosition;
        Quaternion lastRotation;
        bool active;

        public bool Active => active;

        /// <summary>
        /// Starts a pan now over <paramref name="points"/> (left to right) that ends back on the
        /// camera's current (fitted) pose after <paramref name="seconds"/>.
        /// </summary>
        /// <returns>False (and nothing moves) when there are no points.</returns>
        public bool Begin(Camera camera, Vector3[] points, Vector3 focus, float seconds)
        {
            Cancel(false);
            if (camera == null || points == null || points.Length == 0 || seconds <= 0f) return false;

            cam = camera;
            var tr = cam.transform;
            homePosition = tr.position;
            homeRotation = tr.rotation;
            float distance = Mathf.Clamp(Vector3.Distance(homePosition, focus) * DistanceFactor, MinDistance, MaxDistance);

            count = points.Length + 1;
            if (positions.Length < count)
            {
                positions = new Vector3[count];
                rotations = new Quaternion[count];
                cumulative = new float[count];
            }
            for (int i = 0; i < points.Length; i++)
            {
                float k = points.Length > 1 ? i / (float)(points.Length - 1) : 0.5f;
                var rotation = Quaternion.Euler(RevealPitch, Mathf.Lerp(-RevealYaw, RevealYaw, k), 0f);
                rotations[i] = rotation;
                positions[i] = points[i] - rotation * Vector3.forward * distance;
            }
            positions[count - 1] = homePosition;
            rotations[count - 1] = homeRotation;

            // Time each leg by its length (with a floor), so the speed stays even along the path.
            cumulative[0] = 0f;
            for (int i = 1; i < count; i++)
                cumulative[i] = cumulative[i - 1] + Mathf.Max(0.5f, Vector3.Distance(positions[i - 1], positions[i]));

            start = Time.time;
            duration = seconds;
            active = true;
            Write(positions[0], rotations[0]);
            return true;
        }

        /// <summary>Advances the pan (call from LateUpdate, after anything that may refit the camera).</summary>
        public void Tick()
        {
            if (!active) return;
            if (cam == null)
            {
                active = false;
                return;
            }
            var tr = cam.transform;
            // Someone else placed the camera (a refit): their pose wins.
            if ((tr.position - lastPosition).sqrMagnitude > Tolerance || Quaternion.Angle(tr.rotation, lastRotation) > 0.01f)
            {
                active = false;
                return;
            }

            float u = (Time.time - start) / duration;
            if (u >= 1f)
            {
                Finish();
                return;
            }
            float s = SmootherStep(Mathf.Clamp01(u)) * cumulative[count - 1];
            int seg = 1;
            while (seg < count - 1 && cumulative[seg] < s) seg++;
            float segLength = cumulative[seg] - cumulative[seg - 1];
            float local = segLength > 0f ? Mathf.Clamp01((s - cumulative[seg - 1]) / segLength) : 1f;

            var p = CatmullRom(Point(seg - 2), Point(seg - 1), Point(seg), Point(seg + 1), local);
            var r = Quaternion.Slerp(rotations[seg - 1], rotations[seg], SmootherStep(local));
            Write(p, r);
        }

        /// <summary>Stops the pan; with <paramref name="restore"/> puts the camera back on its fitted pose.</summary>
        public void Cancel(bool restore)
        {
            if (active && restore) Finish();
            active = false;
        }

        void Finish()
        {
            active = false;
            if (cam == null) return;
            var tr = cam.transform;
            tr.SetPositionAndRotation(homePosition, homeRotation);
        }

        void Write(Vector3 p, Quaternion r)
        {
            cam.transform.SetPositionAndRotation(p, r);
            lastPosition = cam.transform.position;
            lastRotation = cam.transform.rotation;
        }

        Vector3 Point(int i) => positions[Mathf.Clamp(i, 0, count - 1)];

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        static float SmootherStep(float t) => t * t * t * (t * (6f * t - 15f) + 10f);
    }
}
