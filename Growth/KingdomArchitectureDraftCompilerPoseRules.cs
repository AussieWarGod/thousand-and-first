using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		private static bool TryValidateGlyphPose(string Token, bool HasOrientation,
			ArchitectureFacing Orientation, Dictionary<string, ArchitecturePaletteSlot> Slots,
			ArchitecturePoseRegistry Poses, out string Failure)
		{
			Failure = null;
			if (!HasSceneryToken(Token))
				return !HasOrientation
					|| Fail("local orientation requires scenery on the same layer", out Failure);
			string key = Token.Substring(1);
			if (!Slots.TryGetValue(key, out ArchitecturePaletteSlot slot))
				return Fail("used scenery has no exact palette slot", out Failure);
			if (Poses.IsPoisoned(slot.Blueprint))
				return Fail("selected scenery references a malformed fixture pose declaration",
					out Failure);
			if (!Poses.TryGet(slot.Blueprint, out ArchitecturePoseDraft pose))
				return !HasOrientation || Fail(
					"local orientation requires an exact cardinal fixture pose declaration", out Failure);
			if (pose.Mode == ArchitecturePoseMode.Cardinal)
				return HasOrientation && KnownFacing(Orientation)
					|| Fail("cardinal scenery requires one valid layer-local orientation", out Failure);
			return !HasOrientation
				|| Fail("connected or invariant scenery rejects local orientation", out Failure);
		}
	}
}
