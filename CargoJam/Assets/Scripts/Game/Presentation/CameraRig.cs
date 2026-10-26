using UnityEngine;

namespace CargoJam.Game.Presentation
{
    public static class CameraRig
    {
        const float Pitch = 58f;
        const float FieldOfView = 38f;

        /// <summary>
        /// Points the camera down at the content and backs it off until every corner of
        /// <paramref name="bounds"/> lands inside <paramref name="safeViewport"/> (0..1 viewport space).
        /// </summary>
        public static void Fit(Camera cam, Bounds bounds, Rect safeViewport)
        {
            cam.fieldOfView = FieldOfView;
            cam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);

            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            }

            // Aim so the content's middle lands in the middle of the safe area, not the screen.
            float near = 1f, far = 200f;
            for (int iter = 0; iter < 40; iter++)
            {
                float mid = (near + far) * 0.5f;
                Place(cam, bounds.center, mid, safeViewport);
                if (AllInside(cam, corners, safeViewport)) far = mid;
                else near = mid;
            }
            Place(cam, bounds.center, far, safeViewport);
        }

        static void Place(Camera cam, Vector3 target, float distance, Rect safe)
        {
            cam.transform.position = target - cam.transform.forward * distance;
            // Shift up or down so the target sits at the safe rect's vertical center.
            float offset = (safe.center.y - 0.5f) * 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.position -= cam.transform.up * offset;
        }

        static bool AllInside(Camera cam, Vector3[] corners, Rect safe)
        {
            foreach (var c in corners)
            {
                var v = cam.WorldToViewportPoint(c);
                if (v.z <= 0f || !safe.Contains(new Vector2(v.x, v.y))) return false;
            }
            return true;
        }
    }
}
