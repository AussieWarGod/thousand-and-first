using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryHardenPhysicalFirstGuest(GameObject body,
			KingdomGrowthArrivalCandidate candidate, out string failure)
		{
			failure = null;
			if (!ExactFirstGuestBodyIdentity(body, candidate))
				return FailFirstGuest("first-guest body identity is not exact", out failure);
			r_KingdomFirstGuestBody existing = body.GetPart<r_KingdomFirstGuestBody>();
			if (existing != null) return ExactPhysicalFirstGuestHardening(body, candidate, existing)
				|| FailFirstGuest("first-guest body hardening differs from its receipt", out failure);
			Brain brain = body.Brain;
			if (brain == null || brain.PartyLeader != null || brain.Allegiance == null)
				return FailFirstGuest("first-guest blueprint has unsafe party or mind state", out failure);
			if (!ClearPhysicalFirstGuestLoadout(body, out failure)) return false;
			Corpse corpse = body.GetPart<Corpse>();
			r_KingdomFirstGuestBody part = new r_KingdomFirstGuestBody
			{
				CandidateId = candidate.Id, OpportunityId = candidate.FirstGuest.OpportunityId,
				SettlementId = candidate.SettlementId, ObjectId = candidate.ObjectId,
				Marker = candidate.Marker, ZoneId = candidate.LodgingZoneId,
				OriginalBrainFlags = brain.Flags, OriginalAllegiance = brain.Allegiance,
				HadNoXP = body.HasIntProperty("NoXP"),
				OriginalNoXP = body.GetIntProperty("NoXP"),
				HadSuppressCorpseDrops = body.HasIntProperty("SuppressCorpseDrops"),
				OriginalSuppressCorpseDrops = body.GetIntProperty("SuppressCorpseDrops"),
				HadCorpse = corpse != null,
				OriginalCorpseChance = corpse?.CorpseChance ?? 0,
				OriginalBurntCorpseChance = corpse?.BurntCorpseChance ?? 0,
				OriginalVaporizedCorpseChance = corpse?.VaporizedCorpseChance ?? 0,
				OriginalBuildCorpseChance = corpse?.BuildCorpseChance ?? 0
			};
			if (!ReferenceEquals(body.AddPart(part), part))
				return FailFirstGuest("first-guest custody part could not attach", out failure);
			body.SetIntProperty("NoXP", 1); body.SetIntProperty("SuppressCorpseDrops", 1);
			if (corpse != null)
			{
				corpse.CorpseChance = 0; corpse.BurntCorpseChance = 0;
				corpse.VaporizedCorpseChance = 0; corpse.BuildCorpseChance = 0;
			}
			brain.Allegiance = new AllegianceSet { Calm = true };
			brain.Passive = true; brain.Mobile = false; brain.Staying = true;
			brain.Wanders = false; brain.WandersRandomly = false; brain.DoReequip = false;
			return ExactPhysicalFirstGuestHardening(body, candidate, part)
				|| FailFirstGuest("first-guest hardening did not settle exactly", out failure);
		}

		private static bool ClearPhysicalFirstGuestLoadout(GameObject body, out string failure)
		{
			failure = null;
			List<GameObject> items = body?.GetInventoryDirectAndEquipment();
			for (int i = 0; items != null && i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.IsNatural()) continue;
				if (!item.ForceUnequipAndRemove(Silent: true)
					|| !item.Obliterate(null, Silent: true) || GameObject.Validate(item))
					return FailFirstGuest("first-guest inherited loadout could not clear", out failure);
			}
			return PhysicalFirstGuestLoadoutEmpty(body)
				|| FailFirstGuest("first-guest retained non-natural gear", out failure);
		}

		private static bool PhysicalFirstGuestLoadoutEmpty(GameObject body)
		{
			List<GameObject> items = body?.GetInventoryDirectAndEquipment();
			for (int i = 0; items != null && i < items.Count; i++)
				if (GameObject.Validate(items[i]) && !items[i].IsNatural()) return false;
			return true;
		}

		private static bool ExactPhysicalFirstGuestHardening(GameObject body,
			KingdomGrowthArrivalCandidate candidate, r_KingdomFirstGuestBody part)
		{
			Brain brain = body?.Brain; Corpse corpse = body?.GetPart<Corpse>();
			return part != null && !part.Inert && ExactFirstGuestBodyIdentity(body, candidate)
				&& part.CandidateId == candidate.Id
				&& part.OpportunityId == candidate.FirstGuest.OpportunityId
				&& part.SettlementId == candidate.SettlementId
				&& part.ObjectId == candidate.ObjectId && part.Marker == candidate.Marker
				&& part.ZoneId == candidate.LodgingZoneId && part.OriginalAllegiance != null
				&& brain != null && brain.PartyLeader == null && brain.Passive && !brain.Mobile
				&& brain.Staying && !brain.Wanders && !brain.WandersRandomly && !brain.DoReequip
				&& brain.Allegiance != null && brain.Allegiance.Calm
				&& !brain.Allegiance.Hostile && brain.Allegiance.TotalWeight == 0
				&& body.GetIntProperty("NoXP") == 1
				&& body.GetIntProperty("SuppressCorpseDrops") == 1
				&& (!part.HadCorpse || corpse != null && corpse.CorpseChance == 0
					&& corpse.BurntCorpseChance == 0 && corpse.VaporizedCorpseChance == 0
					&& corpse.BuildCorpseChance == 0) && PhysicalFirstGuestLoadoutEmpty(body);
		}

		private static bool RestorePhysicalFirstGuest(GameObject body,
			KingdomGrowthArrivalCandidate candidate, out string failure)
		{
			failure = null;
			r_KingdomFirstGuestBody part = body?.GetPart<r_KingdomFirstGuestBody>();
			if (!ExactPhysicalFirstGuestHardening(body, candidate, part))
				return FailFirstGuest("first-guest custody cannot restore exactly", out failure);
			body.RemovePart(part);
			if (body.GetPart<r_KingdomFirstGuestBody>() != null)
				return FailFirstGuest("first-guest custody part could not detach", out failure);
			part.Inert = true;
			Brain brain = body.Brain; brain.Flags = part.OriginalBrainFlags;
			brain.Allegiance = part.OriginalAllegiance;
			RestoreInt(body, "NoXP", part.HadNoXP, part.OriginalNoXP);
			RestoreInt(body, "SuppressCorpseDrops", part.HadSuppressCorpseDrops,
				part.OriginalSuppressCorpseDrops);
			Corpse corpse = body.GetPart<Corpse>();
			if (part.HadCorpse)
			{
				corpse.CorpseChance = part.OriginalCorpseChance;
				corpse.BurntCorpseChance = part.OriginalBurntCorpseChance;
				corpse.VaporizedCorpseChance = part.OriginalVaporizedCorpseChance;
				corpse.BuildCorpseChance = part.OriginalBuildCorpseChance;
			}
			return true;
		}

		private static void RestoreInt(GameObject body, string name, bool had, int value)
		{
			if (had) body.SetIntProperty(name, value); else body.RemoveIntProperty(name);
		}
	}
}
