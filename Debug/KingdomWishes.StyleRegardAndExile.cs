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
	public partial class KingdomWishes
	{
		[WishCommand("kingdom:style", null)]
		public static void StyleWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!system.Founded)
			{
				Popup.Show("No kingdom founded yet. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (string.IsNullOrEmpty(Parameter))
			{
				Popup.Show(StyleReport(system));
				return;
			}
			string style = Parameter.Trim();
			if (!KingdomData.TryGetStyle(style, out string canonical))
			{
				Popup.Show("Unknown style {{W|" + style + "}}. Known styles: " + string.Join(", ", KingdomData.Styles) + ".");
				return;
			}
			system.Style = canonical;
			Popup.Show("Style forced to {{C|" + canonical + "}} (" + KingdomFounding.StyleGroundClause(canonical) + ").\n\n" + StyleReport(system));
		}

		private static string StyleReport(KingdomSystem System)
		{
			return "Style: {{C|" + System.Style + "}} (" + KingdomFounding.StyleGroundClause(System.Style) + ")"
				+ "\nFounding terrain: blueprint=" + (System.FoundingTerrainBlueprint ?? "(none)")
				+ " region=" + (System.FoundingRegionName ?? "(none)")
				+ " z=" + System.FoundingZLevel
				+ "\nKnown styles: " + string.Join(", ", KingdomData.Styles);
		}

		/// <summary>
		/// Moves the realm's regard for its founder to an absolute value through the engine's own
		/// reputation path, so the whole ladder &mdash; murmur, warning, the gate &mdash; runs the
		/// way it runs in play rather than being simulated. This is the reachable trigger:
		/// {{W|kingdom:regard -700}} gets a founder thrown out of their own realm by the shipped
		/// code, not by a debug shortcut.
		/// </summary>
		/// <param name="Parameter">Target reputation, e.g. <c>-700</c>. Empty reports where it stands.</param>
		[WishCommand("kingdom:regard", null)]
		public static void RegardWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			// The realm the founder holds, or — once it has put them out — the one they are
			// outside of. Mending an old realm's regard is the whole return path, so a tester
			// with no realm must still be able to move this number.
			string factionName = system.Founded ? system.KingdomFactionName : system.ExiledFactionName;
			if (string.IsNullOrEmpty(factionName))
			{
				Popup.Show("No kingdom founded yet, and none has put you out. Wish {{W|kingdom:found NAME}} first.");
				return;
			}
			if (string.IsNullOrEmpty(Parameter) || !int.TryParse(Parameter.Trim(), out var target))
			{
				Popup.Show(RegardReport(system) + "\n\nUsage: {{W|kingdom:regard AMOUNT}} (absolute, e.g. -700 to be repudiated, 0 to be heard out again).");
				return;
			}
			Faction realm = Factions.GetIfExists(factionName);
			if (realm == null)
			{
				Popup.Show("The realm's faction is not registered; nothing to move.");
				return;
			}
			int before = The.Game.PlayerReputation.Get(realm);
			// Modify rather than Set, precisely because Modify is what the world uses: it fires
			// AfterReputationChangeEvent, which is the surface the expulsion ladder listens on.
			The.Game.PlayerReputation.Modify(realm, target - before, "Wish", null, "Wish");
			Popup.Show("Regard with {{C|" + realm.DisplayName + "}}: " + before + " -> " + The.Game.PlayerReputation.Get(realm) + ".\n\n" + RegardReport(system));
		}

		/// <summary>Where the founder stands with whichever realm currently has an opinion of them.</summary>
		private static string RegardReport(KingdomSystem System)
		{
			bool held = System.Founded;
			int regard = held ? System.FounderRegard() : System.ExiledRealmRegard();
			string name = held ? System.KingdomDisplayName : System.ExiledDisplayName;
			return "{{C|" + (name ?? "-") + "}}" + (held ? "" : " (which put you out)") + " holds you {{W|" + KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(regard)) + "}} (" + regard + ")."
				+ (held ? ("\nLast spoken of: " + KingdomExileRules.RegardName((RealmRegard)System.RegardSpoken)) : "\nThe gate opens above " + KingdomExileRules.RegardHated + ". Stand on its ground and it will put the question to you.")
				+ "\nRungs: beloved " + KingdomExileRules.RegardLoved + "+, trusted " + KingdomExileRules.RegardLiked + "+, doubted >" + KingdomExileRules.RegardDisliked + ", resented >" + KingdomExileRules.RegardHated + ", repudiated at or below " + KingdomExileRules.RegardHated + " (the gate).";
		}

		/// <summary>
		/// Puts the founder out of their own realm without waiting for the regard to fall there.
		/// Everything else is the shipped path: the realm and both its cities are kept whole, the
		/// Charter is taken, both registers record it, and nothing physical is touched.
		/// </summary>
		/// <param name="Parameter">A deed clause to record, or empty for the unnamed-deed line.</param>
		[WishCommand("kingdom:exile", null)]
		public static void ExileWish(string Parameter)
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string deed = string.IsNullOrEmpty(Parameter) ? KingdomExileRules.DeedClause("Wish") : Parameter.Trim();
			if (!system.Exile(deed, Forced: true, out var refusal))
			{
				Popup.Show(refusal + "\n\n{{K|(kingdom:regard AMOUNT walks the ladder the ordinary way.)}}");
				return;
			}
			Popup.Show(ExileReport(system) + "\n\n{{K|Walk back onto its ground and it will put the question to you, if its regard for you has risen since. kingdom:return forces the asking.}}");
		}

		/// <summary>
		/// Asks the realm that expelled the founder to take them back. Skips nothing &mdash; every
		/// requirement, the ground included, is the shipped one; it only saves a tester the walk
		/// back out and in again to make the zone activate.
		/// </summary>
		[WishCommand("kingdom:return", null)]
		public static void ReturnWish()
		{
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Zone zone = The.Player?.CurrentZone;
			if (!system.TryReturn(zone, out var refusal))
			{
				Popup.Show(refusal + "\n\n" + ExileReport(system));
				return;
			}
			Popup.Show(SeatReport(system));
		}

		/// <summary>One block describing the realm the founder is outside of, if any.</summary>
		private static string ExileReport(KingdomSystem System)
		{
			if (!System.Exiled)
			{
				return "{{C|Exile}}: none on the record.";
			}
			StringBuilder sb = new StringBuilder();
			int regard = System.ExiledRealmRegard();
			sb.Append("{{C|Exiled from}}: ").Append(System.ExiledFactionName).Append(" / ").Append(KingdomPresentation.Rich(System.ExiledDisplayName))
				.Append("  cities=").Append(System.ExiledSettlementCount)
				.Append("  standings=").Append(System.ExiledStandings.Count)
				.Append("  tick=").Append(System.ExiledTick);
			sb.Append("\n{{C|Deed}}: ").Append(System.ExiledDeed ?? "-");
			sb.Append("\n{{C|Its regard}}: ").Append(regard).Append(" (").Append(KingdomExileRules.RegardName(KingdomExileRules.ClassifyRegard(regard))).Append(")")
				.Append("  asked-at=").Append((System.ReturnAskedRegard == int.MinValue) ? "never" : System.ReturnAskedRegard.ToString())
				.Append("  door-closed-told=").Append(System.DoorClosedTold);
			sb.Append("\n{{C|Its seat}}: ").Append((System.ExiledSeat != null) ? System.ExiledSeat.Describe() : "(none)");
			sb.Append("\n{{C|Its other city}}: ").Append((System.ExiledAway != null) ? System.ExiledAway.Describe() : "(none)");
			Zone here = The.Player?.CurrentZone;
			sb.Append("\n{{C|Verdict here}}: ").Append(KingdomExileRules.JudgeReturn(System.Exiled, System.Founded, System.ExiledRealmKeptGround, here != null && System.ExiledRealmHolds(here.ZoneID), regard));
			return sb.ToString();
		}

	}
}
