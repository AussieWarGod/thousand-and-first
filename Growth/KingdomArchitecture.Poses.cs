using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		private static bool TryPose(LoadState State, RawPose Raw,
			out ArchitecturePoseDraft Draft)
		{
			Draft = null;
			if (Raw.BadAttributes.Count > 0)
				return Fault(State, "pose " + Raw.Key,
					"an explicitly malformed attribute survived the complete merge");
			if (!Required(State, Raw, "Mode", out string modeText)
				|| !KingdomArchitectureRules.TryParsePoseMode(modeText,
					out ArchitecturePoseMode mode))
				return Fault(State, "pose " + Raw.Key,
					"Mode must be exactly cardinal, connected, or invariant");
			ArchitecturePoseDraft pose = new ArchitecturePoseDraft
			{
				Blueprint = Raw.Key,
				Mode = mode,
				North = Optional(Raw, "North"),
				East = Optional(Raw, "East"),
				South = Optional(Raw, "South"),
				West = Optional(Raw, "West")
			};
			if (!BlueprintExists(pose.Blueprint))
				return Fault(State, "pose " + Raw.Key,
					"semantic base is absent from Qud: " + pose.Blueprint);
			if (mode != ArchitecturePoseMode.Cardinal)
			{
				if (pose.North != null || pose.East != null || pose.South != null || pose.West != null)
					return Fault(State, "pose " + Raw.Key,
						"connected and invariant modes reject directional siblings");
				Draft = pose;
				return true;
			}
			if (!KingdomArchitectureRules.CardinalPoseIdentityAllowed(pose.Blueprint))
				return Fault(State, "pose " + Raw.Key,
					"cardinal mode is prohibited for runtime fixtures with exact semantic identity");
			string[] siblings = new string[] { pose.North, pose.East, pose.South, pose.West };
			for (int i = 0; i < siblings.Length; i++)
			{
				string sibling = siblings[i];
				if (!ValidBlueprint(sibling) || !BlueprintExists(sibling))
					return Fault(State, "pose " + Raw.Key,
						"cardinal mode requires four existing directional blueprints");
				if (!PoseSiblingOf(sibling, pose.Blueprint))
					return Fault(State, "pose " + Raw.Key,
						"directional blueprint does not inherit semantic base: " + sibling);
			}
			if (!TryPoseParity(pose.Blueprint, siblings, out string parityFailure))
				return Fault(State, "pose " + Raw.Key, parityFailure);
			Draft = pose;
			return true;
		}

		private static bool PoseSiblingOf(string Candidate, string SemanticBase)
		{
			if (Candidate == SemanticBase) return true;
			try
			{
				GameObjectBlueprint blueprint =
					GameObjectFactory.Factory.GetBlueprintIfExists(Candidate);
				return blueprint != null && blueprint.InheritsFrom(SemanticBase);
			}
			catch { return false; }
		}

		private static bool OptionalOrientation(LoadState State, RawRecord Raw, string Name,
			out bool HasValue, out ArchitectureFacing Value)
		{
			HasValue = false;
			Value = ArchitectureFacing.North;
			if (Raw.BadAttributes.Contains(Name))
				return Fault(State, Raw.Key + " " + Name, "orientation attribute is malformed");
			if (!Raw.Values.TryGetValue(Name, out string text)) return true;
			HasValue = true;
			string folded = Fold(text);
			if (folded == "north") Value = ArchitectureFacing.North;
			else if (folded == "east") Value = ArchitectureFacing.East;
			else if (folded == "south") Value = ArchitectureFacing.South;
			else if (folded == "west") Value = ArchitectureFacing.West;
			else return Fault(State, Raw.Key + " " + Name,
				"orientation must be exactly north, east, south, or west");
			return true;
		}

		private static bool TryGlyphOrientations(LoadState State, RawRecord Raw,
			ArchitectureGlyphDraft Glyph)
		{
			return OptionalOrientation(State, Raw, "GroundOrientation",
				out Glyph.HasGroundOrientation, out Glyph.GroundOrientation)
				&& OptionalOrientation(State, Raw, "StructureOrientation",
					out Glyph.HasStructureOrientation, out Glyph.StructureOrientation)
				&& OptionalOrientation(State, Raw, "ObjectOrientation",
					out Glyph.HasObjectOrientation, out Glyph.ObjectOrientation);
		}
	}
}
