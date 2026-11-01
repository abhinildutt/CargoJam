using System.Linq;
using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>Level JSON files in Resources/Levels, ordered by file name.</summary>
    public sealed class LevelLibrary
    {
        readonly TextAsset[] files;
        /// <summary>Each file's HARD flag, read the first time it is asked for.</summary>
        readonly bool?[] hard;

        public int Count => files.Length;

        LevelLibrary(TextAsset[] files)
        {
            this.files = files;
            hard = new bool?[files.Length];
        }

        public static LevelLibrary Load()
        {
            var files = Resources.LoadAll<TextAsset>("Levels").OrderBy(f => f.name).ToArray();
            if (files.Length == 0) Debug.LogError("No levels found in Resources/Levels");
            return new LevelLibrary(files);
        }

        public LevelDefinition Get(int index)
        {
            var data = JsonUtility.FromJson<LevelData>(files[index].text);
            return LevelDefinition.Parse(data);
        }

        /// <summary>Whether a level file is a HARD one, parsed once per file and then cached.</summary>
        public bool IsHard(int index)
        {
            if (!hard[index].HasValue) hard[index] = Get(index).Hard;
            return hard[index].Value;
        }

        public string NameOf(int index) => files[index].name;
    }

    public static class Progress
    {
        const string LevelKey = "cargojam.level";

        public static int LevelIndex
        {
            get => PlayerPrefs.GetInt(LevelKey, 0);
            set
            {
                PlayerPrefs.SetInt(LevelKey, value);
                PlayerPrefs.Save();
            }
        }
    }
}
