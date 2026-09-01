using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private static bool TryProveExactIdentity(KingdomSystem System, GameObject Body,
			int ResidentId, bool Destructive,
			KingdomSuccessionResidentAuthority Succession)
		{
			string objectId = Body?.IDIfAssigned;
			string zoneId = Body?.CurrentZone?.ZoneID;
			string name = Body?.GetStringProperty("KingdomName");
			if (System?.Bindings == null || !GameObject.Validate(Body) || !Body.IsAlive
				|| ResidentId <= 0 || Body.GetIntProperty(KingdomResidents.ResidentIdProperty)
					!= ResidentId
				|| string.IsNullOrEmpty(objectId) || Body.CurrentCell == null
				|| string.IsNullOrEmpty(zoneId) || !System.OwnedZone(zoneId)
				|| Body.GetIntProperty("KingdomBorn") != 1 || string.IsNullOrEmpty(name)
				|| Destructive && (Body.IsPlayer() || Body.IsPlayerLed()
					|| Body.GetIntProperty("VillageMerchant") != 0)
				|| !Destructive && Body.IsPlayer() && !Succession.AccessionOwner
				|| !Destructive && Body.IsPlayerLed() && !Body.IsPlayer()) return false;
			if (!TryProveCitizenship(System, Body, Succession, Destructive)) return false;

			bool allowMissing = !Destructive && Succession.RepairOwner;
			if (!KingdomResidents.TryProveResidentTransitionRow(System, Body, ResidentId,
				zoneId, name,
				allowMissing, Succession.RepairSettlementId, Succession.RepairName,
				out int rows)
				|| !TryProveResidentBinding(System.Bindings, ResidentId, objectId, zoneId,
					out int bindings)) return false;
			return KingdomResidentTransitionRules.ExactCarrierMultiplicity(rows, bindings,
				allowMissing);
		}

		private static bool TryProveResidentBinding(KingdomBindingRegistry Registry,
			int ResidentId, string ObjectId, string ZoneId, out int Matches)
		{
			Matches = 0;
			if (Registry?.Keys == null || Registry.Kinds == null || Registry.ZoneIds == null
				|| Registry.ObjectIds == null || Registry.MintedTicks == null) return false;
			int count = Registry.Keys.Count;
			if (Registry.Kinds.Count != count || Registry.ZoneIds.Count != count
				|| Registry.ObjectIds.Count != count || Registry.MintedTicks.Count != count
				|| count > KingdomBindingTable.MaxResidentBindings
					+ KingdomBindingTable.MaxTransientBindings) return false;
			HashSet<long> seen = new HashSet<long>();
			int residents = 0, transients = 0;
			for (int i = 0; i < count; i++)
			{
				int key = Registry.Keys[i], kind = Registry.Kinds[i];
				if (key == 0 || kind < (int)KingdomBindingKind.Resident
					|| kind > (int)KingdomBindingKind.Transient
					|| Registry.MintedTicks[i] < 0L
					|| !seen.Add(((long)kind << 32) | (uint)key)) return false;
				if (kind == (int)KingdomBindingKind.Resident) residents++; else transients++;
				if (kind != (int)KingdomBindingKind.Resident || key != ResidentId) continue;
				Matches++;
				if (!string.Equals(Registry.ObjectIds[i], ObjectId, StringComparison.Ordinal)
					|| !string.Equals(Registry.ZoneIds[i], ZoneId,
						StringComparison.Ordinal)) return false;
			}
			return residents <= KingdomBindingTable.MaxResidentBindings
				&& transients <= KingdomBindingTable.MaxTransientBindings;
		}

		private static bool TryProveCitizenship(KingdomSystem System, GameObject Body,
			KingdomSuccessionResidentAuthority Succession, bool Destructive)
		{
			if (KingdomCitizenship.BelongsTo(System, Body)) return true;
			if ((!Succession.RepairOwner && !Destructive) || Body?.Brain == null
				|| Body.GetIntProperty("KingdomCitizen") == 1) return false;
			r_KingdomCitizenship receipt = Body.GetPart<r_KingdomCitizenship>();
			int expectedReason = Destructive
				? (int)KingdomCitizenshipRemovalReason.Emigration
				: (int)KingdomCitizenshipRemovalReason.Accession;
			if (receipt == null
				|| receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion
				|| receipt.Phase != KingdomCitizenshipPhase.Removed
				|| receipt.RemovalReason != expectedReason
				|| !KingdomCitizenshipRules.ValidReceiptShape(receipt.Phase,
					receipt.PriorKind, receipt.AppliedValue, receipt.EnrollmentReason,
					receipt.RemovalReason, receipt.AppliedTick, receipt.RemovedTick)
				|| !string.Equals(receipt.BodyObjectId, Body.IDIfAssigned,
					StringComparison.Ordinal)
				|| !string.Equals(receipt.OwnerRealmId, System.CurrentRealmId,
					StringComparison.Ordinal)
				|| !Destructive && !string.Equals(receipt.OwnerSettlementId,
					Succession.RepairSettlementId, StringComparison.Ordinal)
				|| Destructive && !string.Equals(receipt.OwnerSettlementId,
					System.SettlementIdForOwnedZone(Body.CurrentZone.ZoneID),
					StringComparison.Ordinal)
				|| !string.Equals(receipt.FactionId, System.KingdomFactionName,
					StringComparison.Ordinal)) return false;
			var allegiance = Body.Brain.GetBaseAllegiance();
			int value = 0;
			bool present = allegiance != null
				&& allegiance.TryGetValue(receipt.FactionId, out value);
			return allegiance != null && KingdomCitizenshipRules.MatchesRemovalPost(
				receipt.PriorKind, receipt.PriorValue, present, value);
		}
	}
}
