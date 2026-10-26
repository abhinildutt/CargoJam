using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>Greybox materials, all made at runtime from one URP Lit base so the shader ships in builds.</summary>
    public sealed class MaterialBank
    {
        // Same order as CargoJam.Core.ParcelColors.Codes: R B G Y P O.
        static readonly Color[] Swatches =
        {
            new Color(0.93f, 0.27f, 0.27f),
            new Color(0.24f, 0.52f, 0.96f),
            new Color(0.30f, 0.78f, 0.36f),
            new Color(0.98f, 0.80f, 0.20f),
            new Color(0.64f, 0.38f, 0.92f),
            new Color(1.00f, 0.56f, 0.16f),
        };

        readonly Material baseMaterial;
        readonly Material[] bright;
        readonly Material[] dim;
        readonly Material[] truckBody;

        public Material Ground { get; }
        public Material Tile { get; }
        public Material Edge { get; }
        public Material Pad { get; }
        public Material PadWarn { get; }
        public Material PadDanger { get; }
        public Material Road { get; }
        public Material Wheel { get; }
        public Material Window { get; }

        public MaterialBank(Material baseMaterial)
        {
            this.baseMaterial = baseMaterial;
            bright = new Material[Swatches.Length];
            dim = new Material[Swatches.Length];
            truckBody = new Material[Swatches.Length];
            for (int i = 0; i < Swatches.Length; i++)
            {
                bright[i] = Make(Swatches[i]);
                dim[i] = Make(Dim(Swatches[i]));
                truckBody[i] = Make(Color.Lerp(Swatches[i], Color.white, 0.25f));
            }
            symbolLight = Make(new Color(0.98f, 0.98f, 0.98f));
            symbolDark = Make(new Color(0.22f, 0.19f, 0.14f));

            Ground = Make(new Color(0.86f, 0.83f, 0.78f));
            Tile = Make(new Color(0.78f, 0.75f, 0.70f));
            Edge = Make(new Color(0.98f, 0.78f, 0.18f));
            Pad = Make(new Color(0.55f, 0.55f, 0.58f));
            PadWarn = Make(new Color(0.98f, 0.65f, 0.15f));
            PadDanger = Make(new Color(0.92f, 0.22f, 0.20f));
            Road = Make(new Color(0.32f, 0.32f, 0.35f));
            Wheel = Make(new Color(0.12f, 0.12f, 0.14f));
            Window = Make(new Color(0.75f, 0.88f, 0.95f));
        }

        public Material Parcel(int color, bool free) => free ? bright[color] : dim[color];
        public Material TruckBody(int color) => truckBody[color];
        public Material TruckCab(int color) => bright[color];

        /// <summary>Symbols are white, except on yellow where white would vanish.</summary>
        public Material Symbol(int color) => color == YellowIndex ? symbolDark : symbolLight;

        const int YellowIndex = 3;
        readonly Material symbolLight;
        readonly Material symbolDark;

        /// <summary>
        /// Blocked parcels are duller and a little darker, but keep their hue: dimming toward plain
        /// grey turned yellow into olive and made it read as a different color.
        /// </summary>
        static Color Dim(Color c)
        {
            float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            var grey = new Color(luma, luma, luma) * 0.8f;
            return Color.Lerp(c, grey, 0.3f) * 0.92f;
        }

        Material Make(Color c)
        {
            var m = new Material(baseMaterial);
            m.SetColor("_BaseColor", c);
            return m;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
