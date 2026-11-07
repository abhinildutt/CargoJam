using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The one global post-processing volume (runtime profile): mild bloom, ACES tonemapping, color
    /// adjustments and a soft vignette, retuned per biome and mood.
    /// </summary>
    public sealed class PostFx
    {
        /// <summary>Design caps (§5): bloom ≤0.35 at threshold 1, vignette ≤0.22.</summary>
        const float MaxBloom = 0.35f;
        const float MaxVignette = 0.22f;
        /// <summary>Switch to Neutral if ACES shifts the parcel hues too far on device.</summary>
        const TonemappingMode Tonemap = TonemappingMode.ACES;

        readonly Volume volume;
        readonly VolumeProfile profile;
        readonly Bloom bloom;
        readonly Tonemapping tonemapping;
        readonly ColorAdjustments color;
        readonly Vignette vignette;

        public PostFx(Transform parent)
        {
            var go = new GameObject("Post Fx");
            go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Environment Post Fx";
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(false);

            tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(Tonemap);

            color = profile.Add<ColorAdjustments>(true);
            color.hueShift.Override(0f);

            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.08f, 0.07f, 0.1f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);
        }

        public void Apply(in LightingPreset light)
        {
            bloom.intensity.Override(Mathf.Clamp(light.Bloom, 0f, MaxBloom));
            bloom.tint.Override(Color.Lerp(Color.white, light.Sun, 0.35f));
            color.postExposure.Override(light.Exposure);
            color.contrast.Override(light.Contrast);
            color.saturation.Override(light.Saturation);
            color.colorFilter.Override(light.Filter.a > 0f ? light.Filter : Color.white);
            vignette.intensity.Override(Mathf.Clamp(light.Vignette, 0f, MaxVignette));
        }

        public void Destroy()
        {
            if (volume != null) Object.Destroy(volume.gameObject);
            if (profile != null) Object.Destroy(profile);
        }
    }
}
