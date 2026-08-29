using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{

		/// <summary>
		/// The guestbook's own reading, appended to the roll of settlers report. Call once from
		/// <c>KingdomReports.Roll</c>, after its own text is built. Empty string when there is
		/// nothing to add, so the appendix never leaves a bare heading behind.
		/// </summary>
		public static string RollAppendix(KingdomSystem System)
		{
			if (System == null || System.GuestbookLines == null || System.GuestbookLines.Count == 0)
			{
				return "";
			}
			StringBuilder text = new StringBuilder();
			text.Append("\n\n{{C|The guestbook}}");
			for (int i = 0; i < System.GuestbookLines.Count; i++)
			{
				text.Append("\n").Append(System.GuestbookLines[i]);
			}
			return text.ToString();
		}

		// ==================================================================================
		// The carry-sign
		// ==================================================================================

		/// <summary>
		/// Plants a carry-sign at <paramref name="Actor"/>'s current cell, on whatever container
		/// or pile stands there. Call from <see cref="XRL.World.Parts.r_KingdomCarrySign"/>'s
		/// inventory action.
		/// </summary>
		public static void AttemptPlantCarrySign(GameObject Actor, GameObject Sign)
		{
			if (!CarrySignEnabled || Actor == null || Sign == null)
			{
				return;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; no new haul can be marked.");
				return;
			}
			Cell cell = Actor.CurrentCell;
			Zone zone = Actor.CurrentZone;
			if (cell == null || zone == null)
			{
				Popup.Show("There is no ground here to plant a sign on.");
				return;
			}
			KingdomCarryRuntime.PlantPlan plan;
			string failure;
			long now = The.Game.TimeTicks;
			if (!KingdomCarryRuntime.TryPreparePlant(system, Actor, Sign, zone, cell, now,
				out plan, out failure))
			{
				Popup.Show(failure);
				return;
			}
			// Consent precedes reservation and every physical callback. The prompt names every
			// whole object/stack and the distance-scaled wait frozen by the draft plan.
			if (Popup.ShowYesNo(KingdomGuestRules.PlantConfirm(plan.Description, plan.Days))
				!= DialogResult.Yes)
			{
				return;
			}
			if (!KingdomCarryRuntime.PublishPlant(plan, out failure))
			{
				Popup.Show(failure);
				return;
			}
			MessageQueue.AddPlayerMessage(KingdomGuestRules.PlantedMessage(plan.Days));
			KingdomLog.Log("carry-sign: exact manifest planted days=" + plan.Days
				+ " objects=" + plan.Sources.Count);
		}

		/// <summary>Compatibility resolver for v5 saves only. New work never enters this scalar
		/// destroy/mint lane; a zero-material System.Haul is the v6 schedule projection.</summary>
		private static void ResolveLegacyHaulIfDue(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, long TimeTicks)
		{
			KingdomCarryHaul haul = System.Haul;
			if (haul == null || !string.Equals(System.CurrentSettlementId,
				haul.DestinationSettlementId, StringComparison.Ordinal) ||
				!KingdomIdentityRules.IsSettlementId(haul.DestinationSettlementId) ||
				!KingdomGuestRules.ShouldResolveHaul(TimeTicks, haul.DueTick))
			{
				return;
			}
			KingdomMaterialTally manifest = new KingdomMaterialTally();
			manifest.Set(KingdomMaterial.Mud, haul.Mud);
			manifest.Set(KingdomMaterial.Brush, haul.Brush);
			manifest.Set(KingdomMaterial.Timber, haul.Timber);
			manifest.Set(KingdomMaterial.Stone, haul.Stone);
			manifest.Set(KingdomMaterial.Marble, haul.Marble);
			manifest.Set(KingdomMaterial.Scrap, haul.Scrap);
			if (manifest.Total() <= 0) return;
			string description = manifest.Describe() ?? "the load";
			bool raidActive = System.RaidState == 1;
			bool raidersPresent = Survey != null && Survey.Raiders.Count > 0;
			if (KingdomGuestRules.HaulWaitsForSafety(raidActive, raidersPresent))
			{
				KingdomLog.Log("carry-sign: due haul retained while threat stands manifest="
					+ description);
				return;
			}
			System.Haul = null;
			int spilled = KingdomMaterials.Deliver(System, Z, manifest);
			KingdomChronicle.Record(System, KingdomGuestRules.DeliveredChronicleLine(KingdomPresentation.Rich(System.SeatName), description));
			System.Ledger.Note(KingdomGuestRules.DeliveredLedgerNote(description));
			KingdomLog.Log("carry-sign: delivered manifest=" + description + " spilled=" + spilled);
		}
	}
}
