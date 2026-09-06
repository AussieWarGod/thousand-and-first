using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRuntime
	{
		/// <summary>Typed registry observation through construction's versioned decoder.
		/// A default getter alone cannot prove absence; this never mints a system.</summary>
		internal static bool ConstructionAvailable(GameObject work)
		{
			return ConstructionAvailable(work, true);
		}

		/// <summary>Receipt release can observe an unplaced carrier; exact carrier authority
		/// is the caller's proof, not a missing-cell inference.</summary>
		internal static bool ConstructionAvailableForRelease(GameObject work)
		{
			return ConstructionAvailable(work, false);
		}

		private static bool ConstructionAvailable(GameObject work, bool requireCell)
		{
			XRLGame game = The.Game;
			if (!GameObject.Validate(work) || string.IsNullOrEmpty(work.IDIfAssigned)
				|| (requireCell && work.CurrentCell == null) || game == null || game.StringGameState == null
				|| game.IntGameState == null || game.Int64GameState == null
				|| game.ObjectGameState == null || game.BooleanGameState == null
				|| work.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;
			string key = KingdomConstruction.RegistryStateKey;
			bool hasString = game.HasStringGameState(key);
			KingdomDurableKeyObservation observed = new KingdomDurableKeyObservation {
				HasString = hasString, HasInt = game.HasIntGameState(key),
				HasInt64 = game.HasInt64GameState(key), HasObject = game.HasObjectGameState(key),
				HasBoolean = game.HasBooleanGameState(key),
				String = hasString ? game.GetStringGameState(key, null) : null };
			if (!KingdomScenarioStateShape.TryAuthorityText(observed, out string wire,
				out bool present, out _)) return false;
			if (!present) return true;
			if (!KingdomConstructionRules.TryDecode(wire, out List<KingdomConstructionJob> jobs)) return false;
			string receipt = work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			foreach (KingdomConstructionJob job in jobs)
			{
				if (KingdomConstructionRules.IsTerminal(job.Phase)) continue;
				if (job.Id == receipt || job.SubjectId == work.IDIfAssigned || job.SourceId == work.IDIfAssigned
					|| job.OutputId == work.IDIfAssigned || job.PhysicalItemId == work.IDIfAssigned
					|| job.PhysicalDestinationId == work.IDIfAssigned
					|| work.CurrentCell != null && job.ZoneId == work.CurrentZone?.ZoneID
						&& job.X == work.CurrentCell.X && job.Y == work.CurrentCell.Y) return false;
			}
			return true;
		}
	}
}
