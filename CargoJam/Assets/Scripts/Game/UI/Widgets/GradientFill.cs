using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// A vertical gradient drawn as a vertex-colored mesh (USS has no gradients): legibility scrims
    /// over the 3D world and soft backdrops. Never picks.
    /// </summary>
    public sealed class GradientFill : VisualElement
    {
        Color top, bottom;
        /// <summary>Where the gradient starts (0..1 of the height); above it the top color is solid.</summary>
        float from;

        public GradientFill(Color top, Color bottom, float from = 0f)
        {
            this.top = top;
            this.bottom = bottom;
            this.from = Mathf.Clamp01(from);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetColors(Color topColor, Color bottomColor)
        {
            top = topColor;
            bottom = bottomColor;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f || float.IsNaN(r.width)) return;
            float mid = r.yMin + r.height * from;
            // Two quads: a solid band down to `from`, then the blend to the bottom.
            var mesh = context.Allocate(8, 12);
            Quad(mesh, 0, r.xMin, r.xMax, r.yMin, mid, top, top);
            Quad(mesh, 4, r.xMin, r.xMax, mid, r.yMax, top, bottom);
        }

        static void Quad(MeshWriteData mesh, ushort first, float left, float right, float y0, float y1, Color c0, Color c1)
        {
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, y1, Vertex.nearZ), tint = c1 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, y0, Vertex.nearZ), tint = c0 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, y0, Vertex.nearZ), tint = c0 });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, y1, Vertex.nearZ), tint = c1 });
            mesh.SetNextIndex(first);
            mesh.SetNextIndex((ushort)(first + 1));
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex((ushort)(first + 3));
            mesh.SetNextIndex(first);
        }
    }
}
