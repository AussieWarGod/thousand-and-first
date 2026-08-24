using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomReports
	{
		/// <summary>
		/// The watch and the larder, as the founder would see them standing here: crewed defence
		/// (including any garrison district) and what the dedicated larders hold. Both come from
		/// a fresh survey because both are facts about this ground, not fields on the system.
		/// </summary>
		private static string DefenceAndPantryLine(KingdomSystem System, Zone Here)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Here, System);
			string pantryName = KingdomRules.PantryTierNames[(int)survey.FoodAbundance];
			// Names the same size a shared meal would call right now, so the Status report
			// doubles as the honest preview the Charter's meal action promises.
			string pantryHint = (survey.FoodAbundance == KingdomRules.PantryTier.Empty)
				? "nothing dedicated is stored yet"
				: "enough for " + KingdomRules.MealSizeName(survey.FoodAbundance);
			return "\nWater detail: " + ((System.WaterCrew > 0) ? (System.WaterCrew + " of " + System.Population + " carrying") : "nobody carrying")
				+ "\nDefence: " + survey.Defence()
				+ (survey.DistrictDefenceBonus > 0 ? ("  (garrison " + survey.DistrictDefenceBonus + ")") : "")
				+ "  Larder: " + pantryName + " (" + survey.FoodStored + " of " + survey.FoodCapacity + ") — " + pantryHint
				// The food economy in one line beside the pantry it fills and empties, on the same
				// terms the water line above states its own: what the fields make against what the
				// people eat, so a founder never has to reverse-engineer a hunger streak.
				+ "\nFields: " + KingdomGrowth.FoodMadePerDay(survey) + " a day made against "
				+ KingdomRules.RationsPerDay(System.Population) + " eaten"
				+ "  {{K|(" + KingdomRules.ForagedRations(KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew), 1) + " of it foraged)}}";
		}

		/// <summary>
		/// The settlement's power on its own line, or nothing at all when this ground has neither
		/// a work nor a store. A founder who has never commissioned a mill is never told about one.
		/// </summary>
		private static string PowerLine(KingdomSystem System, Zone Here)
		{
			string line = KingdomPower.StatusLine(System, Here);
			return string.IsNullOrEmpty(line) ? "" : ("\n" + line);
		}

		/// <summary>
		/// What shades the ground the founder is standing on (Addendum 6), or nothing at all when
		/// they are not standing in the settlement's own zone. A quarter's character is a thing the
		/// founder reads, not an invisible modifier.
		/// </summary>
		private static string QuarterLine(KingdomSystem System, Zone Here)
		{
			string line = KingdomReach.QuarterLine(System, Here);
			return string.IsNullOrEmpty(line) ? "" : ("\n" + line);
		}

		/// <summary>A memory clause, shaded and spaced, or nothing when there is nothing to date.
		/// A two-zone city's level is partly a memory, and the founder is told how old it is.</summary>
		private static string Dated(string Clause)
		{
			return string.IsNullOrEmpty(Clause) ? "" : ("  {{K|" + Clause + "}}");
		}

		public static string Status(KingdomSystem System, Zone Z = null)
		{
			Zone currentZone = The.Player?.CurrentZone;
			bool currentClaimed = currentZone != null && System.ClaimedZones.Contains(currentZone.ZoneID);
			long now = The.Game.TimeTicks;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|").Append(System.SeatName).Append("}}").Append(KingdomSettlement.VocationSuffix(System.Vocation)).Append(", ")
				.Append(KingdomCharterMenuRules.FoundedWhen(System.FoundedTick, now,
					KingdomRules.TicksPerDay))
				.Append("\nStage: ")
				.Append(System.Stage)
				.Append(System.Withered ? " {{r|(withered)}}" : "")
				.Append(System.Famished ? " {{r|(famished)}}" : "")
				.Append("  Population: ")
				.Append(System.Population)
				.Append(System.SupportedLevel > 0 ? ("  {{K|carries " + System.SupportedLevel + "}}") : "")
				// What the settlement's own notable is worth to that number, named rather than
				// left as an invisible modifier (the brief's tastes and leader traits, Addendum 4's
				// Prefers, all through the one shade).
				.Append(System.SupportedLevel > 0 ? KingdomCeremonyRules.ShadeClause(System.NotableShade) : "")
				.Append((System.SupportedLevel > 0 && Z != null) ? Dated(KingdomSubsidence.SightingClause(System, Z, (The.Game != null) ? The.Game.TimeTicks : 0L)) : "");
			// The realm is the faction; the cities are where its history happened. A founder
			// standing in one should be told the other is still out there, keeping itself.
			if (System.Away != null)
			{
				stringBuilder.Append("\n{{K|").Append(System.KingdomDisplayName).Append(" also holds ").Append(System.Away.SettlementName)
					.Append(KingdomSettlement.VocationSuffix(System.Away.Vocation)).Append(", which keeps itself until you stand in it.}}");
			}
			if (System.OriginCounts.Count > 0)
			{
				stringBuilder.Append("\nPeoples:");
				foreach (System.Collections.Generic.KeyValuePair<string, int> originCount in System.OriginCounts)
				{
					stringBuilder.Append(" ").Append(originCount.Value).Append(" of ").Append(originCount.Key).Append(";");
				}
			}
			// Rock the realm poured water on and never opened reads as a claim like any other, so
			// the count alone would be a lie of omission: a founder counting four parasangs has no
			// way to see that one of them is ground nobody can carry anything out of (STANDARDS 7b).
			string unopened = KingdomDelve.UnreachedNote(System);
			stringBuilder.Append("\nClaimed zones: ").Append(System.ClaimedZones.Count)
				.Append((unopened == null) ? "" : ("\n{{r|" + unopened + "}}"))
				.Append(currentClaimed ? ("  (here: " + KingdomGrowth.CountStoredWater(currentZone) + " drams stored, " + KingdomGrowth.CountOpenWater(currentZone) + " open, space for " + KingdomGrowth.CountStorageSpace(currentZone) + ")") : "")
				.Append("\nShops: tier ").Append(System.ShopTier).Append(System.IdleWorks > 0 ? ("  {{r|" + System.IdleWorks + " works idle for want of hands}}") : "").Append(KingdomWearRules.StatusSuffix(System.DamagedWorks))
				// Defence and the pantry are surveyed live: both are facts about the ground the
				// founder is standing on, and neither is carried on the system.
				.Append(currentClaimed ? DefenceAndPantryLine(System, currentZone) : "")
				.Append(currentClaimed ? PowerLine(System, currentZone) : "")
				// Surveyed live for the same reason the pantry is: what the stockpiles hold is a fact
				// about the ground the founder is standing on, not a field carried on the system.
				.Append(currentClaimed ? ("\n" + KingdomMaterials.StockLine(currentZone)) : "")
				// The ways the settlement wore for itself, on the same terms as the stockpiles: a
				// fact about the ground the founder is standing on, not a field on the system.
				.Append(currentClaimed ? ("\n" + KingdomRoads.WornLine(currentZone)) : "")
				// What reaches this ground, named: the temple quarter is different ground from the
				// workers', and the founder should be able to see which one they are standing in.
				.Append(currentClaimed ? QuarterLine(System, currentZone) : "")
				// What the settlement can build at, and what the next level costs, so the craft
				// level is never a number the founder has to reverse-engineer from refusals.
				.Append(KingdomZoning.Readout(System))
				.Append("\nUpkeep: ")
				.Append(KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(System.Population, System.Stage), System.Stores))
				.Append(" drams per interval  Thirst streak: ")
				.Append(System.DryStreak)
				.Append("  Hunger streak: ")
				.Append(System.HungerStreak)
				.Append("\nNext arrival: ")
				.Append(KingdomCharterMenuRules.DueWhen(System.NextArrivalTick, now,
					KingdomRules.TicksPerDay))
				.Append("\nYour standing with the kingdom: ")
				.Append(The.Game.PlayerReputation.Get(System.KingdomFactionName));
			string need = NextNeed(System, currentZone);
			if (!string.IsNullOrEmpty(need))
			{
				stringBuilder.Append("\r\n\r\n{{W|").Append(need).Append("}}");
			}
			stringBuilder.Append(TradeStatus(System));
			return stringBuilder.ToString();
		}

		/// <summary>Player-facing trade summary. Detailed mode remains a wish-only diagnostic.</summary>
		public static string TradeStatus(KingdomSystem System, bool Detailed = false)
		{
			if (Detailed) return TradeDiagnosticStatus(System);
			KingdomTradeBook book = System?.TradeBook;
			if (book == null) return "\nTrade: no trade has been recorded.";
			StringBuilder text = new StringBuilder();
			if (!KingdomTradeRules.BookUsable(book))
			{
				text.Append("\nTrade: {{r|the saved trade record needs inspection.}} No new trade can rely on it.");
				return text.ToString();
			}
			int active = 0;
			int quarantined = 0;
			int charterLimit = Math.Min(book.Charters?.Count ?? 0,
				KingdomTradeRules.MaxCharters);
			for (int i = 0; i < charterLimit; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null || row.Quarantined) quarantined++;
				else active++;
			}
			text.Append("\nTrade: ").Append(active).Append(active == 1 ? " active charter" : " active charters");
			if (quarantined > 0)
				text.Append("; ").Append(quarantined).Append(quarantined == 1
					? " charter held for inspection" : " charters held for inspection");
			if (book.Manifest != null)
				text.Append("; water manifest ").Append(ManifestStatus(book.Manifest.Status))
					.Append(" with ").Append(book.Manifest.EscrowDrams).Append(" drams from ")
					.Append(book.Manifest.OriginName ?? "an unknown city").Append(" to ")
					.Append(book.Manifest.DestinationName ?? "an unknown city");
			if (book.OpenOperation != null)
				text.Append("; {{W|one trade change is still being settled}}");
			if (book.RetainedEscrowDrams > 0)
				text.Append("; ").Append(book.RetainedEscrowDrams)
					.Append(" drams remain held after earlier trade");
			if (book.RetiredThrough > 0)
				text.Append("; earlier trade has been safely closed");
			int projections = 0;
			int projectionLimit = Math.Min(book.Projections?.Count ?? 0,
				KingdomTradeRules.MaxProjectionRows);
			for (int i = 0; i < projectionLimit; i++)
				if (book.Projections[i] != null && !book.Projections[i].Quarantined) projections++;
			if (projections > 0)
				text.Append("; remembered in ").Append(projections)
					.Append(projections == 1 ? " city record" : " city records");
			return text.ToString();
		}

		private static string ManifestStatus(KingdomTradeManifestStatus Status)
		{
			switch (Status)
			{
			case KingdomTradeManifestStatus.InFlight: return "is on the road";
			case KingdomTradeManifestStatus.Delivered: return "has arrived";
			case KingdomTradeManifestStatus.Quarantined: return "is held for inspection";
			default: return "is not on the road";
			}
		}

		private static string TradeDiagnosticStatus(KingdomSystem System)
		{
			KingdomTradeBook book = System?.TradeBook;
			if (book == null) return "\nTrade: no receipt book.";
			StringBuilder text = new StringBuilder();
			text.Append("\nTrade: format ").Append(book.FormatVersion)
				.Append(" ").Append(book.SchemaState);
			if (!KingdomTradeRules.BookUsable(book))
			{
				text.Append(" {{r|(not authoritative)}}");
				if (!string.IsNullOrEmpty(book.SchemaFault))
				{
					text.Append(" — ");
					AppendBoundedTradeText(text, book.SchemaFault,
						KingdomTradeRules.MaxNameChars);
				}
				return text.ToString();
			}
			int active = 0;
			int quarantined = 0;
			int charterLimit = Math.Min(book.Charters?.Count ?? 0,
				KingdomTradeRules.MaxCharters);
			for (int i = 0; i < charterLimit; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null || row.Quarantined) quarantined++;
				else active++;
			}
			text.Append("; charters ").Append(active);
			if (quarantined > 0) text.Append("+").Append(quarantined).Append(" quarantined");
			if (book.Manifest != null)
				text.Append("; manifest ").Append(book.Manifest.Status).Append(" ")
					.Append(book.Manifest.EscrowDrams).Append(" drams ")
					.Append(book.Manifest.OriginName ?? "?").Append("→")
					.Append(book.Manifest.DestinationName ?? "?");
			if (book.OpenOperation != null)
				text.Append("; receipt ").Append(book.OpenOperation.Sequence).Append("/")
					.Append(book.OpenOperation.Phase);
			text.Append("; retained ").Append(book.RetainedEscrowDrams)
				.Append("; retired through ").Append(book.RetiredThrough);
			int projections = 0;
			int projectionLimit = Math.Min(book.Projections?.Count ?? 0,
				KingdomTradeRules.MaxProjectionRows);
			for (int i = 0; i < projectionLimit; i++)
				if (book.Projections[i] != null && !book.Projections[i].Quarantined) projections++;
			text.Append("; city projections ").Append(projections);
			for (int i = 0; i < charterLimit; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null) continue;
				text.Append("\n  charter ").Append(row.Id).Append(" ")
					.Append(row.DealKey).Append("/").Append(row.Faction)
					.Append(" next=").Append(row.NextTick)
					.Append(row.Quarantined ? " QUARANTINED" : "");
			}
			for (int i = 0; i < projectionLimit; i++)
			{
				KingdomTradeProjectionRow row = book.Projections[i];
				if (row == null) continue;
				text.Append("\n  projection city=").Append(row.SettlementId)
					.Append(" zone=").Append(row.ZoneId).Append(" object=")
					.Append(row.ObjectId).Append(row.Quarantined ? " QUARANTINED" : "");
			}
			return text.ToString();
		}

		private static void AppendBoundedTradeText(StringBuilder Text, string Value, int Limit)
		{
			if (Text == null || string.IsNullOrEmpty(Value) || Limit <= 0) return;
			int count = Math.Min(Value.Length, Limit);
			Text.Append(Value, 0, count);
			if (count < Value.Length) Text.Append("...");
		}

		/// <summary>
		/// One plain sentence naming the settlement's most pressing want, so a founder always
		/// knows the next thing to do without reading a manual. Ordered by what actually
		/// blocks growth: water, then beds, then hands, then storage.
		/// </summary>
		/// <returns>Advice line, or empty when nothing is wanting.</returns>
		public static string NextNeed(KingdomSystem System, Zone Here)
		{
			if (Here == null || !System.ClaimedZones.Contains(Here.ZoneID))
			{
				return "Stand on the kingdom's own ground to see what it wants.";
			}
			int stored = KingdomGrowth.CountStoredWater(Here);
			int capacity = KingdomGrowth.CountStorageCapacity(Here);
			if (capacity <= 0)
			{
				return "Nothing here is dedicated to the stores. Dedicate a vessel, or commission a cask rack, and the settlement can begin to keep water.";
			}
			// Named before the thirst line, because it is the answer to it: a settlement with
			// nobody on the water is not failing, it is waiting to be told what to do.
			if (System.WaterCrew <= 0 && System.Population > 0 && KingdomGrowth.CountOpenWater(Here) > 0)
			{
				return "Nobody is carrying water. There is open water on this ground - set a water detail from the Charter, or keep pouring it in yourself.";
			}
			if (stored < KingdomRules.DramsPerArrival + KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(System.Population, System.Stage), System.Stores))
			{
				return "The stores are nearly dry. Pour water into a dedicated vessel; nothing else can happen until there is water to share.";
			}
			// The food half of the two lines above, in the same order and for the same reason: a
			// settlement with nowhere to keep food is not failing, it is waiting to be told what
			// to do, and one that is short of a harvest is failing and must be able to say so.
			KingdomSurvey pantry = KingdomSurvey.Take(Here, System);
			if (pantry.FoodCapacity <= 0 && KingdomGrowth.FoodMadePerDay(pantry) > 0)
			{
				return "The fields have nowhere to send a harvest. Dedicate a larder, or commission one, and what they grow will be kept.";
			}
			if (System.HungerStreak > 0)
			{
				return "The larders came up short and the settlement went hungry. More fields, or fewer mouths — a field feeds four settlers to the hand.";
			}
			if (!KingdomRules.HasRoomToHouse(System.Population, KingdomGrowth.CountBeds(Here)))
			{
				return "There is no bed free. Commission a communal bunk — and if the beds that exist are ones nobody arriving will take, the roll says whose needs they fail.";
			}
			if (System.IdleWorks > 0)
			{
				return System.IdleWorks + " of the works stand idle for want of hands. More settlers, or fewer works.";
			}
			if (System.ShorthandedWorks > 0)
			{
				return System.ShorthandedWorks + " of the works run shorthanded and produce less than they could.";
			}
			if (System.DamagedWorks > 0)
			{
				return KingdomWearRules.NextNeedLine(System.DamagedWorks);
			}
			if (System.Stage < GrowthStage.Steading && capacity < 16)
			{
				return "Storage is thin. A great cistern would carry the settlement toward a steading.";
			}
			return "";
		}

		/// <summary>
		/// The roll of settlers: who came, from where, and when. A settlement of numbers is a
		/// spreadsheet; a settlement of names is a place you come back to.
		/// </summary>
		public static string Roll(KingdomSystem System, int Limit = 30)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|The roll of ").Append(System.SeatName).Append("}}\n");
			if (System.RosterNames.Count == 0)
			{
				stringBuilder.Append("\nNo one has come yet. Water and a bed will change that.");
				return stringBuilder.ToString();
			}
			Zone currentZone = The.Player?.CurrentZone;
			bool hereIsOurs = currentZone != null && System.ClaimedZones.Contains(currentZone.ZoneID);
			int start = (System.RosterNames.Count > Limit) ? (System.RosterNames.Count - Limit) : 0;
			for (int i = start; i < System.RosterNames.Count; i++)
			{
				stringBuilder.Append("\n").Append(System.RosterNames[i]);
				if (i < System.RosterOrigins.Count)
				{
					stringBuilder.Append(", of ").Append(System.RosterOrigins[i]);
				}
				if (i < System.RosterArrived.Count)
				{
					stringBuilder.Append(" {{K|(came the ").Append(System.RosterArrived[i]).Append(")}}");
				}
				if (hereIsOurs)
				{
					stringBuilder.Append(KingdomLodging.RollLine(currentZone, System.RosterNames[i]));
				}
			}
			if (hereIsOurs)
			{
				List<string> yardLines = KingdomYards.RollLines(currentZone);
				if (yardLines.Count > 0)
				{
					stringBuilder.Append("\n\nTrades taken up:");
					for (int i = 0; i < yardLines.Count; i++)
					{
						stringBuilder.Append("\n").Append(yardLines[i]);
					}
				}
			}
			stringBuilder.Append("\n\n{{K|").Append(System.RosterNames.Count).Append(" named; ").Append(System.Population).Append(" living in the settlement.}}");
			stringBuilder.Append(KingdomGuestbook.RollAppendix(System));
			return stringBuilder.ToString();
		}

		public static string Standings(KingdomSystem System, int Limit = 18)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|Standings of ").Append(System.KingdomDisplayName).Append("}}\n");
			int shown = 0;
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Name == System.KingdomFactionName || faction.Name == "Player" || !faction.Visible)
				{
					continue;
				}
				int standing = System.GetStanding(faction.Name);
				if (standing != 0)
				{
					faction.FactionFeeling.TryGetValue(System.KingdomFactionName, out var feeling);
					stringBuilder.Append("\n").Append(faction.DisplayName).Append(": standing ")
						.Append(standing)
						.Append(", their feeling toward us ")
						.Append(feeling);
					shown++;
					if (shown >= Limit)
					{
						stringBuilder.Append("\n...");
						break;
					}
				}
			}
			if (shown == 0)
			{
				stringBuilder.Append("\nThe world holds no strong opinion of us yet.");
			}
			return stringBuilder.ToString();
		}

		public static string Chronicle(KingdomSystem System, bool Outsider = false, int Limit = 25)
		{
			System.Collections.Generic.List<string> entries = Outsider ? System.OutsiderEntries : System.ChronicleEntries;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Outsider ? "{{r|As others tell it}}" : ("{{C|The Chronicle of " + System.KingdomDisplayName + "}}")).Append("\n");
			if (entries.Count == 0)
			{
				stringBuilder.Append("\nNothing is written yet.");
				return stringBuilder.ToString();
			}
			int start = (entries.Count > Limit) ? (entries.Count - Limit) : 0;
			for (int i = start; i < entries.Count; i++)
			{
				stringBuilder.Append("\n").Append(entries[i]);
			}
			return stringBuilder.ToString();
		}
	}
}
