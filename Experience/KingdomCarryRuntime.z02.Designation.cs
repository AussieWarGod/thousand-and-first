using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCarryRuntime
	{
		private static bool TryScanDesignation(GameObject actor, GameObject sign, Cell cell,
			Zone zone, out GameObject container, out List<GameObject> sources,
			out KingdomLifecycleTopology topology, out string ownerId, out string holderId,
			out string failure)
		{
			container = null;
			sources = new List<GameObject>();
			topology = KingdomLifecycleTopology.None;
			ownerId = null; holderId = "";
			failure = null;
			if (cell == null || zone == null || cell.ParentZone != zone)
			{
				failure = "There is no exact ground here to mark.";
				return false;
			}
			List<GameObject> ground = new List<GameObject>(cell.GetObjects());
			for (int i = 0; i < ground.Count; i++)
			{
				GameObject item = ground[i];
				if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()
					|| item.Inventory == null || !ReferenceEquals(item.CurrentCell, cell)) continue;
				if (container != null)
				{
					failure = "More than one container stands here; the sign cannot guess which one you mean.";
					return false;
				}
				container = item;
			}
			if (container != null)
			{
				if (!FounderOwned(container) || container.IsImportant() || container.IsOwned())
				{
					failure = "That container is not unambiguously yours to designate.";
					return false;
				}
				if (container.Inventory.Objects.Count > KingdomLifecycleRules.MaxCarrySources)
				{
					failure = "That container holds more whole objects than one carry-sign can name.";
					return false;
				}
				for (int i = 0; i < container.Inventory.Objects.Count; i++)
				{
					GameObject item = container.Inventory.Objects[i];
					if (!ReferenceEquals(item == null ? null : item.InInventory, container)
						|| !EligibleSource(item, actor, sign, out failure)) return false;
					sources.Add(item);
				}
					topology = KingdomLifecycleTopology.Inventory;
					ownerId = container.IDIfAssigned;
					holderId = container.IDIfAssigned;
			}
			else
			{
				for (int i = 0; i < ground.Count; i++)
				{
					GameObject item = ground[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, actor)
						|| ReferenceEquals(item, sign) || item.IsCreature || item.IsPlayer()
						|| !ReferenceEquals(item.CurrentCell, cell) || item.InInventory != null)
						continue;
					if (!CargoShaped(item)) continue;
					if (!EligibleSource(item, actor, sign, out failure)) return false;
					sources.Add(item);
					if (sources.Count > KingdomLifecycleRules.MaxCarrySources)
					{
						failure = "That pile holds more whole objects than one carry-sign can name.";
						return false;
					}
				}
				topology = KingdomLifecycleTopology.Cell;
			}
			if (sources.Count == 0)
			{
				failure = KingdomGuestRules.PlantRefusal(
					KingdomGuestRules.PlantVerdict.NothingToCarry);
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			if (!string.IsNullOrEmpty(ownerId)) ids.Add(ownerId);
			for (int i = 0; i < sources.Count; i++)
			{
				string assignedId = sources[i].IDIfAssigned;
				if (!string.IsNullOrEmpty(assignedId) && !ids.Add(assignedId))
				{
					failure = "Two confirmed objects already share an ambiguous identity; nothing was taken.";
					return false;
				}
			}
			return true;
		}

		private static bool EligibleSource(GameObject item, GameObject actor, GameObject sign,
			out string failure)
		{
			failure = null;
			if (!GameObject.Validate(item) || ReferenceEquals(item, actor) || ReferenceEquals(item, sign)
				|| item.IsCreature || item.IsPlayer())
				failure = "A creature cannot be cargo for a carry-sign.";
			else if (!KingdomConstructionInputLeaseAuthority.TryObjectGraphAvailableForOrdinaryTransfer(item, out failure)) { }
			else if (item.IsImportant())
				failure = "An important object in the designation must be removed first.";
			else if (item.Equipped != null)
				failure = "Equipped objects cannot be designated as cargo.";
			else if (!item.IsTakeable())
				failure = "An untakeable object in the designation must be removed first.";
			else if (!FounderOwned(item) || item.IsOwned())
				failure = "Every carried object must be unambiguously yours.";
			else if (string.IsNullOrEmpty(item.Blueprint)
				|| item.Count <= 0 || item.Count > 4096)
					failure = "A cargo object's blueprint or whole-stack count cannot be proved.";
			return failure == null;
		}

		private static bool CargoShaped(GameObject item)
		{
			return GameObject.Validate(item) && (item.IsTakeable() || item.OwnedByPlayer
				|| item.GetIntProperty("DroppedByPlayer") > 0 || item.IsImportant()
				|| item.IsOwned() || item.Equipped != null);
		}

		private static bool FounderOwned(GameObject item)
		{
			return GameObject.Validate(item) && (item.OwnedByPlayer
				|| item.GetIntProperty("DroppedByPlayer") > 0);
		}

		private static bool ExactSign(GameObject actor, GameObject sign, Zone zone)
		{
			if (!GameObject.Validate(actor) || !actor.IsPlayer() || actor.Inventory == null
				|| !GameObject.Validate(sign) || !KingdomConstructionInputLeaseAuthority
					.TryObjectAvailableForLocalDebit(sign, out _)
				|| sign.GetPart<r_KingdomCarrySign>() == null
				|| sign.InInventory != actor || sign.Equipped != null || sign.IsImportant()
				|| string.IsNullOrEmpty(sign.Blueprint)
				|| sign.Count <= 0 || sign.Count > 4096 || actor.CurrentZone != zone) return false;
			return ReferenceCount(actor.Inventory.Objects, sign) == 1;
		}

		private static int ReferenceCount(List<GameObject> objects, GameObject wanted)
		{
			int count = 0;
			for (int i = 0; objects != null && i < objects.Count; i++)
				if (ReferenceEquals(objects[i], wanted)) count++;
			return count;
		}

		private static bool SameDesignation(PlantPlan plan, GameObject container,
			List<GameObject> sources, KingdomLifecycleTopology topology, string ownerId,
			string holderId)
		{
			if (plan == null || !ReferenceEquals(plan.Container, container)
				|| plan.SourceTopology != topology
				|| !string.Equals(plan.SourceOwnerId, ownerId, StringComparison.Ordinal)
				|| !string.Equals(plan.SourceHolderObjectId, holderId, StringComparison.Ordinal)
				|| sources == null || sources.Count != plan.Sources.Count) return false;
			for (int i = 0; i < sources.Count; i++)
				if (!ReferenceEquals(sources[i], plan.Sources[i])) return false;
			return true;
		}

		private static bool TryAssignConfirmedIdentities(PlantPlan plan, out string failure)
		{
			failure = null;
			if (plan == null || !GameObject.Validate(plan.Actor) || !plan.Actor.IsPlayer()
				|| !GameObject.Validate(plan.Sign) || plan.Sources == null
				|| plan.Sources.Count == 0
				|| plan.SourceTopology == KingdomLifecycleTopology.Inventory
					&& !GameObject.Validate(plan.Container))
			{
				failure = "The confirmed carry objects are no longer exact.";
				return false;
			}
			try
			{
				// PublishPlant is entered only after the disclosed yes/no prompt and repeats
				// SameDesignation first. This is the sole intentional GameObject identity seam.
				string actorId = AssignConfirmedIdentity(plan.Actor);
				string signId = AssignConfirmedIdentity(plan.Sign);
				string containerId = plan.SourceTopology == KingdomLifecycleTopology.Inventory
					? AssignConfirmedIdentity(plan.Container) : null;
				List<string> sourceIds = new List<string>(plan.Sources.Count);
				HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
				if (!ConfirmedIdentity(actorId) || !ConfirmedIdentity(signId)
					|| !string.IsNullOrEmpty(containerId) && !ConfirmedIdentity(containerId)
					|| !unique.Add(actorId) || !unique.Add(signId)
					|| !string.IsNullOrEmpty(containerId) && !unique.Add(containerId))
				{
					failure = "The confirmed sign, founder, or container shares an identity.";
					return false;
				}
				for (int i = 0; i < plan.Sources.Count; i++)
				{
					string id = AssignConfirmedIdentity(plan.Sources[i]);
					if (!ConfirmedIdentity(id) || !unique.Add(id))
					{
						failure = "A confirmed cargo identity is absent or duplicated.";
						return false;
					}
					sourceIds.Add(id);
				}
				plan.ActorObjectId = actorId; plan.SignObjectId = signId;
				plan.SourceObjectIds = sourceIds;
				plan.SourceOwnerId = containerId; plan.SourceHolderObjectId = containerId ?? "";
				return true;
			}
			catch (Exception ex)
			{
				failure = "Confirmed carry identity assignment threw " + ex.GetType().Name + ".";
				return false;
			}
		}

		private static string AssignConfirmedIdentity(GameObject Item)
		{
			return Item.ID;
		}

		private static bool ConfirmedIdentity(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= KingdomLifecycleRules.MaxIdChars;
		}

		private static string Describe(List<GameObject> sources)
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; sources != null && i < sources.Count; i++)
			{
				GameObject item = sources[i];
				string name = item == null ? null
					: KingdomPresentation.Rich(item.BaseDisplayNameStripped);
				if (string.IsNullOrEmpty(name)) name = item == null ? "object" : item.Blueprint;
				if (name.Length > 96) name = name.Substring(0, 96);
				string entry = item.Count + "\u00d7 " + name;
				if (text.Length + entry.Length + 2 > 3000) return null;
				if (text.Length > 0) text.Append(", ");
				text.Append(entry);
			}
			return text.ToString();
		}

		private static bool TryDistanceDays(string sourceZoneId, string destinationZoneId,
			out int days)
		{
			days = 0;
			string sourceWorld;
			string targetWorld;
			int sx, sy, sz, tx, ty, tz;
			if (!KingdomRules.TryParseZoneID(sourceZoneId, out sourceWorld, out sx, out sy, out sz)
				|| !KingdomRules.TryParseZoneID(destinationZoneId, out targetWorld,
					out tx, out ty, out tz)
				|| !string.Equals(sourceWorld, targetWorld, StringComparison.Ordinal)) return false;
			days = KingdomGuestRules.HaulDays(KingdomGuestRules.ZoneGridDistance(
				sx, sy, sz, tx, ty, tz));
			return days > 0;
		}

		private static bool ThreatPresent(KingdomSystem system, Zone zone)
		{
			if (system == null || zone == null || system.RaidState == 1) return true;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			if (survey != null) return survey.Raiders.Count > 0;
			foreach (GameObject item in zone.GetObjects())
				if (GameObject.Validate(item) && item.GetIntProperty("KingdomRaider") == 1)
					return true;
			return false;
		}

		private static int LegacyMaterialUnits(KingdomCarryHaul haul)
		{
			if (haul == null) return 0;
			long total = (long)Math.Max(0, haul.Mud) + Math.Max(0, haul.Brush)
				+ Math.Max(0, haul.Timber) + Math.Max(0, haul.Stone)
				+ Math.Max(0, haul.Marble) + Math.Max(0, haul.Scrap);
			return total > int.MaxValue ? int.MaxValue : (int)total;
		}

		private static KingdomCarryBook Authority(KingdomSystem system)
		{
			if (system == null || system.CarryBook == null) return null;
			KingdomLifecycleRules.Normalize(system.CarryBook);
			return KingdomLifecycleRules.CanOwnAuthority(system.CarryBook)
				? system.CarryBook : null;
		}
	}
}
