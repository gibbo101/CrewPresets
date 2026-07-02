using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace CrewPresets
{
    // Small name-entry dialog used for both creating and renaming groups. The confirmed name
    // is handed to a callback (which routes through a synced PresetActions method) rather than
    // written to the group directly, so it stays MP-safe.
    public class Dialog_NameCrewGroup : Window
    {
        private const int MaxNameLength = 28;

        private readonly string title;
        private readonly Action<string> onConfirm;
        private string curName;
        private bool focusedField;

        public override Vector2 InitialSize => new Vector2(280f, 175f);

        public Dialog_NameCrewGroup(string title, string initialName, Action<string> onConfirm)
        {
            this.title = title;
            this.onConfirm = onConfirm;
            curName = initialName ?? "";
            doCloseX = true;
            forcePause = true;
            closeOnAccept = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool accepted = false;
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                accepted = true;
                Event.current.Use();
            }
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect);
            titleRect.height = Text.LineHeight + 10f;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;
            GUI.SetNextControlName("CrewGroupNameField");
            string text = Widgets.TextField(new Rect(0f, titleRect.height, inRect.width, 35f), curName);
            if (text.Length < MaxNameLength) curName = text;
            if (!focusedField)
            {
                UI.FocusControl("CrewGroupNameField", this);
                focusedField = true;
            }
            if (!(Widgets.ButtonText(new Rect(15f, inRect.height - 45f, inRect.width - 30f, 35f), "OK") || accepted))
                return;
            if (curName.Trim().NullOrEmpty())
            {
                Messages.Message("NameIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            onConfirm?.Invoke(curName.Trim());
            Find.WindowStack.TryRemove(this);
        }
    }
}
