using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		internal static bool FactionRegistryCoherent(string Name, Faction Faction)
		{
			if (string.IsNullOrEmpty(Name) || Faction == null || Faction.Name != Name ||
				!ReferenceEquals(Factions.GetIfExists(Name), Faction))
			{
				return false;
			}
			int exactReferences = 0;
			int matchingNames = 0;
			foreach (Faction listed in Factions.GetList())
			{
				if (ReferenceEquals(listed, Faction))
				{
					exactReferences++;
				}
				if (listed != null && listed.Name == Name)
				{
					matchingNames++;
				}
			}
			return exactReferences == 1 && matchingNames == 1;
		}

		internal static bool FactionNameAvailable(string Name)
		{
			if (string.IsNullOrEmpty(Name) || Factions.Exists(Name))
			{
				return false;
			}
			foreach (Faction listed in Factions.GetList())
			{
				if (listed != null && listed.Name == Name)
				{
					return false;
				}
			}
			return true;
		}

		private static bool ReceiptFactionCoherent(r_FounderBasin Basin,
			KingdomSystem System)
		{
			if (Basin == null || System == null)
			{
				return false;
			}
			Faction faction = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity && faction == null)
			{
				return !System.Founded;
			}
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity &&
				!FactionRegistryCoherent(Basin.PendingRealmFaction, faction) &&
				!RepairPendingFactionRegistry(Basin.PendingRealmFaction,
					Basin.PendingTransactionID, Basin.PendingAuthority))
			{
				return false;
			}
			faction = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, faction) ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1)
			{
				return false;
			}
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity)
			{
				return faction.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					faction.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					(faction.GetIntProperty(PendingFactionProperty) == 1 ||
					 (System.Founded && System.KingdomFactionName == Basin.PendingRealmFaction));
			}
			string realmReservation = faction.GetStringProperty(
				RealmReservationProperty, null);
			if (!System.Founded || System.KingdomFactionName != Basin.PendingRealmFaction ||
				(realmReservation != Basin.PendingAuthority &&
				 !(Basin.PendingPhase == KingdomFoundingPhase.Complete &&
				   string.IsNullOrEmpty(realmReservation))))
			{
				return false;
			}
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter)
			{
				Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
				string villageReservation = village?.GetStringProperty(
					VillageReservationProperty, null);
				return FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
					village.GetIntProperty("Village") == 1 &&
					village.DisplayName == Basin.PendingVillageDisplayName &&
					(villageReservation == Basin.PendingAuthority ||
					 (Basin.PendingPhase == KingdomFoundingPhase.Complete &&
					  string.IsNullOrEmpty(villageReservation)));
			}
			return true;
		}

		private static bool ExistingReservationOwnersMatch(r_FounderBasin Basin)
		{
			if (Basin == null)
			{
				return false;
			}
			Faction realm = Factions.GetIfExists(Basin.PendingRealmFaction);
			if (Basin.PendingKind == KingdomFoundingKind.FirstCity)
			{
				return realm == null ||
					(realm.GetIntProperty("PlayerKingdom") == 1 &&
					 realm.GetIntProperty("Village") == 1 &&
					 realm.GetStringProperty(PendingFactionTransactionProperty, null) ==
						Basin.PendingTransactionID &&
					 realm.GetStringProperty(PendingFactionAuthorityProperty, null) ==
						Basin.PendingAuthority &&
					 realm.GetStringProperty(RealmReservationProperty, null) ==
						Basin.PendingAuthority);
			}
			if (!FactionRegistryCoherent(Basin.PendingRealmFaction, realm) ||
				realm.GetIntProperty("PlayerKingdom") != 1 ||
				realm.GetIntProperty("Village") != 1 ||
				realm.GetStringProperty(RealmReservationProperty, null) !=
					Basin.PendingAuthority)
			{
				return false;
			}
			if (Basin.PendingKind != KingdomFoundingKind.VillageCharter)
			{
				return true;
			}
			Faction village = Factions.GetIfExists(Basin.PendingVillageFaction);
			return FactionRegistryCoherent(Basin.PendingVillageFaction, village) &&
				village.GetIntProperty("Village") == 1 &&
				village.DisplayName == Basin.PendingVillageDisplayName &&
				village.GetStringProperty(VillageReservationProperty, null) ==
					Basin.PendingAuthority;
		}

		internal static bool RepairPendingFactionRegistry(string Name, string Transaction,
			string Authority)
		{
			Faction faction = Factions.GetIfExists(Name);
			if (faction == null || string.IsNullOrEmpty(Transaction) ||
				string.IsNullOrEmpty(Authority) || faction.Name != Name ||
				faction.GetIntProperty("PlayerKingdom") != 1 ||
				faction.GetIntProperty("Village") != 1 ||
				faction.GetIntProperty(PendingFactionProperty) != 1 ||
				faction.GetStringProperty(PendingFactionTransactionProperty, null) != Transaction ||
				faction.GetStringProperty(PendingFactionAuthorityProperty, null) != Authority)
			{
				return false;
			}
			int exact = 0;
			int sameName = 0;
			foreach (Faction listed in Factions.GetList())
			{
				if (ReferenceEquals(listed, faction))
				{
					exact++;
				}
				if (listed != null && listed.Name == Name)
				{
					sameName++;
				}
			}
			if (exact == 1 && sameName == 1)
			{
				return true;
			}
			if (exact != 0 || sameName != 0 || FactionListField == null)
			{
				return false;
			}
			try
			{
				List<Faction> list = FactionListField.GetValue(null) as List<Faction>;
				if (list == null || list.Contains(faction))
				{
					return false;
				}
				list.Add(faction);
				return FactionRegistryCoherent(Name, faction);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("pending faction list repair failed: " + Describe(ex));
				return false;
			}
		}

		/// <summary>Durable founding/claim outbox. Official and outsider registers are locally
		/// compensated together. Journal accomplishments use the event id as secretId, so a throw
		/// after insertion is observed and never inserted twice on retry.</summary>
	}
}
