using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The scenery, lighting and atmosphere around the board and on the home screen. Lives outside
    /// the level root (it survives level rebuilds) and fully rebuilds only when the stop or mood
    /// changes; otherwise it reshuffles props from the level's seed. Owned by stream B.
    /// </summary>
    public sealed class WorldEnvironment : MonoBehaviour
    {
        Light sun;
        Camera cam;
        MaterialBank materials;
        int lastStop = -1;
        Mood lastMood;

        /// <summary>World-space points the arrival reveal pans the camera across (back band, left to right).</summary>
        public Vector3[] RevealPoints { get; private set; } = new Vector3[0];

        public void Init(Light sun, Camera camera, MaterialBank materials)
        {
            this.sun = sun;
            cam = camera;
            this.materials = materials;
        }

        /// <summary>Shows the environment for a level whose board uses <paramref name="layout"/>.</summary>
        public EnvironmentInfo ShowLevel(int levelIndex, Layout layout) => Show(levelIndex);

        /// <summary>Shows the home-screen diorama for the destination of <paramref name="levelIndex"/>.</summary>
        public EnvironmentInfo ShowMenu(int levelIndex) => Show(levelIndex);

        /// <summary>Removes everything this environment built.</summary>
        public void Clear()
        {
            lastStop = -1;
        }

        // Contract stub: applies the old route theme so the game keeps working until stream B lands.
        EnvironmentInfo Show(int levelIndex)
        {
            int stop = Journey.StopFor(levelIndex);
            var mood = Journey.MoodFor(levelIndex);
            var info = new EnvironmentInfo
            {
                LevelIndex = levelIndex,
                Stop = stop,
                Mood = mood,
                Biome = Biomes.For(stop),
                Changed = stop != lastStop || mood != lastMood,
                Arrival = Journey.IsArrival(levelIndex),
            };
            lastStop = stop;
            lastMood = mood;
            Theme.Apply(levelIndex, materials, cam);
            return info;
        }
    }
}
