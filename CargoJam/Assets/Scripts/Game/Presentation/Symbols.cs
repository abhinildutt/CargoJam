using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// A flat shape per color, laid on top of parcels and truck cabs, so the game is playable without
    /// telling colors apart (colorblind players, grayscale ads). Order matches ParcelColors.Codes:
    /// Red ●, Blue ■, Green ✚, Yellow ◆, Purple ✖, Orange ═.
    /// </summary>
    public static class Symbols
    {
        const float Thickness = 0.04f;
        static readonly Mesh[] meshes = new Mesh[6];

        /// <summary>Builds the symbol in the XZ plane of <paramref name="parent"/>, centered at <paramref name="localPosition"/>.</summary>
        public static Transform Create(int color, Transform parent, Vector3 localPosition, float size, Material material)
        {
            var r = MeshKit.Show("Symbol", parent, MeshFor(color), material, castShadows: false);
            var root = r.transform;
            root.localPosition = localPosition;
            root.localScale = Vector3.one * size;
            return root;
        }

        /// <summary>The symbol as one cached mesh (size 1, centered, in the XZ plane).</summary>
        public static Mesh MeshFor(int color)
        {
            int i = Mathf.Clamp(color, 0, meshes.Length - 1);
            if (meshes[i] != null) return meshes[i];
            var b = new MeshBuilder();
            Append(b, 0, i, Matrix4x4.identity, Vector2.zero);
            meshes[i] = b.ToMesh($"Symbol {i}");
            return meshes[i];
        }

        /// <summary>Adds the symbol's geometry (size 1, centered on the origin of <paramref name="matrix"/>) to a builder.</summary>
        public static void Append(MeshBuilder b, int submesh, int color, Matrix4x4 matrix, Vector2 uv)
        {
            switch (color)
            {
                case 0: // circle
                    MeshKit.AddCylinder(b, submesh, matrix * Matrix4x4.Scale(new Vector3(0.56f, Thickness, 0.56f)),
                        24, 0.5f, 0.5f, uv);
                    break;
                case 1: // square
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.5f, Thickness, 0.5f), 0f, uv);
                    break;
                case 2: // plus
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.62f, Thickness, 0.18f), 0f, uv);
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.18f, Thickness, 0.62f), 0f, uv);
                    break;
                case 3: // diamond
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.42f, Thickness, 0.42f), 45f, uv);
                    break;
                case 4: // cross
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.66f, Thickness, 0.17f), 45f, uv);
                    Bar(b, submesh, matrix, Vector3.zero, new Vector3(0.66f, Thickness, 0.17f), -45f, uv);
                    break;
                default: // two bars
                    Bar(b, submesh, matrix, new Vector3(0f, 0f, 0.13f), new Vector3(0.6f, Thickness, 0.15f), 0f, uv);
                    Bar(b, submesh, matrix, new Vector3(0f, 0f, -0.13f), new Vector3(0.6f, Thickness, 0.15f), 0f, uv);
                    break;
            }
        }

        static void Bar(MeshBuilder b, int submesh, Matrix4x4 matrix, Vector3 position, Vector3 scale, float yaw, Vector2 uv) =>
            MeshKit.AddBox(b, submesh, matrix * MeshKit.TRS(position, yaw, scale), 0f, uv);
    }
}
