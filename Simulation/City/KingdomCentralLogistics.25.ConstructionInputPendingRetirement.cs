using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Retires an exact rooted/bound carrier, adopts one exact pre-bind mint,
		/// or durably proves that an activated row never projected a body. No branch mints.</summary>
		internal static bool TryRetireConstructionInputCancellationSource(
			KingdomSystem system, string owner, int schema, string digest, long revision,
			int jobId, int tripId, KingdomConstructionJob ownerJob,
			KingdomConstructionInputReceipt receipt, int childOrdinal, Zone source,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomConstructionInputChild child = receipt != null && childOrdinal >= 0
				&& childOrdinal < receipt.ChildCount ? receipt.ChildAt(childOrdinal) : null;
			if (!ExactCancellationRetirementRow(system, owner, schema, digest, revision,
				jobId, tripId, ownerJob, receipt, child, source, out KingdomJobRow row,
				out KingdomBindingTable bindings, out fault)) return false;
			KingdomPhysicalLookupState root = LookupTransitRoot(owner, tripId,
				out GameObject _);
			if (root == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (bindings.Holds(tripId, KingdomBindingKind.Transient))
			{
				if (root == KingdomPhysicalLookupState.Exact
					&& !TryMaterializeConstructionInputCancellationSource(system, owner,
						schema, digest, revision, jobId, tripId, ownerJob, receipt,
						childOrdinal, source, out GameObject _, out fault)) return false;
				return TryRetireActiveConstructionInputCarrier(system, owner, tripId,
					source, row.DeliverySourceX, row.DeliverySourceY, out fault);
			}
			if (root != KingdomPhysicalLookupState.Absent)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			int bodies = CountExactPendingCancellationBodies(source, tripId,
				row.DeliverySourceX, row.DeliverySourceY, out GameObject pending);
			if (ExactRetirementMarker(owner, tripId, out string _))
			{
				if (bodies != 0) { fault = KingdomCityFault.DuplicateBinding; return false; }
				fault = KingdomCityFault.None; return true;
			}
			bool neverProjected = ExactNeverProjectedCancellation(receipt, child, row)
				&& CountGraveyardTripBodies(tripId) == 0;
			if (ExactUnprojectedMarker(owner, tripId))
			{
				if (!neverProjected || bodies != 0)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				fault = KingdomCityFault.None; return true;
			}
			if (LookupRetirement(owner, tripId, out string _)
				!= KingdomPhysicalLookupState.Absent || !neverProjected
				|| bodies < 0 || bodies > 1)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (bodies == 0)
			{
				if (!PublishRetirement(owner, tripId, UnprojectedState(tripId))
					|| !ExactUnprojectedMarker(owner, tripId)) return false;
				fault = KingdomCityFault.None; return true;
			}
			if (!GameObject.Validate(pending)
				|| !KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
					source.ZoneID, pending, The.Game == null ? 0L : The.Game.TimeTicks))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			return TryRetireActiveConstructionInputCarrier(system, owner, tripId,
				source, row.DeliverySourceX, row.DeliverySourceY, out fault);
		}

		internal static bool ConstructionInputCarrierCustodyExists(KingdomSystem system,
			string owner, int tripId)
		{
			if (LookupTransitRoot(owner, tripId, out GameObject _)
				!= KingdomPhysicalLookupState.Absent) return true;
			if (system?.Bindings == null || system.Jobs == null
				|| !system.Bindings.TryRead(out KingdomBindingTable bindings, out _)
				|| !system.Jobs.TryRead(out KingdomJobTable jobs, out _)) return true;
			if (bindings.Holds(tripId, KingdomBindingKind.Transient)) return true;
			int matches = 0;
			KingdomJobRow exact = default(KingdomJobRow);
			for (int i = 0; i < jobs.Count; i++)
				if (jobs.TryAt(i, out KingdomJobRow row)
					&& row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& row.DeliveryOwnerOperationId == owner
					&& row.DeliveryTripId == tripId) { matches++; exact = row; }
			if (matches != 1) return matches != 0;
			if (exact.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared) return false;
			return !ExactUnprojectedMarker(owner, tripId)
				&& !ExactRetirementMarker(owner, tripId, out string _);
		}

		private static bool ExactCancellationRetirementRow(KingdomSystem system,
			string owner, int schema, string digest, long revision, int jobId, int tripId,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputChild child, Zone source, out KingdomJobRow row,
			out KingdomBindingTable bindings, out KingdomCityFault fault)
		{
			row = default(KingdomJobRow); bindings = default(KingdomBindingTable);
			fault = KingdomCityFault.NullArgument;
			return ActiveZone(source) && system?.Jobs != null && system.Bindings != null
				&& ownerJob != null && ownerJob.Id == owner && receipt != null
				&& receipt.ConstructionJobId == owner && receipt.Schema == schema
				&& receipt.PlanDigest == digest && child != null && child.JobId == jobId
				&& child.TripId == tripId && system.Jobs.TryRead(out KingdomJobTable jobs,
					out fault) && jobs.TryGet(jobId, out row) && TripRows(jobs, tripId).Count == 1
				&& ConstructionTransitRow(row, owner, jobId, tripId)
				&& row.SourceZoneId == source.ZoneID
				&& row.DeliveryOwnerManifestVersion == schema
				&& row.DeliveryOwnerManifestDigest == digest
				&& row.DeliveryOwnerManifestRevision <= revision
				&& row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
				&& system.Bindings.TryRead(out bindings, out fault);
		}

		private static int CountExactPendingCancellationBodies(Zone source, int tripId,
			int x, int y, out GameObject exact)
		{
			exact = null;
			if (!ActiveZone(source) || !KingdomSurvey.ActiveFor(source).TryLoaded(
				out System.Collections.Generic.IList<GameObject> loaded)) return -1;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject body = loaded[i];
				r_KingdomPorter part = body?.GetPart<r_KingdomPorter>();
				int stamped = body?.GetIntProperty(KingdomResidents.JobIdProperty) ?? 0;
				if (stamped != tripId && part?.JobId != tripId) continue;
				count++;
				if (count == 1 && GameObject.Validate(body) && body.IsAlive
					&& !body.IsPlayer() && !body.IsPlayerLed()
					&& stamped == tripId && part?.JobId == tripId
					&& body.Blueprint == KingdomGrowth.DefaultSettlerBlueprint
					&& body.CurrentZone == source && body.CurrentCell == source.GetCell(x, y)
					&& KingdomOrdinaryCustody.TryProveEmpty(body, out string _)) exact = body;
			}
			if (count != 1 || exact == null) exact = null;
			return count;
		}

		private static bool ExactNeverProjectedCancellation(
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputChild child,
			KingdomJobRow row)
		{
			if (receipt == null || child == null
				|| row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
				|| child.CentralPhase != (int)KingdomDeliveryPhase.ReservationPrepared
				|| child.CargoCount < 1 || child.CargoStart < 0
				|| child.CargoStart + child.CargoCount > receipt.CargoCount) return false;
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				if (cargo == null || cargo.Phase != KingdomConstructionInputCargoPhase.Planned
					|| cargo.SourceLineOrdinal < 0
					|| cargo.SourceLineOrdinal >= receipt.SourceCount
					|| receipt.SourceAt(cargo.SourceLineOrdinal).Phase
						!= KingdomConstructionInputSourcePhase.Reserved) return false;
			}
			return true;
		}

		private static int CountGraveyardTripBodies(int tripId)
		{
			if (The.ZoneManager?.Graveyard?.Objects == null
				|| The.ZoneManager.Graveyard.Objects.Count > 1024) return -1;
			int count = 0;
			for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
			{
				GameObject body = The.ZoneManager.Graveyard.Objects[i];
				if (body?.GetIntProperty(KingdomResidents.JobIdProperty) == tripId
					|| body?.GetPart<r_KingdomPorter>()?.JobId == tripId) count++;
			}
			return count;
		}

		private static bool ExactUnprojectedMarker(string owner, int tripId)
		{
			return LookupRetirement(owner, tripId, out string state)
				== KingdomPhysicalLookupState.Exact && state == UnprojectedState(tripId)
				&& CountGraveyardTripBodies(tripId) == 0;
		}

		private static string UnprojectedState(int tripId)
		{
			return "unprojected:" + tripId.ToString(
				System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
