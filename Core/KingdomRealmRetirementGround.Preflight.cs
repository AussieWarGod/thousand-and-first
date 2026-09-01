using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		private const int MaxObjects = 20000;

		internal static bool TryPrepare(KingdomSystem System, Zone Zone,
			out KingdomRealmRemovalGroundPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || Zone == null || The.Game == null
				|| !ReferenceEquals(Zone, The.Player?.CurrentZone))
				return Fail("cleanup requires the player's genuinely active zone", out Failure);
			if (!KingdomRemovalProjectionRuntime.TryInspectPlayer(
				out List<string> _, out Failure))
				return Fail("player custody changed before ground mutation: " + Failure, out Failure);
			if (!KingdomExternalOwnershipBindingRuntime.TryPrepareRealmReset(Zone,
				new List<string> { System.RealmId, System.KingdomFactionName },
				out KingdomExternalOwnershipResetPlan reset, out Failure))
				return false;
			KingdomRealmRemovalGroundPlan plan = new KingdomRealmRemovalGroundPlan
			{
				Zone = Zone, ExternalOwnership = reset
			};
			if (!KingdomRelocation.TryPrepareRealmRemoval(System, Zone,
				out plan.Relocation, out Failure)) return false;
			if (!TryObjectGraph(Zone, plan.Objects, out Failure)) return false;
			if (!TryBuildConstructionAuthority(System, Zone, plan.Objects,
				out HashSet<GameObject> constructionOwned, out Failure)) return false;
			if (!TryPrepareWitnessWorks(System, Zone, plan.Objects, constructionOwned,
				plan, out Failure))
				return false;
			if (!KingdomRelocation.CollectRealmRemovalCustody(plan.Relocation,
				plan.Objects, out Failure)) return false;
			HashSet<GameObject> custody = PlayerCustody(out Failure);
			if (custody == null) return false;
			for (int i = 0; i < plan.Objects.Count; i++)
				if (custody.Contains(plan.Objects[i]))
					return Fail("player custody is excluded from attended city-ground cleanup",
						out Failure);
			if (plan.Objects.Count > MaxObjects)
				return Fail("active-zone object graph plus exact relocation custody exceeds the bounded cleanup scan",
					out Failure);
			for (int i = 0; i < plan.Objects.Count; i++)
			{
				r_KingdomStasisVault vault = plan.Objects[i]?.GetPart<r_KingdomStasisVault>();
				if (vault == null) continue;
				if (!KingdomStasisVault.TryPrepareRealmRemoval(System, vault,
					out KingdomStasisVaultRemovalPlan stasis, out Failure)) return false;
				plan.StasisVaults.Add(stasis);
				KingdomStasisVault.CollectRealmRemovalArtifacts(stasis, plan.RemovedObjects);
			}
			KingdomRelocation.CollectRealmRemovalArtifacts(plan.Relocation,
				plan.RemovedObjects);
			plan.RecoveryDigest = RecoveryDigest(plan);
			for (int i = 0; i < plan.Objects.Count; i++)
			{
				GameObject item = plan.Objects[i];
				if (!KingdomMarketRemoval.CanRetireStock(System, item,
					out bool retireStock, out Failure)
					|| !KingdomMarketRemoval.CanRetireLegendary(System, item,
						out bool retireLegend, out Failure)) return false;
				if (retireStock) plan.MarketStockRetirements.Add(item);
				if (retireLegend) plan.LegendaryMarketRetirements.Add(item);
				if (!TryClassifyOwnedObject(System, item, constructionOwned,
					out bool owned, out Failure)) return false;
				if (!KingdomRemovalProjectionRuntime.TryInspectCampfire(item,
					out List<string> campfireRows, out Failure)) return false;
				if (!owned && campfireRows.Count == 0) continue;
				if (!CanRemoveExperienceProjections(System, item, out Failure)) return false;
				string objectId = item.RequireID();
				if (string.IsNullOrEmpty(objectId))
					return Fail("an active-ground object could not retain identity", out Failure);
				if (!TryFallback(item, out GameObjectBlueprint fallback, out Failure)) return false;
				if (fallback != null) { plan.Fallbacks[item] = fallback; plan.OwnedBlueprintCount++; }
				if (!InspectCitizenship(System, item, plan, out Failure)) return false;
				plan.MutationObjects.Add(item);
				for (int p = 0; p < (item.PartsList?.Count ?? 0); p++)
				{
					string name = item.PartsList[p]?.GetType().Name;
					if (LooksCustomPart(name) && !KingdomRemovalCoverage.IsCustomPart(name))
						return Fail("custom part lacks teardown coverage: " + name, out Failure);
					if (!KingdomRemovalCoverage.IsCustomPart(name)) continue;
					KingdomRemovalCarrierDisposition disposition =
						KingdomRemovalCoverage.CarrierDisposition(name);
					if (disposition == KingdomRemovalCarrierDisposition.Unknown
						|| disposition == KingdomRemovalCarrierDisposition.PlayerTerminalCut)
						return Fail("ground carrier lacks a lawful attended disposition: " + name,
							out Failure);
					if (name.StartsWith("r_KingdomStasis", StringComparison.Ordinal)
						&& !StasisCovered(item.PartsList[p], plan.StasisVaults))
						return Fail("stasis projection lacks an exact loaded-vault receipt: " + name,
							out Failure);
					if (name == "r_KingdomRelocationFrame"
						&& !plan.RemovedObjects.Contains(item))
						return Fail("relocation frame lacks its exact zone receipt", out Failure);
					plan.CustomPartCount++;
				}
				if (item.Property != null) foreach (string key in item.Property.Keys)
					if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) plan.ObjectPropertyCount++;
				if (item.IntProperty != null) foreach (string key in item.IntProperty.Keys)
					if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) plan.ObjectPropertyCount++;
			}
			plan.RetainedObjectCount = plan.Objects.Count - plan.RemovedObjects.Count;
			for (int i = 0; i < (Zone.Parts?.Count ?? 0); i++)
				if (KingdomRemovalCoverage.IsCustomZonePart(Zone.Parts[i]?.GetType().Name))
					plan.ZonePartCount++;
			if (The.ZoneManager?.ZoneProperties != null
				&& The.ZoneManager.ZoneProperties.TryGetValue(Zone.ZoneID,
					out Dictionary<string, object> properties))
				foreach (string key in properties.Keys)
					if (KingdomRemovalCoverage.IsOwnedZoneProperty(key)) plan.ZonePropertyCount++;
			if (The.ZoneManager?.ZoneProperties != null
				&& The.ZoneManager.ZoneProperties.TryGetValue(Zone.ZoneID,
					out Dictionary<string, object> shared)
				&& shared.TryGetValue("faction", out object faction))
				plan.SharedFaction = faction as string ?? faction?.ToString();
			Plan = plan; return true;
		}

		private static bool StasisCovered(IPart Part,
			List<KingdomStasisVaultRemovalPlan> Plans)
		{
			if (Part is r_KingdomStasisVault vault)
				for (int i = 0; i < Plans.Count; i++)
					if (ReferenceEquals(Plans[i].Vault, vault)) return true;
			KingdomStasisCustodyReceipt receipt =
				(Part as r_KingdomStasisCustody)?.Receipt
				?? (Part as r_KingdomStasisProjection)?.Receipt
				?? (Part as r_KingdomStasisFieldAnchor)?.Receipt;
			if (receipt == null) return false;
			for (int i = 0; i < Plans.Count; i++)
				for (int j = 0; j < Plans[i].Receipts.Count; j++)
					if (KingdomStasisVaultRules.SameAuthority(receipt,
						Plans[i].Receipts[j])) return true;
			return false;
		}

		private static string RecoveryDigest(KingdomRealmRemovalGroundPlan Plan)
		{
			List<string> rows = new List<string>();
			if (Plan.Relocation != null) rows.Add("relocation|" + Plan.Relocation.ExpectedWire);
			for (int i = 0; i < Plan.StasisVaults.Count; i++)
				rows.Add("stasis|" + KingdomStasisVault.RealmRemovalEvidence(
					Plan.StasisVaults[i]));
			rows.Sort(StringComparer.Ordinal);
			return KingdomRetirementDigestRules.Evidence("ground-recovery-v1", rows);
		}

		private static bool TryObjectGraph(Zone Zone, List<GameObject> Objects,
			out string Failure)
		{
			Failure = null;
			Queue<GameObject> pending = new Queue<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			HashSet<GameObject> playerCustody = PlayerCustody(out Failure);
			if (playerCustody == null) return false;
			List<GameObject> roots = Zone.GetObjects();
			for (int i = 0; i < roots.Count; i++)
				if (!playerCustody.Contains(roots[i])) pending.Enqueue(roots[i]);
			while (pending.Count > 0)
			{
				GameObject item = pending.Dequeue();
				if (item == null || playerCustody.Contains(item) || !seen.Add(item)) continue;
				if (seen.Count > MaxObjects)
					return Fail("active-zone object graph exceeds the bounded cleanup scan", out Failure);
				Objects.Add(item);
				List<GameObject> children = item.GetInventoryAndEquipment();
				for (int i = 0; i < children.Count; i++) pending.Enqueue(children[i]);
			}
			return true;
		}

		private static HashSet<GameObject> PlayerCustody(out string Failure)
		{
			Failure = null;
			HashSet<GameObject> seen = new HashSet<GameObject>();
			Queue<GameObject> pending = new Queue<GameObject>();
			if (The.Player != null) pending.Enqueue(The.Player);
			while (pending.Count > 0)
			{
				GameObject item = pending.Dequeue();
				if (item == null || !seen.Add(item)) continue;
				if (seen.Count > MaxObjects)
				{
					Failure = "player custody exceeds the bounded exclusion scan"; return null;
				}
				List<GameObject> children = item.GetInventoryAndEquipment();
				for (int i = 0; i < children.Count; i++) pending.Enqueue(children[i]);
			}
			return seen;
		}

		private static bool TryFallback(GameObject Item, out GameObjectBlueprint Fallback,
			out string Failure)
		{
			Fallback = null; Failure = null;
			if (Item == null || !KingdomRemovalCoverage.IsOwnedBlueprint(Item.Blueprint)) return true;
			string name = Item.Blueprint;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			while (KingdomRemovalCoverage.IsOwnedBlueprint(name))
			{
				if (!seen.Add(name)) return Fail("owned blueprint inheritance contains a cycle: "
					+ Item.Blueprint, out Failure);
				GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(name);
				if (blueprint == null || string.IsNullOrEmpty(blueprint.Inherits))
					return Fail("owned blueprint has no exact vanilla fallback: " + Item.Blueprint,
						out Failure);
				name = blueprint.Inherits;
			}
			Fallback = GameObjectFactory.Factory.GetBlueprintIfExists(name);
			return Fallback != null || Fail("vanilla fallback blueprint is absent: " + name,
				out Failure);
		}

		private static bool InspectCitizenship(KingdomSystem System, GameObject Citizen,
			KingdomRealmRemovalGroundPlan Plan, out string Failure)
		{
			Failure = null;
			r_KingdomCitizenship receipt = Citizen?.GetPart<r_KingdomCitizenship>();
			if (receipt == null)
			{
				if (Citizen?.GetIntProperty("KingdomCitizen") == 1) Plan.LegacyCitizenCount++;
				return true;
			}
			if (Citizen.GetIntProperty("KingdomBorn") == 1
				|| receipt.Phase == KingdomCitizenshipPhase.LegacyPriorUnknown)
			{
				Plan.LegacyCitizenCount++; return true;
			}
			if (receipt.Phase == KingdomCitizenshipPhase.Removed) return true;
			if (receipt.Phase != KingdomCitizenshipPhase.Applied || Citizen.Brain == null
				|| receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion
				|| receipt.BodyObjectId != (Citizen.IDIfAssigned ?? "")
				|| receipt.OwnerRealmId != (System.CurrentRealmId ?? "")
				|| receipt.FactionId != (System.KingdomFactionName ?? ""))
				return Fail("citizenship receipt is divergent or owned by another realm", out Failure);
			AllegianceSet allegiance = Citizen.Brain.GetBaseAllegiance();
			int value = 0;
			bool present = allegiance != null && allegiance.TryGetValue(receipt.FactionId,
				out value);
			if (allegiance == null || KingdomCitizenshipRules.JudgeRemove(receipt.Phase,
				receipt.PriorKind, receipt.PriorValue, present, value, receipt.AppliedValue)
				== KingdomCitizenshipMutation.Quarantine)
				return Fail("citizenship allegiance changed outside its exact receipt", out Failure);
			Plan.ExactForeignCitizens.Add(Citizen); return true;
		}

		private static bool LooksCustomPart(string Name)
		{
			return !string.IsNullOrEmpty(Name) && (Name.StartsWith("r_Kingdom",
				StringComparison.Ordinal) || Name == "KingdomCharterPart");
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
