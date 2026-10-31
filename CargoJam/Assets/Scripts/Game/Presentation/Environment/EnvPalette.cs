using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The environment's materials for one biome. Kenney nature colors are remapped by material name
    /// to the biome's foliage, grass, wood, rock and soil; the city and car kits share one material
    /// per atlas (city buildings get window glow); code-built set pieces ask for named solids.
    /// </summary>
    public sealed class EnvPalette
    {
        const float PropSmoothness = 0.12f;
        /// <summary>Window glow color (times the mood's <see cref="LightingPreset.Glow"/>).</summary>
        static readonly Color WindowGlow = new Color(1f, 0.76f, 0.44f);

        static Texture2D windowMask;

        readonly MaterialBank bank;
        readonly BiomeDefinition biome;
        readonly Dictionary<string, Material> nature = new Dictionary<string, Material>();
        readonly Dictionary<Texture, Material> kits = new Dictionary<Texture, Material>();
        readonly Dictionary<string, Material> solids = new Dictionary<string, Material>();
        readonly List<Material> glowing = new List<Material>();
        readonly Dictionary<string, Texture> kitTextures = new Dictionary<string, Texture>();
        Color glow;
        bool lampsOn;

        public EnvPalette(MaterialBank bank, BiomeDefinition biome)
        {
            this.bank = bank;
            this.biome = biome;
        }

        public BiomeDefinition Biome => biome;

        /// <summary>Lamp heads, lit signs and lantern rooms (shared with the menu diorama).</summary>
        public Material Glow => bank.Glow;
        public Material Water => bank.Water;
        public Material Curb => bank.Curb;
        public Material Ground => bank.Ground;
        public Material LaneMark => bank.LaneMark;

        /// <summary>Lights or darkens every window material for a mood.</summary>
        public void SetMood(in LightingPreset light)
        {
            lampsOn = light.LampsOn;
            glow = WindowGlow * light.Glow;
            foreach (var m in glowing) MaterialBank.SetGlow(m, glow, lampsOn);
        }

        /// <summary>A named solid color for code-built pieces ("crane", "container 2", ...).</summary>
        public Material Solid(string key, Color color)
        {
            if (solids.TryGetValue(key, out var m)) return m;
            m = bank.NewMaterial(color, null, PropSmoothness);
            m.name = $"{biome.Id} {key}";
            m.enableInstancing = true;
            solids[key] = m;
            return m;
        }

        public Material Wall => Solid("wall", biome.Wall);
        public Material Roof => Solid("roof", biome.Roof);
        public Material Trim => Solid("trim", biome.Trim);
        public Material Accent => Solid("accent", biome.Accent);
        public Material Rock => Solid("rock", biome.Rock);
        public Material Wood => Solid("wood", biome.Wood);
        /// <summary>Destroys the materials this palette made (the environment calls it when it goes away).</summary>
        public void Destroy()
        {
            foreach (var m in nature.Values) if (m != null) Object.Destroy(m);
            foreach (var m in kits.Values) if (m != null) Object.Destroy(m);
            foreach (var m in solids.Values) if (m != null) Object.Destroy(m);
            nature.Clear();
            kits.Clear();
            solids.Clear();
            glowing.Clear();
        }

        public Material Dark => Solid("dark", new Color(0.22f, 0.23f, 0.26f));
        public Material Metal => Solid("metal", new Color(0.70f, 0.72f, 0.75f));
        public Material Snow => Solid("snow", new Color(0.95f, 0.97f, 1f));

        /// <summary>The material a Kenney model part should render with in this biome.</summary>
        public Material Resolve(PropModel model, in PropModel.Part part)
        {
            var texture = part.Texture;
            int slash = model.Path.IndexOf('/');
            string kit = slash > 0 ? model.Path.Substring(0, slash) : model.Path;
            if (kit != "Nature")
            {
                if (texture == null) texture = KitTexture(kit);
                return KitMaterial(texture, model.Building);
            }
            if (nature.TryGetValue(part.MaterialName, out var m)) return m;
            m = bank.NewMaterial(NatureColor(part.MaterialName, part.Color), null, PropSmoothness);
            m.name = $"{biome.Id} {part.MaterialName}";
            m.enableInstancing = true;
            nature[part.MaterialName] = m;
            return m;
        }

        Material KitMaterial(Texture texture, bool building)
        {
            if (texture == null) return Solid("kit fallback", biome.Wall);
            if (kits.TryGetValue(texture, out var m)) return m;
            if (building)
            {
                m = bank.NewEmissive(biome.KitTint, texture, WindowMask);
                glowing.Add(m);
                MaterialBank.SetGlow(m, glow, lampsOn);
            }
            else
            {
                m = bank.NewMaterial(biome.KitTint, texture, PropSmoothness);
            }
            m.name = $"{biome.Id} kit {texture.name}";
            m.enableInstancing = true;
            kits[texture] = m;
            return m;
        }

        Texture KitTexture(string kit)
        {
            if (!kitTextures.TryGetValue(kit, out var t))
            {
                t = Resources.Load<Texture2D>($"Env/{kit}/Textures/colormap");
                kitTextures[kit] = t;
            }
            return t;
        }

        /// <summary>Kenney Nature Kit material names → this biome's colors.</summary>
        Color NatureColor(string name, Color original)
        {
            var b = biome;
            switch (name)
            {
                case "leafsFall": return Color.Lerp(b.Foliage, new Color(0.86f, 0.56f, 0.30f), 0.65f);
                case "leafsDark": return b.Foliage * 0.82f;
                case "leafsGreen": return b.Foliage;
                case "grass": return b.Grass;
                case "dirt": return b.Dirt;
                case "dirtDark": return b.Dirt * 0.82f;
                case "stone": return b.Rock;
                case "woodBirch": return Color.Lerp(b.Wood, Color.white, 0.7f);
                case "woodInner": return Color.Lerp(b.Wood, Color.white, 0.45f);
                case "woodBark": case "wood": return b.Wood;
                case "woodBarkDark": case "woodDark": return b.Wood * 0.8f;
            }
            if (name.StartsWith("leafs")) return b.Foliage;
            if (name.StartsWith("wood")) return b.Wood;
            // Flowers, mushrooms, corn, tents: keep the hue, a quarter less saturated.
            float luma = 0.299f * original.r + 0.587f * original.g + 0.114f * original.b;
            var c = Color.Lerp(original, new Color(luma, luma, luma), 0.25f);
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// Emission mask for the city kits' shared 512² atlas (8×8 swatches): only the two light-blue
        /// glass swatches (column 5, rows 2–3 from the top) glow. Point-filtered, no mips.
        /// </summary>
        static Texture2D WindowMask
        {
            get
            {
                if (windowMask != null) return windowMask;
                windowMask = new Texture2D(8, 8, TextureFormat.RGBA32, false)
                {
                    name = "City Window Mask",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var px = new Color[64];
                for (int i = 0; i < px.Length; i++) px[i] = Color.black;
                // Texture rows count from the bottom: image rows 2 and 3 are texture rows 5 and 4.
                px[5 * 8 + 5] = Color.white;
                px[4 * 8 + 5] = Color.white;
                windowMask.SetPixels(px);
                windowMask.Apply(false, true);
                return windowMask;
            }
        }
    }
}
