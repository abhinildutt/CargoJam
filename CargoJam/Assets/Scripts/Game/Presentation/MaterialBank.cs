using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The game's materials, all made at runtime from one URP Lit base so the shader ships in builds.
    /// Environment colors (ground, tiles, road, paint, water) are recolored in place per biome.
    /// </summary>
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

        static readonly Color SymbolLight = new Color(0.98f, 0.98f, 0.98f);
        static readonly Color SymbolDark = new Color(0.22f, 0.19f, 0.14f);

        /// <summary>Texel centers (u) of the 4×1 parcel skin texture: body, tape, symbol.</summary>
        public const float SkinBodyU = 0.125f;
        public const float SkinTapeU = 0.375f;
        public const float SkinSymbolU = 0.625f;

        const string CarKitColormap = "Env/Cars/Textures/colormap";

        readonly Material baseMaterial;
        readonly Material emissiveBase;
        readonly Material[] bright;
        readonly Material[] dim;
        readonly Material[] truckBody;
        readonly Material[] skinBright;
        readonly Material[] skinDim;
        readonly Material[] truckPaint;
        Texture carKit;
        bool carKitLoaded;
        Material truckChassis;

        public Material Ground { get; }
        public Material Tile { get; }
        public Material Edge { get; }
        public Material Pad { get; }
        public Material PadWarn { get; }
        public Material PadDanger { get; }
        public Material Road { get; }
        public Material Wheel { get; }
        public Material Window { get; }
        public Material Tarp { get; }
        public Material TarpDim { get; }
        public Material Gold { get; }
        public Material Steel { get; }

        /// <summary>The concrete slab under the yard and dock.</summary>
        public Material Apron { get; }
        /// <summary>Road paint: edge lines, dashes, crosswalks.</summary>
        public Material LaneMark { get; }
        /// <summary>Curbs, sidewalks and quay edges.</summary>
        public Material Curb { get; }
        public Material Water { get; }
        /// <summary>Lamp heads and lit signs: emissive while the biome's lamps are on.</summary>
        public Material Glow { get; }

        public MaterialBank(Material baseMaterial)
        {
            this.baseMaterial = baseMaterial;
            // A material asset with the emission keyword on, so URP keeps that shader variant in builds.
            emissiveBase = Resources.Load<Material>("Materials/Emissive");
            bright = new Material[Swatches.Length];
            dim = new Material[Swatches.Length];
            truckBody = new Material[Swatches.Length];
            skinBright = new Material[Swatches.Length];
            skinDim = new Material[Swatches.Length];
            truckPaint = new Material[Swatches.Length];
            for (int i = 0; i < Swatches.Length; i++)
            {
                bright[i] = Make(Swatches[i]);
                dim[i] = Make(Dim(Swatches[i]));
                truckBody[i] = Make(Color.Lerp(Swatches[i], Color.white, 0.25f));
                skinBright[i] = MakeSkin(Swatches[i], SymbolColor(i), $"Parcel {i}");
                skinDim[i] = MakeSkin(Dim(Swatches[i]), SymbolColor(i), $"Parcel {i} dim");
            }
            symbolLight = Make(SymbolLight);
            symbolDark = Make(SymbolDark);

            Ground = Make(new Color(0.86f, 0.83f, 0.78f));
            Tile = Make(new Color(0.78f, 0.75f, 0.70f));
            Edge = Make(new Color(0.98f, 0.78f, 0.18f));
            Pad = Make(new Color(0.55f, 0.55f, 0.58f));
            PadWarn = Make(new Color(0.98f, 0.65f, 0.15f));
            PadDanger = Make(new Color(0.92f, 0.22f, 0.20f));
            Road = Make(new Color(0.32f, 0.32f, 0.35f));
            Wheel = Make(new Color(0.12f, 0.12f, 0.14f));
            Window = Make(new Color(0.75f, 0.88f, 0.95f));
            Tarp = Make(new Color(0.55f, 0.56f, 0.6f));
            TarpDim = Make(new Color(0.45f, 0.46f, 0.5f));
            Gold = Make(new Color(1f, 0.78f, 0.22f));
            Steel = Make(new Color(0.7f, 0.72f, 0.76f));

            Apron = Make(new Color(0.70f, 0.69f, 0.66f));
            LaneMark = Make(new Color(0.93f, 0.91f, 0.84f));
            Curb = Make(new Color(0.84f, 0.82f, 0.78f));
            Water = Make(new Color(0.18f, 0.44f, 0.55f));
            Water.SetFloat("_Smoothness", 0.86f);
            Glow = NewEmissive(new Color(1f, 0.93f, 0.78f), null);
        }

        public Material Parcel(int color, bool free) => free ? bright[color] : dim[color];

        /// <summary>
        /// The parcel box's material: a tiny texture holding the body color, the tape stripe and the
        /// symbol color, so a parcel with its symbol is one draw call (see <see cref="ParcelView"/>).
        /// </summary>
        public Material ParcelSkin(int color, bool free) => free ? skinBright[color] : skinDim[color];

        public Material TruckBody(int color) => truckBody[color];
        public Material TruckCab(int color) => bright[color];

        /// <summary>The Car Kit atlas with the white paint swatch tinted to the truck's color.</summary>
        public Material TruckPaint(int color)
        {
            if (truckPaint[color] == null)
            {
                truckPaint[color] = Make(Color.Lerp(Swatches[color], Color.white, 0.06f));
                truckPaint[color].name = $"Truck Paint {color}";
                truckPaint[color].SetTexture("_BaseMap", CarKit);
                truckPaint[color].SetFloat("_Smoothness", 0.45f);
            }
            return truckPaint[color];
        }

        /// <summary>The Car Kit atlas, untinted: chassis, glass, lights, wheels and the dark deck.</summary>
        public Material TruckChassis
        {
            get
            {
                if (truckChassis == null)
                {
                    truckChassis = Make(Color.white);
                    truckChassis.name = "Truck Chassis";
                    truckChassis.SetTexture("_BaseMap", CarKit);
                }
                return truckChassis;
            }
        }

        Texture CarKit
        {
            get
            {
                if (!carKitLoaded)
                {
                    carKitLoaded = true;
                    carKit = Resources.Load<Texture2D>(CarKitColormap);
                }
                return carKit;
            }
        }

        /// <summary>Symbols are white, except on yellow where white would vanish. -1 is the mystery mark.</summary>
        public Material Symbol(int color) => color == YellowIndex ? symbolDark : symbolLight;

        public static Color SymbolColor(int color) => color == YellowIndex ? SymbolDark : SymbolLight;

        /// <summary>Recolors the ground, yard tiles and road.</summary>
        public void SetEnvironment(Color ground, Color tile, Color road)
        {
            Ground.SetColor(BaseColorId, ground);
            Tile.SetColor(BaseColorId, tile);
            Road.SetColor(BaseColorId, road);
        }

        /// <summary>Recolors every environment material for a biome, and lights the lamps for its mood.</summary>
        public void SetBiome(BiomeDefinition biome, in LightingPreset light)
        {
            SetEnvironment(biome.Ground, biome.Tile, biome.Road);
            Apron.SetColor(BaseColorId, biome.Apron);
            LaneMark.SetColor(BaseColorId, biome.LaneMark);
            Curb.SetColor(BaseColorId, biome.Curb);
            Water.SetColor(BaseColorId, biome.Water);
            SetGlow(Glow, new Color(1f, 0.74f, 0.42f) * light.Glow, light.LampsOn);
        }

        /// <summary>A new opaque material from the URP Lit base (for environment props).</summary>
        public Material NewMaterial(Color color, Texture texture = null, float smoothness = -1f)
        {
            var m = Make(color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            if (smoothness >= 0f) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        /// <summary>A material whose emission can be switched with <see cref="SetGlow"/> (starts dark).</summary>
        public Material NewEmissive(Color color, Texture texture, Texture emissionMap = null)
        {
            var m = new Material(emissiveBase != null ? emissiveBase : baseMaterial);
            m.SetColor(BaseColorId, color);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetTexture("_EmissionMap", emissionMap);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor(EmissionColorId, Color.black);
            return m;
        }

        /// <summary>Turns an emissive material's glow on (HDR color) or off.</summary>
        public static void SetGlow(Material m, Color emission, bool on)
        {
            if (m != null) m.SetColor(EmissionColorId, on ? emission : Color.black);
        }

        const int YellowIndex = 3;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
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
            m.SetColor(BaseColorId, c);
            return m;
        }

        /// <summary>Body, tape and symbol colors in a 4×1 point-filtered texture under a white base color.</summary>
        Material MakeSkin(Color body, Color symbol, string name)
        {
            // Packing tape: a slightly darker, slightly greyer band of the body color.
            float luma = 0.299f * body.r + 0.587f * body.g + 0.114f * body.b;
            var tape = Color.Lerp(body * 0.8f, new Color(luma, luma, luma) * 0.8f, 0.2f);
            tape.a = 1f;
            var tex = new Texture2D(4, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(new[] { body, tape, symbol, body });
            tex.Apply(false, true);
            var m = Make(Color.white);
            m.name = name;
            m.SetTexture("_BaseMap", tex);
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
