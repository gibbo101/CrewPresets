using System;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // A float-menu row with a separate right-click action (overwrite the group). Needed
    // because vanilla FloatMenuOption fires its action on ANY mouse button.
    public class FloatMenuOption_CrewGroup : FloatMenuOption
    {
        private readonly Action rightClickAction;

        public FloatMenuOption_CrewGroup(string label, Action action, Action rightClickAction)
            : base(label, action)
        {
            this.rightClickAction = rightClickAction;
        }

        public override bool DoGUI(Rect rect, bool colonistOrdering, FloatMenu floatMenu)
        {
            if (rightClickAction != null && Event.current.type == EventType.MouseDown
                && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                rightClickAction();
                return true; // closes the float menu, like a normal choice
            }
            return base.DoGUI(rect, colonistOrdering, floatMenu);
        }
    }
}
