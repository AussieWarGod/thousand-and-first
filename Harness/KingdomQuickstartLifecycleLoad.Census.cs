using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>The zone-wide debit census lifecycle-next consults (run 46b, investigation A) and
	/// the lifecycle save's own pre-activation witness (investigation C). Split from the main
	/// shard only to keep it under the house line cap.</summary>
	internal static partial class KingdomQuickstartLifecycleLoad
	{
		/// <summary>Every dedicated store's timber, in survey order, read through the same
		/// TakeStock census the single-store proof uses. Ids and counts are parallel lists.</summary>
		private static void TimberByStore(Zone Zone, out List<string> Ids, out List<int> Timber)
		{
			Ids = new List<string>();
			Timber = new List<int>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!GameObject.Validate(item) || !KingdomMaterials.IsStockpile(item)
					|| item.Inventory == null) continue;
				int count = KingdomQuickstartBuildCensus.TakeStock(Zone, item, false,
					out var stock, out _) ? KingdomQuickstartLifecycleSteps.Timber(stock) : 0;
				Ids.Add(item.IDIfAssigned ?? "");
				Timber.Add(count);
			}
		}

		private static bool SameStores(List<string> Before, List<string> After)
		{
			if (Before.Count != After.Count) return false;
			for (int i = 0; i < Before.Count; i++)
				if (Before[i] != After[i]) return false;
			return true;
		}

		/// <summary>
		/// Run 46b/47 (investigation C): the lifecycle save's own pre-activation witness. Before
		/// this, KingdomScenarioLoadWitness.Prefix routed only Upgrade/Quickstart/Rung snapshots,
		/// so a lifecycle save fell into the generic subsidence-shaped witness with Snapshot ==
		/// null and journaled an NRE. This reads only what exists before activation: the loaded
		/// game id against the witness, and the clock. Never throws: a refusal is journaled and
		/// the load goes on to VerifyLoaded, which is the real cold-load proof.
		/// </summary>
		internal const string PreactivationRow = "lifecycle-preactivation";

		internal static void BeforeActivation(KingdomQuickstartLifecycleSnapshot Witness)
		{
			try
			{
				XRLGame game = The.Game;
				string failure = game == null ? "no live game before activation"
					: Witness == null ? "no lifecycle witness was decoded"
					: game.GameID != Witness.GameId ? "the loading game is not the saved game"
					: null;
				KingdomScenarioJournal.Append(PreactivationRow, failure == null, failure == null
					? KingdomQuickstartLifecycleSteps.Stamped("native-lifecycle step=pre-activation; saveId="
						+ game.GameID + "; turns=" + game.Turns + "; zone=" + Witness.ZoneId
						+ "; before-AfterGameLoaded-handlers-and-zone-activation=true")
					: KingdomQuickstartLifecycleSteps.Refuse("pre-activation", failure));
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append(PreactivationRow, false,
					KingdomQuickstartLifecycleSteps.Refuse("pre-activation",
						error.GetType().Name + ": " + error.Message));
			}
		}

	}
}
