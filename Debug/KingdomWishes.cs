using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	[HasWishCommand]
	public partial class KingdomWishes
	{
		[WishCommand("kingdom:found", null)]
		public static void FoundWish(string Parameter)
		{
			string name = (string.IsNullOrEmpty(Parameter) ? "Kavvat" : Parameter.Trim());
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				Popup.Show("The kingdom of {{C|" + KingdomPresentation.Rich(system.KingdomDisplayName) +
					"}} is already founded. ({{W|kingdom:found2 NAME:VOCATION}} founds the second city here.)");
				return;
			}
			Zone site = The.Player?.CurrentZone;
			if (!KingdomFoundingTransaction.TryFoundFirstWithoutWater(name, site,
				out var faction, out var failure) || faction == null)
			{
				Popup.Show((string.IsNullOrEmpty(failure)
					? "The founding was refused."
					: failure) + " ({{W|kingdom:dump}} for state.)");
				return;
			}
			Popup.Show("{{C|" + faction.DisplayName + "}} is founded on " + KingdomFounding.StyleGroundClause(system.Style) + ". The chronicle begins.\n\nPersonal-reputation baselines were recorded; both civic relationship directions begin unspecified.");
		}

		/// <summary>
		/// Founds another city on the ground the tester is standing on, skipping the
		/// walk the rite would otherwise require: adjacency to the realm is forced, everything
		/// else &mdash; the realm cap, the refusal to found on ground already held &mdash; is
		/// the shipped rule, because those are the rules worth testing.
		/// </summary>
		/// <param name="Parameter">NAME, or NAME:VOCATION. Vocation defaults to the neutral one.</param>
		[WishCommand("kingdom:found2", null)]
		public static void FoundSecondWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			string name = "Sheol";
			string vocation = KingdomSettlement.NeutralVocation;
			if (!string.IsNullOrEmpty(Parameter))
			{
				if (Parameter.Length > KingdomTradeRules.MaxNameChars * 2)
				{
					Popup.Show("Second-city wish text is too long to parse safely.");
					return;
				}
				string[] parts = Parameter.Trim().Split(':');
				if (parts.Length > 2)
				{
					Popup.Show("Use NAME or NAME:VOCATION.");
					return;
				}
				if (!string.IsNullOrEmpty(parts[0]))
				{
					name = parts[0].Trim();
				}
				if (parts.Length > 1 && KingdomSettlement.IsKnownVocation(parts[1].Trim().ToLowerInvariant()))
				{
					vocation = parts[1].Trim().ToLowerInvariant();
				}
			}
			if (!KingdomFounding.FoundSecond(name, vocation, zone, Force: true))
			{
				Popup.Show(RefusalOrDefault(system, zone) + "\n\nKnown vocations: " + string.Join(", ", KingdomSettlement.Vocations) + ".");
				return;
			}
			Popup.Show("{{C|" + KingdomPresentation.Rich(name) + "}} is founded here as " +
				KingdomSettlement.VocationClause(vocation) + ", another city of {{C|" +
				KingdomPresentation.Rich(system.KingdomDisplayName) + "}}.\n\nSeated: " +
				system.Capture().Describe() + "\nNon-seat cities: " +
				NonSeatDescription(system));
		}

		private static string RefusalOrDefault(KingdomSystem System, Zone Site)
		{
			string refusal = KingdomSettlement.SecondFoundingRefusal(KingdomFounding.JudgeSite(System, Site), KingdomPresentation.Rich(System.KingdomDisplayName));
			return string.IsNullOrEmpty(refusal) ? "The founding was refused; stand in a zone the realm does not already hold." : refusal;
		}

		/// <summary>
		/// Shows which city is seated and what every non-seat city holds, and &mdash; with
		/// {{W|swap}} &mdash; exchanges the seat with the first canonical non-seat row
		/// without the walk. The swap is a probe, not a move: walking into either city's own
		/// ground re-seats it the ordinary way, through
		/// <see cref="KingdomSystem.TrySeat"/>.
		/// </summary>
		[WishCommand("kingdom:seat", null)]
		public static void SeatWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (!string.IsNullOrEmpty(Parameter) && Parameter.Trim().ToLowerInvariant() == "swap")
			{
				KingdomSettlement target = system.NonSeatSettlementAt(0);
				if (target == null)
				{
					Popup.Show("There is only one city. Wish {{W|kingdom:found2 NAME:VOCATION}} to found the second here.");
					return;
				}
				KingdomSettlement wasSeated = system.Capture();
				system.Restore(target);
				if (!system.TryReplaceNonSeatSettlement(target, wasSeated,
					out string failure))
				{
					system.Restore(wasSeated);
					Popup.Show("Seat probe refused without changing topology: " + failure);
					return;
				}
				Popup.Show("Seat forced to {{C|" + KingdomPresentation.Rich(system.SeatName) + "}}.\n\n" + SeatReport(system) + "\n\n{{K|Debug probe: the flat fields now describe a city you are not standing in. Walk into any owned city's ground and the ordinary seat exchange corrects it.}}");
				return;
			}
			Popup.Show(SeatReport(system));
		}

		/// <summary>One line naming the city the flat fields currently describe and every canonical
		/// non-seat city. Prefixed to reports that would otherwise read as one-city state.</summary>
		private static string SeatLine(KingdomSystem System)
		{
			return "{{C|" + KingdomPresentation.Rich(System.SeatName) + "}}" + KingdomSettlement.VocationSuffix(System.Vocation)
				+ (System.NonSeatSettlementCount > 0 ?
					("  {{K|(non-seat: " + NonSeatNames(System) + ")}}") : "");
		}

		private static string NonSeatNames(KingdomSystem System)
		{
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			List<string> names = new List<string>();
			for (int i = 0; i < rows.Count; i++)
				names.Add(KingdomPresentation.Rich(rows[i]?.SettlementName ?? "(unnamed)"));
			return names.Count == 0 ? "none" : string.Join(", ", names.ToArray());
		}

		private static string NonSeatDescription(KingdomSystem System)
		{
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			if (rows.Count == 0) return "(none)";
			List<string> descriptions = new List<string>();
			for (int i = 0; i < rows.Count; i++) descriptions.Add(rows[i].Describe());
			return string.Join("\n", descriptions.ToArray());
		}

		private static string SeatReport(KingdomSystem System)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{{C|Realm}}: ").Append(System.KingdomFactionName ?? "-").Append(" / ").Append(KingdomPresentation.Rich(System.KingdomDisplayName ?? "-"))
				.Append("  cities=").Append(System.SettlementCount).Append("/").Append(KingdomSettlement.MaxSettlements);
			sb.Append("\n{{C|Seated}}: ").Append(System.Capture().Describe());
			sb.Append("\n{{C|Non-seat cities}}: ").Append(NonSeatDescription(System));
			List<string> mismatches = KingdomSettlement.SeatMismatches(typeof(KingdomSystem));
			sb.Append("\nCarried fields: ").Append(KingdomSettlement.CarriedFields().Length)
				.Append("  seat mismatches: ").Append((mismatches.Count == 0) ? "none" : string.Join("; ", mismatches.ToArray()));
			Zone here = The.Player?.CurrentZone;
			if (here != null)
			{
				sb.Append("\nHere (").Append(here.ZoneID).Append("): seat=").Append(System.ClaimedZones.Contains(here.ZoneID))
					.Append(" non-seat=").Append(System.NonSeatClaimsZone(here.ZoneID))
					.Append(" rite=").Append(KingdomFounding.JudgeSite(System, here));
			}
			return sb.ToString();
		}

		/// <summary>
		/// Reports the founded city style plus the terrain evidence that produced it, and lets a
		/// tester force a different style for testing <see cref="KingdomRules.StyleAllows"/>
		/// filtering without re-founding on a different site. Forcing a style does not rewrite the
		/// recorded founding terrain &mdash; it only overrides which building/district rules apply,
		/// same as every other debug wish in this file (reversible probe, not a rewrite of history).
		/// </summary>
	}
}
