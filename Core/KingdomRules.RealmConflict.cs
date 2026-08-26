namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		public const int DealTrickleStanding = 2;

		public class DealEntry
		{
			public string Key;

			public string DisplayName;

			public int MinStanding;

			public int IncomeDrams;

			public long IntervalTicks;

			public string CaravanBlueprint;
		}

		public static bool TryParseDealAttributes(string Key, string DisplayName, string MinStanding, string Income, string Interval, string Caravan, out DealEntry Entry, out string Error)
		{
			Entry = null;
			Error = null;
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(DisplayName))
			{
				Error = "deal needs Key and DisplayName";
				return false;
			}
			if (!int.TryParse(MinStanding, out var minStanding))
			{
				Error = "deal " + Key + " has a bad MinStanding";
				return false;
			}
			if (!int.TryParse(Income, out var income) || income < 0)
			{
				Error = "deal " + Key + " has a bad Income";
				return false;
			}
			if (!long.TryParse(Interval, out var interval) || interval <= 0)
			{
				Error = "deal " + Key + " has a bad Interval";
				return false;
			}
			Entry = new DealEntry
			{
				Key = Key,
				DisplayName = DisplayName,
				MinStanding = minStanding,
				IncomeDrams = income,
				IntervalTicks = interval,
				CaravanBlueprint = (string.IsNullOrEmpty(Caravan) ? "DromadTrader1" : Caravan)
			};
			return true;
		}

		public const int RaidStandingThreshold = -250;

		public const int RaidTributeDrams = 6;

		public const int RaidPlunderDrams = 24;

		public const long RaidCooldownTicks = 8400L;

		public const long RaidWarningLeadTicks = 1200L;

		/// <summary>
		/// How far past a raid's due tick the founder still counts as having been there to meet
		/// it, in whole days. One: raiders who arrive within the day of the warning running out
		/// find somebody home and the raid resolves, and raiders who arrive on ground nobody has
		/// walked for longer than that wait rather than looting in the dark
		/// (<see cref="RestampDeadline"/>, and <c>KingdomRaids.RewarnRaidOnReturn</c> for what
		/// the founder is told about it).
		/// <para>
		/// This was a bare <c>&gt; TicksPerDay</c> written inline at the comparison - the only
		/// raw-tick grace band in the mod that never went through a named rule. Same width,
		/// named, and now the same helper the manifest and the arrival queue read.
		/// </para>
		/// </summary>
		public const int RaidWitnessGraceDays = 1;

		public static int RaidSize(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 0;
			case GrowthStage.Steading:
				return 2;
			case GrowthStage.Village:
				return 3;
			case GrowthStage.Town:
				return 4;
			default:
				return 5;
			}
		}

		/// <summary>
		/// Every faction that will actually come for the stores. A faction is provokable only
		/// because <see cref="RaiderTableFor"/> can field a war party for it, so this array and
		/// those tables are one contract, indexed together: <c>KingdomRaids.FindProvokedFaction</c>
		/// skips any standing whose faction has no table, and a name here with no table would be a
		/// threat that never arrives.
		/// </summary>
		public static readonly string[] ProvokableFactions = new string[5] { "Snapjaws", "Baboons", "Goatfolk", "Cannibals", "Issachari" };

		// Parallel to ProvokableFactions, and verified against Caves of Qud 2.0.211.51: creature
		// names against StreamingAssets/Base/ObjectBlueprints/Creatures.xml, faction keys against
		// Factions.xml (the key is "Issachari"; "Issachari tribe" is only its DisplayName). A
		// misspelling on either side creates nothing and fails silently at raid time, so the two
		// arrays are walked in both directions by the tests.
		//
		// Raiders are drawn one per body at random, so a doubled entry is weight: each party is
		// two parts scavenger to one part fighter. Raids take drams and leave scars; they are not
		// meant to field a faction's best.
		private static readonly string[][] RaiderTables = new string[5][]
		{
			new string[3] { "Snapjaw Scavenger", "Snapjaw Scavenger", "Snapjaw Hunter" },
			new string[3] { "Baboon", "Baboon", "Hulking Baboon" },
			new string[3] { "Goatfolk Bully", "Goatfolk Bully", "Goatfolk Hornblower" },
			new string[3] { "Cannibal", "Cannibal", "Juicing Cannibal" },
			new string[3] { "Issachari Raider", "Issachari Raider", "Issachari Rifler" }
		};

		/// <summary>
		/// The war party a provoked faction sends. Blueprint names, drawn one per raider.
		/// </summary>
		/// <param name="FactionName">A faction key as it appears in the settlement's standings
		/// (Qud's <c>Factions.xml</c> Name, not its DisplayName). Null is tolerated.</param>
		/// <returns>The faction's table, or null where the faction cannot raid &mdash; which is
		/// how a standing is judged unprovokable. The returned array is shared: read it, never
		/// write it.</returns>
		public static string[] RaiderTableFor(string FactionName)
		{
			for (int i = 0; i < ProvokableFactions.Length; i++)
			{
				if (ProvokableFactions[i] == FactionName)
				{
					// A faction listed with no table is a wiring error, and the tests fail loudly
					// on it; inside a raid it degrades to "cannot raid" rather than throwing out
					// of the engine's event dispatch.
					if (i >= RaiderTables.Length)
					{
						return null;
					}
					return RaiderTables[i];
				}
			}
			return null;
		}

		public static readonly string[] OutsiderLeads = new string[6] { "It is said that ", "Travelers claim that ", "The dromads tell that ", "A rumor holds that ", "The cults mutter that ", "Some deny that " };

		public static readonly string[] OutsiderTails = new string[6] { ", though the tellers disagree on the year", ", and the water in the telling is always sweeter", ", or so the version sold at the Stilt goes", ", which is a lie, or was one", ", and no two who tell it agree who was there", "" };

		public static string ComposeOutsider(string Text, int Roll)
		{
			int lead = Roll % OutsiderLeads.Length;
			if (lead < 0)
			{
				lead += OutsiderLeads.Length;
			}
			int tail = (Roll / OutsiderLeads.Length) % OutsiderTails.Length;
			if (tail < 0)
			{
				tail += OutsiderTails.Length;
			}
			return OutsiderLeads[lead] + Text + OutsiderTails[tail] + ".";
		}

	}
}
