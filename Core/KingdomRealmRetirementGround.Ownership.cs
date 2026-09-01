using System;
using System.Collections.Generic;
using System.Reflection;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		private static bool TryClassifyOwnedObject(KingdomSystem System, GameObject Item,
			HashSet<GameObject> ConstructionOwned,
			out bool Owned, out string Failure)
		{
			Owned = false; Failure = null;
			if (!GameObject.Validate(Item)) return true;
			if (ConstructionOwned != null && ConstructionOwned.Contains(Item))
			{
				Owned = true; return true;
			}
			List<string> evidence = new List<string>();
			bool candidate = KingdomRemovalCoverage.IsOwnedBlueprint(Item.Blueprint);
			for (int i = 0; i < (Item.PartsList?.Count ?? 0); i++)
			{
				IPart part = Item.PartsList[i];
				string name = part?.GetType().Name;
				if (!KingdomRemovalCoverage.IsCustomPart(name)) continue;
				candidate = true;
				try { CollectRealmEvidence(part, evidence); }
				catch (Exception ex)
				{
					return Fail("TAF carrier owner evidence could not be read: " + name
						+ " (" + ex.Message + ")", out Failure);
				}
			}
			CollectOwnedProperties(Item, evidence, ref candidate);
			r_KingdomCitizenship citizenship = Item.GetPart<r_KingdomCitizenship>();
			if (citizenship != null && !string.IsNullOrEmpty(citizenship.OwnerRealmId))
				evidence.Add(citizenship.OwnerRealmId);
			KingdomRemovalOwnerVerdict verdict =
				KingdomRealmRemovalRetryRules.ClassifyOwnerEvidence(System.RealmId,
					evidence, candidate, false);
			if (verdict == KingdomRemovalOwnerVerdict.NotApplicable) return true;
			if (verdict == KingdomRemovalOwnerVerdict.Ambiguous)
				return Fail("TAF ground carrier has no exact realm-owner evidence: "
					+ (Item.IDIfAssigned ?? Item.Blueprint ?? "object"), out Failure);
			if (verdict != KingdomRemovalOwnerVerdict.CurrentRealm)
				return Fail("TAF ground carrier is foreign or ownership-divergent: "
					+ (Item.IDIfAssigned ?? Item.Blueprint ?? "object"), out Failure);
			Owned = true; return true;
		}

		private static void CollectRealmEvidence(object Carrier, List<string> Evidence)
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.NonPublic;
			Type type = Carrier?.GetType();
			if (type == null) return;
			foreach (FieldInfo field in type.GetFields(flags))
				if (field.FieldType == typeof(string) && RealmMember(field.Name))
				{
					string value = field.GetValue(Carrier) as string;
					if (!string.IsNullOrEmpty(value)) Evidence.Add(value);
				}
			foreach (PropertyInfo property in type.GetProperties(flags))
				if (property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0
					&& property.GetGetMethod(true) != null && RealmMember(property.Name))
				{
					string value = property.GetValue(Carrier, null) as string;
					if (!string.IsNullOrEmpty(value)) Evidence.Add(value);
				}
		}

		private static void CollectOwnedProperties(GameObject Item, List<string> Evidence,
			ref bool Candidate)
		{
			if (Item.Property != null) foreach (KeyValuePair<string, string> row in Item.Property)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(row.Key))
				{
					Candidate = true;
					if (RealmMember(row.Key) && !string.IsNullOrEmpty(row.Value)) Evidence.Add(row.Value);
				}
			if (Item.IntProperty != null) foreach (string key in Item.IntProperty.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) Candidate = true;
		}

		private static bool RealmMember(string Name)
		{
			return Name == KingdomShopStockRules.LegacyStockRealmProperty
				|| (!string.IsNullOrEmpty(Name) && Name.IndexOf("RealmId",
					StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static string ObjectRosterRow(GameObject Item,
			string BlueprintOverride = null, bool ExcludeCampfire = false,
			bool ExcludeExperienceProjections = false,
			bool ExcludeMarketStockProjection = false,
			bool ExcludeLegendaryMarketProjection = false)
		{
			List<string> rows = new List<string>();
			rows.Add("id=" + (Item.IDIfAssigned ?? ""));
			rows.Add("blueprint=" + (BlueprintOverride ?? Item.Blueprint ?? ""));
			for (int i = 0; i < (Item.PartsList?.Count ?? 0); i++)
			{
				string name = Item.PartsList[i]?.GetType().Name;
				if (ExcludeExperienceProjections && (name == "r_KingdomOfficeProjection"
					|| name == "r_KingdomRemembranceProjection"
					|| name == "r_KingdomWitnessWorkProjection")) continue;
				if (ExcludeMarketStockProjection
					&& name == "r_KingdomMarketStockProjection") continue;
				if (ExcludeLegendaryMarketProjection
					&& name == "r_KingdomLegendaryMarketProjection") continue;
				if (KingdomRemovalCoverage.IsCustomPart(name)) rows.Add("part=" + name);
			}
			if (Item.Property != null) foreach (KeyValuePair<string, string> row in Item.Property)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(row.Key))
				{
					if (ExcludeMarketStockProjection
						&& KingdomMarketRemoval.IsStockProjectionProperty(row.Key)) continue;
					rows.Add("string=" + row.Key + "=" + (row.Value ?? ""));
				}
			if (Item.IntProperty != null) foreach (KeyValuePair<string, int> row in Item.IntProperty)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(row.Key))
				{
					if (ExcludeMarketStockProjection
						&& KingdomMarketRemoval.IsStockProjectionProperty(row.Key)) continue;
					rows.Add("int=" + row.Key + "=" + row.Value);
				}
			if (!ExcludeCampfire && KingdomRemovalProjectionRuntime.TryInspectCampfire(Item,
				out List<string> campfire, out string _))
				for (int i = 0; i < campfire.Count; i++) rows.Add("campfire=" + campfire[i]);
			rows.Sort(StringComparer.Ordinal);
			return string.Join("\u001e", rows.ToArray());
		}
	}
}
