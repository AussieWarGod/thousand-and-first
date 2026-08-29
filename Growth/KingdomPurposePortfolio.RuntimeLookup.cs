using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPurposeZone(string ZoneId, out Zone Zone)
		{
			Zone = null;
			if (string.IsNullOrEmpty(ZoneId) || The.ZoneManager == null) return false;
			try { Zone = The.ZoneManager.GetZone(ZoneId); }
			catch { Zone = null; }
			return Zone != null && Zone.ZoneID == ZoneId;
		}

		private static bool TryOperationGround(KingdomPurposeOperationReceipt Operation,
			out Zone SourceZone, out GameObject Work, out GameObject Input,
			out GameObject Output, out Zone DestinationZone, out GameObject DestinationInput,
			out string Failure)
		{
			SourceZone = null;
			Work = null;
			Input = null;
			Output = null;
			DestinationZone = null;
			DestinationInput = null;
			Failure = null;
			if (Operation == null || !TryPurposeZone(Operation.SourceZoneId, out SourceZone)
				|| !TryPurposeZone(Operation.DestinationZoneId, out DestinationZone)
				|| FindExactKnown(SourceZone, Operation.SourceWorkId, out Work)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactKnown(SourceZone, Operation.SourceInputStoreId, out Input)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactKnown(SourceZone, Operation.SourceOutputStoreId, out Output)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactKnown(DestinationZone, Operation.DestinationInputStoreId,
					out DestinationInput) != KingdomPhysicalLookupState.Exact)
				return Fail("The exact purpose work, zone, or dedicated store is unavailable.",
					out Failure);
			if (Work.GetIntProperty("KingdomBuilt") != 1
				|| Work.GetIntProperty("KingdomStaffed") != 1
				|| Work.GetIntProperty("KingdomEffectiveness") <= 0
				|| Work.GetIntProperty("KingdomBrownout") == 1
				|| KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Work)) <= 0)
				return Fail("The exact purpose work is unstaffed, unpowered, or worn out.", out Failure);
			if (!KingdomMaterials.IsStockpile(Input) || Input.Inventory == null
				|| !KingdomMaterials.IsStockpile(Output) || Output.Inventory == null
				|| !KingdomMaterials.IsStockpile(DestinationInput)
				|| DestinationInput.Inventory == null || ReferenceEquals(Input, Output))
				return Fail("The frozen purpose input/output store dedication changed.", out Failure);
			if (!PurposeSourceBindingMatches(Operation, SourceZone, Work, Input, Output,
				out Failure) || !PurposeDestinationBindingMatches(Operation, DestinationZone,
				DestinationInput, out Failure)) return false;
			return true;
		}

		private static bool PurposeSourceBindingMatches(
			KingdomPurposeOperationReceipt Operation, Zone Zone, GameObject Work,
			GameObject Input, GameObject Output, out string Failure)
		{
			Failure = null;
			if (!TryAuthoredPurposeStores(Zone, Work, out GameObject authoredInput,
				out GameObject authoredOutput, out bool declared, out Failure)) return false;
			if (declared)
				return (ReferenceEquals(authoredInput, Input)
					&& ReferenceEquals(authoredOutput, Output))
					|| Fail("The operation stores no longer answer their authored roles.",
						out Failure);
			if (!TryFrozenPurposeStores(Zone, Operation.SourceInputStoreId,
				Operation.SourceOutputStoreId, out GameObject frozenInput,
				out GameObject frozenOutput, out Failure)) return false;
			return (ReferenceEquals(frozenInput, Input) && ReferenceEquals(frozenOutput, Output))
				|| Fail("The legacy operation stores changed after pair freeze.", out Failure);
		}

		private static bool PurposeDestinationBindingMatches(
			KingdomPurposeOperationReceipt Operation, Zone Zone, GameObject Input,
			out string Failure)
		{
			Failure = null;
			if (string.IsNullOrEmpty(Operation.DestinationWorkId))
				return ExactPurposeStore(KingdomMaterials.Stock(Zone), Input)
					|| Fail("The provisional destination input lost exact stock custody.",
						out Failure);
			if (FindExactKnown(Zone, Operation.DestinationWorkId, out GameObject work)
					!= KingdomPhysicalLookupState.Exact
				|| work.GetIntProperty("KingdomBuilt") != 1
				|| !KingdomPurposePortfolioRules.TryBuildKind(
					KingdomUpgrade.DesignKeyOf(work), out KingdomPurposeKind kind)
				|| kind != Operation.DestinationKind)
				return Fail("The exact destination purpose root is unavailable.", out Failure);
			if (!TryAuthoredPurposeStores(Zone, work, out GameObject authoredInput,
				out _, out bool declared, out Failure)) return false;
			if (declared && !ReferenceEquals(authoredInput, Input))
				return Fail("The destination input no longer answers its authored role.",
					out Failure);
			return ExactPurposeStore(KingdomMaterials.Stock(Zone), Input)
				|| Fail("The destination input lost exact stock custody.", out Failure);
		}

		private static KingdomPhysicalLookupState FindPortfolioObject(string Id,
			out GameObject Object, out bool Graveyard)
		{
			Object = null;
			Graveyard = false;
			if (string.IsNullOrEmpty(Id) || The.ZoneManager == null)
				return KingdomPhysicalLookupState.Ambiguous;
			HashSet<GameObject> found = new HashSet<GameObject>();
			HashSet<Zone> zones = new HashSet<Zone>();
			if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
			if (The.ZoneManager.CachedZones != null)
				foreach (Zone zone in The.ZoneManager.CachedZones.Values)
					if (zone != null) zones.Add(zone);
			foreach (Zone zone in zones)
			{
				GameObject candidate;
				KingdomPhysicalLookupState state = FindExactKnown(zone, Id, out candidate);
				if (state == KingdomPhysicalLookupState.Ambiguous)
					return KingdomPhysicalLookupState.Ambiguous;
				if (state == KingdomPhysicalLookupState.Exact) found.Add(candidate);
			}
			if (The.ZoneManager.Graveyard?.Objects != null)
				for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
				{
					GameObject candidate = The.ZoneManager.Graveyard.Objects[i];
					if (candidate != null && candidate.IDIfAssigned == Id) found.Add(candidate);
				}
			if (found.Count == 0) return KingdomPhysicalLookupState.Absent;
			if (found.Count != 1) return KingdomPhysicalLookupState.Ambiguous;
			foreach (GameObject candidate in found) Object = candidate;
			Graveyard = Object != null && Object.IsInGraveyard();
			return KingdomPhysicalLookupState.Exact;
		}
	}
}
