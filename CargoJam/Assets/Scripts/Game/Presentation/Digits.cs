using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// Blocky seven-segment numbers (0–99) as flat geometry, for counts printed on cargo: express tags
    /// and gate signs. Like <see cref="Symbols"/> they lie in the XZ plane with +Z as "up" on the page,
    /// so they read the right way round on a lid seen from the camera.
    /// </summary>
    public static class Digits
    {
        /// <summary>Height of one digit, and its width and the distance between two digits' centers.</summary>
        public const float Height = 1f;
        public const float DigitWidth = 0.6f;
        public const float Advance = 0.78f;
        const float Stroke = 0.17f;
        const float Thickness = 0.04f;
        public const int Max = 99;

        /// <summary>Lit segments per digit, bits a..g (top, top right, bottom right, bottom, bottom left, top left, middle).</summary>
        static readonly byte[] Segments = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        static readonly Mesh[] meshes = new Mesh[Max + 1];

        /// <summary>The width a number takes at <see cref="Height"/> 1.</summary>
        public static float Width(int value) => Mathf.Clamp(value, 0, Max) >= 10 ? Advance + DigitWidth : DigitWidth;

        /// <summary>The number as one cached mesh (height 1, centered, in the XZ plane).</summary>
        public static Mesh MeshFor(int value)
        {
            int v = Mathf.Clamp(value, 0, Max);
            if (meshes[v] != null) return meshes[v];
            var b = new MeshBuilder();
            Append(b, 0, v, Matrix4x4.identity, Vector2.zero);
            meshes[v] = b.ToMesh($"Digits {v}");
            return meshes[v];
        }

        /// <summary>
        /// Adds the number's geometry (height 1, centered on the origin of <paramref name="matrix"/>)
        /// to a builder. Values are clamped to 0–99.
        /// </summary>
        public static void Append(MeshBuilder b, int submesh, int value, Matrix4x4 matrix, Vector2 uv)
        {
            int v = Mathf.Clamp(value, 0, Max);
            if (v >= 10)
            {
                Digit(b, submesh, v / 10, matrix * Matrix4x4.Translate(new Vector3(-Advance * 0.5f, 0f, 0f)), uv);
                Digit(b, submesh, v % 10, matrix * Matrix4x4.Translate(new Vector3(Advance * 0.5f, 0f, 0f)), uv);
            }
            else
            {
                Digit(b, submesh, v, matrix, uv);
            }
        }

        static void Digit(MeshBuilder b, int submesh, int digit, Matrix4x4 matrix, Vector2 uv)
        {
            int mask = Segments[digit];
            const float x = (DigitWidth - Stroke) * 0.5f;
            const float z = (Height - Stroke) * 0.5f;
            const float vertical = Height * 0.5f + Stroke * 0.5f;
            // A lone "1" is two right-hand segments; center it so it doesn't sit off to one side.
            if (digit == 1) matrix *= Matrix4x4.Translate(new Vector3(-x, 0f, 0f));
            if ((mask & 0x01) != 0) Bar(b, submesh, matrix, new Vector3(0f, 0f, z), new Vector3(DigitWidth, Thickness, Stroke), uv);
            if ((mask & 0x02) != 0) Bar(b, submesh, matrix, new Vector3(x, 0f, z * 0.5f), new Vector3(Stroke, Thickness, vertical), uv);
            if ((mask & 0x04) != 0) Bar(b, submesh, matrix, new Vector3(x, 0f, -z * 0.5f), new Vector3(Stroke, Thickness, vertical), uv);
            if ((mask & 0x08) != 0) Bar(b, submesh, matrix, new Vector3(0f, 0f, -z), new Vector3(DigitWidth, Thickness, Stroke), uv);
            if ((mask & 0x10) != 0) Bar(b, submesh, matrix, new Vector3(-x, 0f, -z * 0.5f), new Vector3(Stroke, Thickness, vertical), uv);
            if ((mask & 0x20) != 0) Bar(b, submesh, matrix, new Vector3(-x, 0f, z * 0.5f), new Vector3(Stroke, Thickness, vertical), uv);
            if ((mask & 0x40) != 0) Bar(b, submesh, matrix, Vector3.zero, new Vector3(DigitWidth, Thickness, Stroke), uv);
        }

        static void Bar(MeshBuilder b, int submesh, Matrix4x4 matrix, Vector3 position, Vector3 scale, Vector2 uv) =>
            MeshKit.AddBox(b, submesh, matrix * Matrix4x4.TRS(position, Quaternion.identity, scale), 0f, uv);
    }
}
