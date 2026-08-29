using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		private const string ConstructionInputRetirementPrefix =
			"$ThousandAndFirst_ConstructionInputRetirement_";

		private static bool TryRetireActiveConstructionInputCarrier(KingdomSystem system,
			string owner, int tripId, Zone zone, int x, int y, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			if (!ActiveZone(zone) || system?.Bindings == null
				|| ConstructionInputTransitRootExists(owner, tripId)) return false;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (!system.Bindings.TryRead(out bindings, out fault)) return false;
			bool held = bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding);
			if (held && binding.ZoneId != zone.ZoneID)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomPhysicalLookupState stateLookup = LookupRetirement(owner, tripId,
				out string state);
			if (stateLookup == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (!held)
			{
				if (!ExactRetirementMarker(owner, tripId, out string _)
					|| CountActiveTripBodies(zone, tripId, null, out _) != 0)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				fault = KingdomCityFault.None; return true;
			}
			string id = binding.ObjectId;
			if (string.IsNullOrEmpty(id)) return false;
			GameObject body = KingdomSurvey.ActiveFor(zone)
				.FindBoundBody(id, KingdomBindingKind.Transient);
			if (GameObject.Validate(body))
			{
				if (!ExactCancellationBody(body, id, tripId) || body.CurrentZone != zone
					|| body.CurrentCell != zone.GetCell(x, y)
					|| !KingdomOrdinaryCustody.TryProveEmpty(body, out string _))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				if (state == null)
				{
					if (!PublishRetirement(owner, tripId, RetirementIntent(tripId, id))) return false;
					state = RetirementIntent(tripId, id);
				}
				if (state != RetirementIntent(tripId, id))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				bool removed = false;
				try { removed = body.Obliterate(null, Silent: true); } catch { }
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, body);
				if (!removed || GameObject.Validate(body)
					|| CountActiveTripBodies(zone, tripId, id, out _) != 0
					|| !ExactRetiredCarrierEvidence(id, tripId, body)
					|| !PublishRetirement(owner, tripId, RetiredState(tripId, id)))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				fault = KingdomCityFault.None; return true;
			}
			if (CountActiveTripBodies(zone, tripId, id, out _) != 0)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (state == RetirementIntent(tripId, id))
			{
				if (!ExactRetiredCarrierEvidence(id, tripId, null)
					|| !PublishRetirement(owner, tripId, RetiredState(tripId, id))) return false;
				fault = KingdomCityFault.None; return true;
			}
			if (state != RetiredState(tripId, id)
				|| !KingdomResidents.Unbind(system, tripId, KingdomBindingKind.Transient,
					KingdomUnbindCause.JobClosed))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		private static bool RetirementSettled(KingdomSystem system, string owner,
			int tripId, Zone active)
		{
			KingdomBindingTable bindings;
			return ActiveZone(active) && ExactRetirementMarker(owner, tripId, out string _)
				&& system?.Bindings != null && system.Bindings.TryRead(out bindings, out _)
				&& !bindings.Holds(tripId, KingdomBindingKind.Transient)
				&& !ConstructionInputTransitRootExists(owner, tripId)
				&& CountActiveTripBodies(active, tripId, null, out _) == 0;
		}

		private static bool ExactRetiredCarrierEvidence(string objectId, int tripId,
			GameObject expected)
		{
			if (string.IsNullOrEmpty(objectId) || The.ZoneManager?.Graveyard?.Objects == null)
				return false;
			GameObject exact = null;
			int count = 0;
			for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
			{
				GameObject candidate = The.ZoneManager.Graveyard.Objects[i];
				if (candidate?.IDIfAssigned != objectId) continue;
				count++; exact = candidate;
			}
			return count == 1 && (expected == null || ReferenceEquals(expected, exact))
				&& exact.Inventory != null
				&& exact.GetIntProperty(KingdomResidents.JobIdProperty) == tripId
				&& exact.GetPart<r_KingdomPorter>()?.JobId == tripId
				&& !exact.IsPlayer() && !exact.IsPlayerLed()
				&& ExactGraveyardCarrierEmpty(exact);
		}

		private static bool ExactGraveyardCarrierEmpty(GameObject carrier)
		{
			try
			{
				System.Collections.Generic.IList<GameObject> contents =
					carrier?.GetContents(new System.Collections.Generic.List<GameObject>());
				return contents != null && contents.Count == 0;
			}
			catch { return false; }
		}

		private static string ReadRetirement(string owner, int tripId)
		{
			string key = RetirementKey(owner, tripId);
			return key != null && The.Game?.ObjectGameState != null
				&& The.Game.ObjectGameState.TryGetValue(key, out object value) ? value as string : null;
		}

		private static KingdomPhysicalLookupState LookupRetirement(string owner, int tripId,
			out string state)
		{
			state = null;
			string key = RetirementKey(owner, tripId);
			if (key == null || The.Game?.ObjectGameState == null)
				return KingdomPhysicalLookupState.Ambiguous;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object value))
				return KingdomPhysicalLookupState.Absent;
			state = value as string;
			return string.IsNullOrEmpty(state) ? KingdomPhysicalLookupState.Ambiguous
				: KingdomPhysicalLookupState.Exact;
		}

		private static bool PublishRetirement(string owner, int tripId, string value)
		{
			string key = RetirementKey(owner, tripId);
			if (key == null || string.IsNullOrEmpty(value) || The.Game?.ObjectGameState == null)
				return false;
			The.Game.SetObjectGameState(key, value);
			return ReadRetirement(owner, tripId) == value;
		}

		private static bool ClearRetirement(string owner, int tripId)
		{
			string key = RetirementKey(owner, tripId);
			if (key == null || The.Game?.ObjectGameState == null) return false;
			The.Game.ObjectGameState.Remove(key);
			return !The.Game.ObjectGameState.ContainsKey(key);
		}

		internal static bool TryClearConstructionInputRetirement(KingdomSystem system,
			string owner, KingdomConstructionInputReceipt receipt, int tripId)
		{
			if (system?.Jobs == null || system.Bindings == null || receipt == null
				|| receipt.ConstructionJobId != owner
				|| !KingdomConstructionInputRules.IsTerminal(receipt)) return false;
			int childMatches = 0;
			for (int i = 0; i < receipt.ChildCount; i++)
				if (receipt.ChildAt(i).TripId == tripId) childMatches++;
			if (childMatches != 1 || !system.Jobs.TryRead(out KingdomJobTable jobs, out _)
				|| !system.Bindings.TryRead(out KingdomBindingTable bindings, out _)
				|| bindings.Holds(tripId, KingdomBindingKind.Transient)
				|| LookupTransitRoot(owner, tripId, out GameObject _)
					!= KingdomPhysicalLookupState.Absent) return false;
			for (int i = 0; i < jobs.Count; i++)
				if (jobs.TryAt(i, out KingdomJobRow row)
					&& row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& (row.DeliveryOwnerOperationId == owner
						|| row.DeliveryTripId == tripId)) return false;
			KingdomPhysicalLookupState lookup = LookupRetirement(owner, tripId, out string _);
			return lookup == KingdomPhysicalLookupState.Absent
				|| lookup == KingdomPhysicalLookupState.Exact
				&& (ExactUnprojectedMarker(owner, tripId)
					|| ExactRetirementMarker(owner, tripId, out string _))
				&& ClearRetirement(owner, tripId);
		}

		private static bool ExactRetirementMarker(string owner, int tripId, out string objectId)
		{
			objectId = null;
			if (LookupRetirement(owner, tripId, out string state)
				!= KingdomPhysicalLookupState.Exact) return false;
			string prefix = "retired:" + tripId.ToString(
				System.Globalization.CultureInfo.InvariantCulture) + ":";
			if (state == null || !state.StartsWith(prefix, System.StringComparison.Ordinal)
				|| state.Length == prefix.Length) return false;
			objectId = state.Substring(prefix.Length);
			return state == RetiredState(tripId, objectId)
				&& ExactRetiredCarrierEvidence(objectId, tripId, null);
		}

		private static string RetirementIntent(int tripId, string objectId)
		{
			return "intent:" + tripId.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ":" + objectId;
		}

		private static string RetiredState(int tripId, string objectId)
		{
			return "retired:" + tripId.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ":" + objectId;
		}

		internal static bool AnyConstructionInputAuthorityExists(KingdomSystem system)
		{
			KingdomJobTable jobs;
			if (system?.Jobs == null || !system.Jobs.TryRead(out jobs, out _)
				|| The.Game?.ObjectGameState == null || The.Game.ObjectGameState.Count > 65536)
				return true;
			for (int i = 0; i < jobs.Count; i++)
				if (jobs.TryAt(i, out KingdomJobRow row)
					&& row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput) return true;
			if (AnyUnattributedTransientBinding(system, jobs)) return true;
			foreach (System.Collections.Generic.KeyValuePair<string, object> state
				in The.Game.ObjectGameState)
				if (state.Key != null && (state.Key.StartsWith(ConstructionInputTransitPrefix,
						System.StringComparison.Ordinal)
					|| state.Key.StartsWith(ConstructionInputRetirementPrefix,
						System.StringComparison.Ordinal))) return true;
			return false;
		}

		private static string RetirementKey(string owner, int tripId)
		{
			return string.IsNullOrEmpty(owner) || tripId <= 0 ? null
				: ConstructionInputRetirementPrefix + owner + "_" + tripId;
		}

		internal static bool ConstructionInputOwnerAuthorityExists(KingdomSystem system,
			string owner, KingdomConstructionInputReceipt receipt)
		{
			KingdomJobTable jobs;
			KingdomBindingTable bindings;
			if (system?.Jobs == null || system.Bindings == null || receipt == null
				|| !system.Jobs.TryRead(out jobs, out _) || !system.Bindings.TryRead(out bindings, out _))
				return true;
			for (int i = 0; i < jobs.Count; i++)
				if (jobs.TryAt(i, out KingdomJobRow row)
					&& row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ConstructionInput
					&& row.DeliveryOwnerOperationId == owner) return true;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				int trip = receipt.ChildAt(i).TripId;
				if (bindings.Holds(trip, KingdomBindingKind.Transient)
					|| ConstructionInputTransitRootExists(owner, trip)
					|| LookupRetirement(owner, trip, out string _)
						!= KingdomPhysicalLookupState.Absent) return true;
			}
			return false;
		}
	}
}
