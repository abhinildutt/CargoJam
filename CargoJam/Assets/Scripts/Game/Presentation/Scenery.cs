using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>Static greybox set dressing: ground, yard tiles, dock edge and the road.</summary>
    public static class Scenery
    {
        public static void Build(Transform parent, Layout layout, MaterialBank materials)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);

            MaterialBank.Box("Ground", root, new Vector3(0f, -0.06f, 0f), new Vector3(40f, 0.1f, 40f), materials.Ground);

            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                var c = layout.CellCenter(new Core.Cell(x, y));
                MaterialBank.Box("Tile", root, c + Vector3.down * 0.005f, new Vector3(0.95f, 0.02f, 0.95f), materials.Tile);
            }

            float yardWidth = layout.Width * Layout.CellSize;
            MaterialBank.Box("Dock Edge", root, new Vector3(0f, 0.01f, Layout.DockEdgeZ - 0.1f),
                new Vector3(yardWidth + 0.2f, 0.04f, 0.1f), materials.Edge);

            MaterialBank.Box("Road", root, new Vector3(0f, -0.005f, Layout.RoadZ),
                new Vector3(60f, 0.02f, 1.7f), materials.Road);
        }
    }
}
