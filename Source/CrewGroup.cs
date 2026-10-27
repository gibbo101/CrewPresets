using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CrewPresets
{
    /// <summary>
    /// A named group of pawn references - any mix of colonists, animals and mechs.
    /// Membership is save-specific (pawn refs), scribed with the game.
    /// </summary>
    public class CrewGroup : IExposable
    {
        public string name = "";
        public List<Pawn> members = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Collections.Look(ref members, "members", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // References to pawns that were not saved resolve to null; drop them.
                members ??= new List<Pawn>();
                members.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Per-gravship-engine boarding exclusions: animals/mechs the player has UNTICKED in the
    /// boarding panel. Exclusion-based so newly tamed/gestated pawns default to boarding,
    /// matching vanilla's board-everything checkboxes.
    /// </summary>
    public class BoardingExclusions : IExposable
    {
        public int engineId;
        public List<Pawn> pawns = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref engineId, "engineId");
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new List<Pawn>();
                pawns.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>Per-save storage for crew groups, like bills. Auto-instantiated by Game.FillComponents.</summary>
    public class GameComponent_CrewPresets : GameComponent
    {
        public List<CrewGroup> groups = new List<CrewGroup>();
        public List<BoardingExclusions> boardingExclusions = new List<BoardingExclusions>();

        public GameComponent_CrewPresets(Game game)
        {
        }

        public static GameComponent_CrewPresets Get => Current.Game.GetComponent<GameComponent_CrewPresets>();

        public List<Pawn> ExclusionsFor(int engineId) => FindExclusions(engineId)?.pawns;

        public List<Pawn> EnsureExclusionsFor(int engineId)
        {
            BoardingExclusions e = FindExclusions(engineId);
            if (e == null)
            {
                e = new BoardingExclusions { engineId = engineId };
                boardingExclusions.Add(e);
            }
            return e.pawns;
        }

        private BoardingExclusions FindExclusions(int engineId) =>
            boardingExclusions.FirstOrDefault(x => x.engineId == engineId);

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref boardingExclusions, "boardingExclusions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                groups ??= new List<CrewGroup>();
                boardingExclusions ??= new List<BoardingExclusions>();
            }
        }
    }
}
