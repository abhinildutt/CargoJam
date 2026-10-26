using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>Terse element factories so screens read like a layout.</summary>
    public static class Ui
    {
        /// <summary>A plain container with USS classes (space separated), appended to a parent.</summary>
        public static VisualElement El(VisualElement parent, string classes = null, bool pickable = false)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            e.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
            parent?.Add(e);
            return e;
        }

        /// <summary>A label with USS classes, appended to a parent. Labels never eat presses.</summary>
        public static Label Text(VisualElement parent, string text, string classes = null)
        {
            var l = new Label(text);
            AddClasses(l, classes);
            l.pickingMode = PickingMode.Ignore;
            parent?.Add(l);
            return l;
        }

        public static Icon Icon(VisualElement parent, IconKind kind, string classes = null)
        {
            var i = new Icon(kind);
            AddClasses(i, classes);
            parent?.Add(i);
            return i;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
        }

        public static void Show(VisualElement e, bool visible) =>
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
