using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainHandoverOccupancy
	{
		private static GameObject Resident;
		private static string JobId, ResidentId, Materials;
		private static bool Entered, Renovation;
		private static long Tick;
		private static int Water, DestinationCalls;
		private static Cell Blocked;
		private static KingdomSurvey Survey;
		internal static bool Proved;
		internal static bool DenyDestinations;

		internal static void Arm(GameObject Body, string PaidJob, bool Retained = false)
		{
			Require(Resident == null && !Entered || Proved && JobId != PaidJob,
				"handover occupant probe armed twice or before its prior proof");
			Entered = Proved = false; Renovation = Retained; Blocked = null; DestinationCalls = 0;
			Resident = Body; ResidentId = Body.ID; JobId = PaidJob;
		}

		internal static bool Before(GameObject Owner, GameObject Target, string Key,
			r_KingdomImprovement Intent, KingdomArchitectureIntent Layout, KingdomConstructionJob Job)
		{
			if (Entered || Resident == null || Job?.Id != JobId) return false;
			Entered = true;
			var system = The.Game.GetSystem<KingdomSystem>();
			Zone zone = Owner.CurrentZone;
			Survey = KingdomSurvey.ActiveFor(zone);
			Require(KingdomCampHeartChainTrace.Active(system) && Key == (Renovation ? "heartcourt" : "heartmoot")
				&& KingdomSurvey.ActiveFor(zone) != null && GameObject.Validate(Resident)
				&& Resident.CurrentZone == zone && Resident.IsAlive
				&& KingdomPlots.IsMovableEnvelopeOccupant(system, zone, Resident),
				"handover probe lacks its paid city and eligible resident");
			Require(KingdomArchitectureRuntime.TryRead(Owner, out var before, out string failure), failure);
			Require(KingdomArchitectureStamper.TryNewBlockingCells(zone, before, Layout,
				out var slots, out failure), failure);
			foreach (var slot in slots)
			{
				int x = slot.Key % zone.Width, y = slot.Key / zone.Width;
				Cell cell = zone.GetCell(x, y);
				if (before.Rect.Contains(x, y) == Renovation && slot.Value == ArchitecturePassability.Blocked
					&& cell != null && cell.IsPassable(Resident) && !cell.HasOpenLiquidVolume())
				{ Blocked = cell; break; }
			}
			Require(Blocked != null, "handover probe lacks newly blocked ground; retained=" + Renovation);
			Tick = The.Game.TimeTicks; Materials = Job.Claims.MaterialSpent; Water = Job.Claims.WaterSpent;
			Place(Resident, Blocked);
			bool strict = Renovation
				? KingdomArchitectureStamper.TryProveRenovationOccupants(system, zone, before, Layout,
					false, out _, out failure)
				: KingdomArchitectureStamper.TryProveEnvelopeGrowth(system, zone, Owner, Target, Layout, true, out failure);
			Require(!strict && failure != null
				&& failure.StartsWith("a living occupant stands on ", StringComparison.Ordinal),
				"late resident did not block strict handover ground: " + failure);
			ProbeProtected(The.Player, Owner, Target, Key, Intent, Layout, Job);
			GameObject stranger = GameObject.Create("NPC");
			try
			{
				Require(stranger != null && stranger.IsCreature
					&& !KingdomCitizenship.BelongsTo(system, stranger), "handover stranger is not foreign");
				ProbeProtected(stranger, Owner, Target, Key, Intent, Layout, Job);
			}
			finally { if (GameObject.Validate(stranger)) stranger.Obliterate(null, Silent: true); }
			DenyDestinations = true;
			try
			{
				Require(!KingdomUpgrade.TryPrepareHandoverGround(Owner, Target, Key, Intent, Layout,
					Job, out failure) && failure == "no free ground beside the site to stand them on"
					&& DestinationCalls > 0 && Resident.CurrentCell == Blocked,
					"no-destination handover did not refuse before moving: " + failure);
				Unchanged(Owner, Target, Intent, Job);
			}
			finally { DenyDestinations = false; }
			Record(Renovation ? "camp-heart-chain-renovation-refusals" : "camp-heart-chain-handover-refusals", "post-payment-resident=true; strict-refused=true"
				+ "; founder-protected=true; stranger-protected=true; synthetic-no-destination=true"
				+ "; no-movement=true; no-debit=true; restored=true; job=" + JobId);
			return true;
		}

		private static void ProbeProtected(GameObject Body, GameObject Owner, GameObject Target,
			string Key, r_KingdomImprovement Intent, KingdomArchitectureIntent Layout,
			KingdomConstructionJob Job)
		{
			Cell origin = Body.CurrentCell;
			string id = Body.ID;
			try
			{
				Place(Body, Blocked);
				Require(!KingdomUpgrade.TryPrepareHandoverGround(Owner, Target, Key, Intent, Layout,
					Job, out string failure) && failure != null
					&& failure.StartsWith("a living occupant stands on ", StringComparison.Ordinal)
					&& Resident.CurrentCell == Blocked && Body.CurrentCell == Blocked
					&& Body.IDIfAssigned == id, "protected handover occupant was moved: " + failure);
				Unchanged(Owner, Target, Intent, Job);
			}
			finally
			{
				Body.CurrentCell?.RemoveObject(Body);
				if (origin != null) Place(Body, origin);
				Require(Body.CurrentCell == origin && Body.IDIfAssigned == id,
					"protected handover occupant was not restored");
			}
		}

		internal static void After(bool Witness, bool Accepted, string Failure, GameObject Owner,
			GameObject Target, r_KingdomImprovement Intent, KingdomArchitectureIntent Layout,
			KingdomConstructionJob Job)
		{
			if (!Witness) return;
			Require(Accepted, "late resident clearance refused: " + Failure);
			Require(GameObject.Validate(Resident) && Resident.IsAlive && Resident.IDIfAssigned == ResidentId
				&& Resident.CurrentZone == Owner.CurrentZone && Resident.CurrentCell != Blocked
				&& !Layout.Rect.Contains(Resident.CurrentCell.X, Resident.CurrentCell.Y)
				&& KingdomCitizenship.BelongsTo(The.Game.GetSystem<KingdomSystem>(), Resident),
				"handover did not preserve and stand its resident off the successor plot");
			Unchanged(Owner, Target, Intent, Job);
			Require(KingdomArchitectureStamper.TryProveEnvelopeGrowth(The.Game.GetSystem<KingdomSystem>(),
				Owner.CurrentZone, Owner, Target, Layout, true, out string failure), failure);
			Require(KingdomArchitectureRuntime.TryRead(Owner, out var before, out failure), failure);
			Require(KingdomArchitectureStamper.TryProveRenovationOccupants(The.Game.GetSystem<KingdomSystem>(),
				Owner.CurrentZone, before, Layout, false, out _, out failure), failure);
			Proved = true;
			Record(Renovation ? "camp-heart-chain-renovation-cleared" : "camp-heart-chain-handover-cleared", "post-payment-resident=true; same-body=true"
				+ "; citizenship-retained=true; outside-successor=true; strict-ground=true; scope-restored=true; no-debit=true"
				+ "; same-paid-job=true; job=" + JobId + "; resident=" + ResidentId);
		}

		private static void Unchanged(GameObject Owner, GameObject Target,
			r_KingdomImprovement Intent, KingdomConstructionJob Job)
		{
			Require(The.Game.TimeTicks == Tick && ReferenceEquals(KingdomSurvey.ActiveFor(Owner.CurrentZone), Survey)
				&& KingdomConstruction.TryFind(JobId, out var retained)
				&& retained != null && KingdomConstruction.IsCurrent(Job)
				&& retained.SubjectId == Owner.IDIfAssigned && retained.OutputId == Target.IDIfAssigned
				&& retained.Claims.MaterialSpent == Materials && retained.Claims.WaterSpent == Water
				&& retained.Phase == Job.Phase && retained.PhysicalPhase == Job.PhysicalPhase,
				"handover probe changed time, payment or paid endpoints");
			Require(r_KingdomImprovement.VerifyHandoverContentCustody(Owner, Target, Owner.CurrentCell,
				Intent, true, out string failure), failure);
		}

		private static void Place(GameObject Body, Cell Cell)
		{
			Body.CurrentCell?.RemoveObject(Body);
			Require(ReferenceEquals(Cell.AddObject(Body, NoStack: true), Body) && Body.CurrentCell == Cell,
				"handover probe placement changed its exact body");
		}
		internal static bool Destination(ref Cell Result)
		{
			if (!DenyDestinations) return true;
			DestinationCalls++; Result = null; return false;
		}
		private static void Require(bool Value, string Failure)
			=> KingdomCampHeartNativeProvider.Require(Value, Failure);
		private static void Record(string Name, string Detail)
			=> Require(KingdomScenarioJournal.Append(Name, true, Detail) == null, "handover journal unavailable");
	}

	[HarmonyPatch(typeof(KingdomUpgrade), nameof(KingdomUpgrade.TryPrepareHandoverGround))]
	internal static class KingdomCampHeartChainHandoverOccupancyPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject Predecessor, GameObject Successor, string SuccessorKey,
			r_KingdomImprovement Intent, KingdomArchitectureIntent Layout, KingdomConstructionJob Job,
			out bool __state)
		{
			__state = false;
			try { __state = KingdomCampHeartChainHandoverOccupancy.Before(Predecessor, Successor,
				SuccessorKey, Intent, Layout, Job); }
			catch (Exception error) { KingdomCampHeartChainRetryFault.Fault(error); }
		}
		[HarmonyPostfix]
		internal static void Postfix(bool __state, bool __result, string Failure, GameObject Predecessor,
			GameObject Successor, r_KingdomImprovement Intent, KingdomArchitectureIntent Layout,
			KingdomConstructionJob Job)
		{
			try { KingdomCampHeartChainHandoverOccupancy.After(__state, __result, Failure,
				Predecessor, Successor, Intent, Layout, Job); }
			catch (Exception error) { KingdomCampHeartChainRetryFault.Fault(error); }
		}
	}

	[HarmonyPatch(typeof(KingdomPlots), "FreeGroundOffLayout")]
	internal static class KingdomCampHeartChainNoDestinationPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(ref Cell __result)
			=> KingdomCampHeartChainHandoverOccupancy.Destination(ref __result);
	}
}
