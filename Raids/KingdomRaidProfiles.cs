using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed class KingdomRaidProfile
	{
		public string Key;
		public string Faction;
		public string Reach;
		public string[] Causes;
		public string ChannelBlueprint;
		public string Grievance;
		public string Tactic;
		public string Objective;
		public string Demand;
		public string Recovery;
		public string NaturalTrigger;
		public string NaturalCause;
		public string NaturalEvidence;
		public string[] Steading;
		public string[] Village;
		public string[] Town;
		public string[] City;

		public string[] Members(GrowthStage stage)
		{
			if (stage >= GrowthStage.City && City != null && City.Length > 0) return City;
			if (stage >= GrowthStage.Town && Town != null && Town.Length > 0) return Town;
			if (stage >= GrowthStage.Village && Village != null && Village.Length > 0) return Village;
			return Steading;
		}

		public bool AllowsCause(string cause)
		{
			if (string.IsNullOrEmpty(cause) || Causes == null) return false;
			for (int i = 0; i < Causes.Length; i++)
				if (string.Equals(Causes[i], cause, StringComparison.Ordinal)) return true;
			return false;
		}
	}

	/// <summary>Mergeable faction rosters. Content owns bodies; raid causality only freezes the
	/// selected profile key and deterministic body sequence.</summary>
	public static class KingdomRaidProfiles
	{
		private const int MaxProfiles = 64;
		private const int MaxMembers = 16;
		private static Dictionary<string, KingdomRaidProfile> Profiles;

		public static void Reload()
		{
			Profiles = null;
		}

		public static bool TryGet(string faction, out KingdomRaidProfile profile)
		{
			EnsureLoaded();
			profile = null;
			return !string.IsNullOrEmpty(faction) && Profiles.TryGetValue(faction, out profile);
		}

		public static string Blueprint(KingdomRaidProfile profile, GrowthStage stage,
			long seed, int ordinal)
		{
			if (profile == null || ordinal < 0) return null;
			string[] members = profile.Members(stage);
			if (members == null || members.Length == 0) return null;
			long mixed;
			unchecked { mixed = seed ^ ((long)(ordinal + 1) * 6364136223846793005L); }
			int at = (int)((mixed & 0x7fffffffffffffffL) % members.Length);
			return members[at];
		}

		/// <summary>Freezes the exact tier, reach, and deterministic body sequence into one
		/// content-addressed plan ID. A profile edit after save therefore cancels safely instead of
		/// silently changing the already-disclosed warband.</summary>
		public static string FreezePlan(KingdomRaidProfile profile, GrowthStage stage,
			long seed, int count)
		{
			if (profile == null || count <= 0 || count > MaxMembers
				|| stage < GrowthStage.Camp || stage > GrowthStage.City) return null;
			List<string> selected = new List<string>(count);
			for (int i = 0; i < count; i++)
			{
				string member = Blueprint(profile, stage, seed, i);
				if (!Safe(member)) return null;
				selected.Add(member);
			}
			return KingdomLifecycleRules.ChildId(profile.Key,
				"raid-force-v2:" + profile.Faction + ":" + profile.Reach + ":"
					+ profile.ChannelBlueprint + ":" + profile.Grievance + ":"
					+ profile.Tactic + ":" + profile.Objective + ":" + profile.Demand + ":"
					+ profile.Recovery + ":" + string.Join(",", profile.Causes) + ":"
					+ ((int)stage).ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ ":" + string.Join("\u001f", selected.ToArray()), count);
		}

		public static bool TryResolveFrozen(string faction, string frozenPlan,
			long seed, int count, out KingdomRaidProfile profile, out GrowthStage stage)
		{
			stage = GrowthStage.Camp;
			if (!TryGet(faction, out profile) || string.IsNullOrEmpty(frozenPlan)) return false;
			for (int value = (int)GrowthStage.Camp; value <= (int)GrowthStage.City; value++)
			{
				GrowthStage candidate = (GrowthStage)value;
				if (string.Equals(FreezePlan(profile, candidate, seed, count), frozenPlan,
					StringComparison.Ordinal))
				{
					stage = candidate;
					return true;
				}
			}
			profile = null;
			return false;
		}

		private static void EnsureLoaded()
		{
			if (Profiles != null) return;
			Profiles = new Dictionary<string, KingdomRaidProfile>(StringComparer.Ordinal);
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{ "kingdomraidprofiles", delegate(XmlDataHelper xml)
					{ KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomRaidProfiles"); } },
				{ "profile", HandleProfile }
			};
			foreach (XmlDataHelper xml in DataManager.YieldXMLStreamsWithRoot("KingdomRaidProfiles"))
				xml.HandleNodes(handlers);
		}

		private static void HandleProfile(XmlDataHelper xml)
		{
			string key = xml.GetAttribute("Key");
			string faction = xml.GetAttribute("Faction");
			string reach = xml.GetAttribute("Reach");
			string channel = xml.GetAttribute("ChannelBlueprint");
			string grievance = xml.GetAttribute("Grievance");
			string tactic = xml.GetAttribute("Tactic");
			string objective = xml.GetAttribute("Objective");
			string demand = xml.GetAttribute("Demand");
			string recovery = xml.GetAttribute("Recovery");
			string naturalTrigger = xml.GetAttribute("NaturalTrigger");
			string naturalCause = xml.GetAttribute("NaturalCause");
			string naturalEvidence = xml.GetAttribute("NaturalEvidence");
			string[] causes;
			string[] steading = new string[0];
			string[] village = new string[0];
			string[] town = new string[0];
			string[] city = new string[0];
			bool validMembers = TryMembers(xml.GetAttribute("Steading"), true, out steading)
				&& TryMembers(xml.GetAttribute("Village"), false, out village)
				&& TryMembers(xml.GetAttribute("Town"), false, out town)
				&& TryMembers(xml.GetAttribute("City"), false, out city);
			bool room = !string.IsNullOrEmpty(faction)
				&& (Profiles.ContainsKey(faction) || Profiles.Count < MaxProfiles);
			bool natural = string.IsNullOrEmpty(naturalTrigger) && string.IsNullOrEmpty(naturalCause)
				&& string.IsNullOrEmpty(naturalEvidence)
				|| SafeToken(naturalTrigger) && SafeToken(naturalCause) && Safe(naturalEvidence);
			if (room && SafeToken(key) && Safe(faction) && Safe(reach)
				&& TryTokens(xml.GetAttribute("Causes"), out causes)
				&& EligibleChannel(channel) && Safe(grievance) && Safe(tactic)
				&& string.Equals(objective, "stores", StringComparison.Ordinal)
				&& string.Equals(demand, "water", StringComparison.Ordinal)
				&& string.Equals(recovery, "watch-disarray", StringComparison.Ordinal)
				&& natural && (string.IsNullOrEmpty(naturalCause) || Contains(causes, naturalCause))
				&& Factions.GetIfExists(faction) != null && validMembers)
			{
				Profiles[faction] = new KingdomRaidProfile
				{
					Key = key, Faction = faction, Reach = reach, Causes = causes,
					ChannelBlueprint = channel, Grievance = grievance, Tactic = tactic,
					Objective = objective, Demand = demand, Recovery = recovery,
					NaturalTrigger = naturalTrigger, NaturalCause = naturalCause,
					NaturalEvidence = naturalEvidence,
					Steading = steading, Village = village, Town = town, City = city
				};
			}
			else MetricsManager.LogError("ThousandAndFirst KingdomRaidProfiles: refused malformed profile "
				+ (key ?? "(unnamed)"));
			xml.DoneWithElement();
		}

		private static bool TryTokens(string encoded, out string[] values)
		{
			values = new string[0];
			if (string.IsNullOrEmpty(encoded)) return false;
			string[] raw = encoded.Split(',');
			if (raw.Length == 0 || raw.Length > MaxMembers) return false;
			HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
			List<string> accepted = new List<string>();
			for (int i = 0; i < raw.Length; i++)
			{
				string value = raw[i] == null ? null : raw[i].Trim();
				if (!SafeToken(value) || !unique.Add(value)) return false;
				accepted.Add(value);
			}
			values = accepted.ToArray();
			return true;
		}

		private static bool Contains(string[] values, string value)
		{
			if (values == null) return false;
			for (int i = 0; i < values.Length; i++)
				if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool EligibleChannel(string value)
		{
			if (!Safe(value)) return false;
			try
			{
				GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(value);
				return blueprint != null && blueprint.HasPart("r_KingdomRaidDemand");
			}
			catch { return false; }
		}

		private static bool TryMembers(string encoded, bool required, out string[] members)
		{
			members = new string[0];
			if (string.IsNullOrEmpty(encoded)) return !required;
			string[] raw = encoded.Split(',');
			if (raw.Length > MaxMembers) return false;
			List<string> values = new List<string>();
			for (int i = 0; i < raw.Length; i++)
			{
				string value = raw[i] == null ? null : raw[i].Trim();
				if (!EligibleBlueprint(value)) return false;
				values.Add(value);
			}
			members = values.ToArray();
			return members.Length > 0 || !required;
		}

		private static bool EligibleBlueprint(string value)
		{
			if (!Safe(value)) return false;
			try
			{
				GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(value);
				return blueprint != null && !blueprint.IsBaseBlueprint()
					&& !blueprint.HasProperName() && !blueprint.IsExcludedFromDynamicEncounters()
					&& blueprint.HasPart("Brain");
			}
			catch { return false; }
		}

		private static bool SafeToken(string value)
		{
			if (!Safe(value)) return false;
			for (int i = 0; i < value.Length; i++)
				if (value[i] == '#' || value[i] == ':' || char.IsWhiteSpace(value[i])) return false;
			return true;
		}

		private static bool Safe(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 512) return false;
			for (int i = 0; i < value.Length; i++) if (char.IsControl(value[i])) return false;
			return true;
		}
	}
}
