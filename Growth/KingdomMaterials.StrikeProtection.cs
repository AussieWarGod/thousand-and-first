using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		/// <summary>A strike may destroy only an empty exact work graph. Player goods and
		/// protected purpose cargo remain physical; moving them out is an explicit prerequisite.</summary>
		private static bool StrikeTargetsUnencumbered(GameObject building, Zone zone,
			KingdomStrikeIntent intent, out string failure)
		{
			failure = null;
			if (!StrikeObjectUnencumbered(building, out failure)) return false;
			if (intent?.Targets == null)
			{
				failure = "The strike target list is unavailable.";
				return false;
			}
			HashSet<string> seen = new HashSet<string>();
			for (int i = 0; i < intent.Targets.Count; i++)
			{
				KingdomStrikeTarget target = intent.Targets[i];
				GameObject exact = target == null ? null : ExactObject(target.Id);
				if (target == null || string.IsNullOrEmpty(target.Id) || !seen.Add(target.Id)
					|| !GameObject.Validate(exact) || exact.CurrentZone != zone
					|| exact.CurrentCell != zone?.GetCell(target.X, target.Y)
					|| !StrikeObjectUnencumbered(exact, out failure))
				{
					if (failure == null)
						failure = "An exact strike target is absent, duplicated, or moved.";
					return false;
				}
			}
			return true;
		}

		private static bool StrikeObjectUnencumbered(GameObject target, out string failure)
		{
			failure = null;
			if (!GameObject.Validate(target))
			{
				failure = "An exact strike target is unavailable.";
				return false;
			}
			if (KingdomPurpose.HasProtectedCargoEvidence(target))
			{
				failure = "Protected purpose cargo cannot be struck or salvaged as a building part.";
				return false;
			}
			if (!KingdomOrdinaryCustody.TryProveEmpty(target, out _))
			{
				failure = "Empty every struck building and furnishing first; stored objects are never destroyed with the structure.";
				return false;
			}
			return true;
		}
	}
}
