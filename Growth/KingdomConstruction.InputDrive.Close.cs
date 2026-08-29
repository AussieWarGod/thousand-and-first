using System;

using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool CloseAndCommitInput(KingdomSystem system,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				KingdomCityFault central;
				if (!KingdomCentralLogistics.TryCloseConstructionInputTrip(system,
					job.Id, child.TripId, receipt.Schema, receipt.PlanDigest,
					receipt.Revision, true,
					The.ZoneManager == null ? null : The.ZoneManager.ActiveZone, out central))
				{
					failure = "The routed construction carrier could not close ("
						+ central + ").";
					return false;
				}
			}
			KingdomConstructionInputReceipt committed;
			KingdomConstructionInputFault inputFault = KingdomConstructionInputFault.None;
			KingdomConstructionClaims claims;
			if (!KingdomConstructionInputRules.TryCommittedClaims(receipt,
				job.Claims, out claims)
				|| !KingdomConstructionInputRules.TryTransitionTransaction(receipt,
					receipt.Revision, KingdomConstructionInputTxPhase.Closing,
					KingdomConstructionInputTxPhase.Committed, out committed, out inputFault))
			{
				failure = "The physically closed routed input cannot produce its exact claim ("
					+ inputFault + ").";
				return false;
			}
			long now = The.Game == null ? job.UpdatedTick : The.Game.TimeTicks;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(job,
				KingdomConstructionPhase.Funded, now);
			next.Claims = claims;
			if (!KingdomConstructionRules.UpdateInputReceipt(ref next, committed)
				|| !TryUpdate(next, out failure)) return false;
			KingdomConstructionJob routeAuthority = job;
			job = next;
			for (int i = 0; i < committed.ChildCount; i++)
				KingdomCentralLogistics.TryClearConstructionInputRetirement(system, job.Id,
					committed,
					committed.ChildAt(i).TripId);
			ReleaseInputRemainders(routeAuthority, committed,
				The.ZoneManager == null ? null : The.ZoneManager.ActiveZone);
			return true;
		}

		/// <summary>Releases only markers owned by this terminal receipt. The original taken
		/// object also carries a marker while in route custody and must become ordinary stock
		/// again after cancellation; unrelated NeverStack policy is never removed.</summary>
		private static void ReleaseInputRemainders(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, Zone zone)
		{
			if (receipt == null || zone == null || KingdomSurvey.ActiveFor(zone) == null) return;
			for (int i = 0; i < receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine line = receipt.SourceAt(i);
				if (line.SourceZoneId != zone.ZoneID) continue;
				ReleaseInputLineMarkers(job, receipt, line, zone);
			}
		}

		private static bool ReleaseInputLineMarkers(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine line,
			Zone zone)
		{
			if (job == null || receipt == null || line == null || zone == null
				|| line.SourceZoneId != zone.ZoneID || KingdomSurvey.ActiveFor(zone) == null) return false;
			if (line.Kind == KingdomConstructionInputKind.Water) return true;
			KingdomConstructionInputCargoLine cargo = receipt.CargoAt(line.CargoOrdinal);
			if (FindExactId(zone, line.HolderId, out GameObject holder)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactId(zone, line.SourceObjectId, out GameObject source)
					!= KingdomPhysicalLookupState.Exact) return false;
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(source);
			int routePolicy = protectedCargo ? -1 : 1;
			int cleanPolicy = protectedCargo ? -1 : 0;
			if (!ExactRoutedMaterialAtHolder(zone, holder, source, job, receipt, line,
				cargo, line.Before, null, cleanPolicy))
			{
				if (!ExactRoutedMaterialAtHolder(zone, holder, source, job, receipt, line,
					cargo, line.Before, cargo.CargoKey, routePolicy)) return false;
				if (!ExactRoutedMaterialAtHolder(zone, holder, source, job, receipt, line,
					cargo, line.Before, cargo.CargoKey, routePolicy)) return false;
				source.RemoveStringProperty(InputMarkerProperty);
				if (!protectedCargo) source.RemoveIntProperty("NeverStack");
				KingdomSurvey.ObserveChangedInActive(zone, holder);
			}
			if (!ExactRoutedMaterialAtHolder(zone, holder, source, job, receipt, line,
				cargo, line.Before, null, cleanPolicy)) return false;
			if (string.IsNullOrEmpty(line.RemainderObjectId)) return true;
			GameObject remainder;
			KingdomPhysicalLookupState remainderState = FindGlobalInputId(receipt,
				line.RemainderObjectId, out remainder, out bool graveyard);
			if (remainderState == KingdomPhysicalLookupState.Exact && graveyard) return true;
			if (remainderState != KingdomPhysicalLookupState.Exact
				|| FindExactId(zone, line.HolderId, out holder)
					!= KingdomPhysicalLookupState.Exact) return false;
			if (ExactRoutedSplitRemainderState(zone, holder, job, receipt, line,
				remainder, line.RemainderMarker, 1))
			{
				if (!ExactRoutedSplitRemainderState(zone, holder, job, receipt, line,
					remainder, line.RemainderMarker, 1)) return false;
				remainder.RemoveStringProperty(InputMarkerProperty);
				remainder.RemoveIntProperty("NeverStack");
				KingdomSurvey.ObserveChangedInActive(zone, holder);
			}
			return ExactRoutedSplitRemainderState(zone, holder, job, receipt, line,
				remainder, null, 0);
		}

		private static void ReleaseTerminalInputRemaindersOnActiveZone(
			KingdomSystem system, Zone zone)
		{
			if (system == null || zone == null || KingdomSurvey.ActiveFor(zone) == null
				|| !TryRead(out System.Collections.Generic.List<KingdomConstructionJob> jobs,
					out string _)) return;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob job = jobs[i];
				if (job == null || string.IsNullOrEmpty(job.InputReceipt)
					|| !KingdomConstructionRules.TryGetInputReceipt(job,
						out KingdomConstructionInputReceipt receipt)
					|| !KingdomConstructionInputRules.IsTerminal(receipt)) continue;
				ReleaseInputRemainders(job, receipt, zone);
			}
		}
	}
}
