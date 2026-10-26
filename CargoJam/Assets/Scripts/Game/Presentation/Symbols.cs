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

        /// <summary>Builds the symbol in the XZ plane of <paramref name="parent"/>, centered at <paramref name="localPosition"/>.</summary>
        public static Transform Create(int color, Transform parent, Vector3 localPosition, float size, Material material)
        {
            var root = new GameObject("Symbol").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localScale = Vector3.one * size;

            switch (color)
            {
                case 0: // circle
                    Shape(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.56f, Thickness * 0.5f, 0.56f), 0f, material);
                    break;
                case 1: // square
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.5f, Thickness, 0.5f), 0f, material);
                    break;
                case 2: // plus
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.62f, Thickness, 0.18f), 0f, material);
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.18f, Thickness, 0.62f), 0f, material);
                    break;
                case 3: // diamond
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.42f, Thickness, 0.42f), 45f, material);
                    break;
                case 4: // cross
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.66f, Thickness, 0.17f), 45f, material);
                    Shape(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.66f, Thickness, 0.17f), -45f, material);
                    break;
                default: // two bars
                    Shape(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.13f), new Vector3(0.6f, Thickness, 0.15f), 0f, material);
                    Shape(PrimitiveType.Cube, root, new Vector3(0f, 0f, -0.13f), new Vector3(0.6f, Thickness, 0.15f), 0f, material);
                    break;
            }
            return root;
        }

        static void Shape(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, float yaw, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
