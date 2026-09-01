using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The rite of shared water held with one of the founder's own settlers: Addendum 5's
	/// diplomacy channel, and the only one that works on one named person at a time.
	/// <see cref="KingdomWaterRiteRules"/> owns every decision and every hand-written line; this
	/// file gathers the facts off real people and real buildings, spends the real water, and hands
	/// the outcome to the surfaces that already exist for it.
	/// <para>
	/// <b>What this is.</b> Qud's own water ritual is the setting's central act: you share your
	/// water with a stranger, and afterwards you are water-bonded to them and to everything they
	/// belong to. This is that act turned inward, and it is deliberately <em>not</em> the engine's
	/// ritual machinery &mdash; no <c>WaterRitualRecord</c>, no reputation award, no Sifrah board.
	/// Those exist to move the player's standing with a faction through whichever stranger is
	/// standing there. Our settlers are the realm's own roll, not vanilla ritualists; what moves
	/// here is one person's covenant, allegiance, or belief, and it moves because the founder filled a bowl from the city's
	/// stores, set it on the ground, and waited.
	/// </para>
	/// <para>
	/// <b>It builds nothing that already exists.</b> Theology goes through
	/// <c>KingdomConversion.Convert</c>; neutral affiliation through <c>AdoptAffiliation</c>. Both
	/// share one exact transition-custody path, so
	/// the tally, both registers and the ledger cannot drift apart. A settler pressed past bearing
	/// goes to <c>KingdomConversion</c>'s pressure surface through
	/// <see cref="IConversionPressure"/>, so there is one exit in this mod, with one set of words
	/// and one grace, and this file does not open a second. What is genuinely this channel's own
	/// is here and nowhere else: the invitation, the price, the answer, and the rule that a
	/// question asked once is not asked again until something is different.
	/// </para>
	/// <para>
	/// <b>Rare on purpose.</b> One rite, one soul: the cadence is the same three days
	/// <c>KingdomCreed.HoldRite</c> asks between rites of shared water between cities, read from
	/// the same constant and for the same stated reason. The price is the founding basin's own
	/// eight drams plus a measure for whatever stands in the way, disclosed before it is paid, and
	/// spent whichever way they answer.
	/// </para>
	/// <para>
	/// <b>Its state lives on the people.</b> Shared living, the stamp a refusal leaves, and the
	/// mark of an asking that went too far are string and int properties on the settler, so they
	/// travel with the person, go with the person, and cost a settlement that has never held a
	/// rite exactly nothing. The realm carries one new field, the tick of the last rite, for the
	/// cadence.
	/// </para>
	/// </summary>
	public static partial class KingdomWaterRite
	{
		/// <summary>
		/// Gated on the conversion machinery rather than on an option of its own: an acceptance is
		/// recorded through <c>KingdomConversion.Convert</c>, so a rite offered while that is off
		/// would pour real water into a channel that could not land it.
		/// </summary>
		public static bool Enabled
		{
			get { return KingdomConversion.Enabled; }
		}

		/// <summary>
		/// Attended passes this settler has been present for &mdash; shared living WITH THE
		/// SETTLEMENT, which is not <c>KingdomConversionRules.SharedLivingForConversion</c>'s
		/// shared living TOWARD ONE CREED. That one is household-scoped, closeness-scaled and
		/// redirected the moment somebody moves house; this one asks only how much of this
		/// settlement's life this person has stood through. The rite needs the second because it
		/// exists to reach the people the first cannot: the settler in a quarter of their own,
		/// whom no household majority is pulling at.
		/// <para>
		/// Advanced by <see cref="OnSettlementPass"/> and by nothing else, so an absent founder
		/// adds none of it.
		/// </para>
		/// </summary>
		public const string SharedDaysProperty = "KingdomSharedDays";

		/// <summary>Tick <see cref="SharedDaysProperty"/> was last advanced at, so two callers
		/// resolving the same moment cannot count one evening twice.</summary>
		public const string SharedDayTickProperty = "KingdomSharedDayTick";

		/// <summary>Refusals this settler has given. See
		/// <c>KingdomWaterRiteRules.AskedTooOften</c>.</summary>
		public const string RefusalsProperty = "KingdomWaterRiteRefusals";

		/// <summary>The answer their last refusal gave, stored as the enum value plus one so that
		/// zero &mdash; what an absent property reads as &mdash; means "never asked".</summary>
		public const string StampAnswerProperty = "KingdomWaterRiteAnswer";

		/// <summary>Hostility as it stood at their refusal.</summary>
		public const string StampHostilityProperty = "KingdomWaterRiteHostility";

		/// <summary>Whether a rival shrine stood in their quarter at their refusal.</summary>
		public const string StampShrineProperty = "KingdomWaterRiteShrine";

		/// <summary>Whether only a change of the realm's own creed can re-open the question.</summary>
		public const string StampAbsoluteProperty = "KingdomWaterRiteAbsolute";

		/// <summary>Shared passes at which their reach would have covered the distance, or zero.</summary>
		public const string StampNeededProperty = "KingdomWaterRiteNeeded";

		/// <summary>The realm's creed as it stood at their refusal.</summary>
		public const string StampCreedProperty = "KingdomWaterRiteCreed";

		/// <summary>
		/// The creed a settler was asked about one time too many. While the realm still holds it,
		/// the rite is shut to them and <see cref="RepeatedAsking"/> reports it to
		/// <c>KingdomConversion</c> as pressure &mdash; which walks them out only if they resent it
		/// (<c>KingdomConversionRules.Resents</c>), and otherwise simply leaves them alone.
		/// </summary>
		public const string AskedTooOftenCreedProperty = "KingdomWaterRiteClosedCreed";

		// ==================================================================================
		// The pressure source. Repeated asking is the one thing about this channel that IS
		// imposed: KingdomConversionRules.IsImposed names Diplomacy as invited and consented,
		// and it is -- once. Asked over and over it stops being an invitation, so this file
		// reports it as standing pressure through the sanctioned surface rather than growing an
		// exit of its own. Re-derived every pass by that surface's own contract, so a founder
		// who stops holding the creed they kept asking about takes the pressure off by doing it.
		// ==================================================================================

		private sealed class RepeatedAsking : IConversionPressure
		{
			public string PressingCreed(KingdomSystem System, Zone Z, GameObject Settler)
			{
				if (System == null || Settler == null)
				{
					return null;
				}
				string closed = Settler.GetStringProperty(AskedTooOftenCreedProperty);
				if (string.IsNullOrEmpty(closed)
					|| !KingdomData.CreedUsesTheology(closed))
				{
					return null;
				}
				return KingdomWaterRiteRules.SameCreed(closed, RealmCreed(System)) ? closed : null;
			}
		}

		private static readonly RepeatedAsking Pressure = new RepeatedAsking();

		/// <summary>
		/// Registers this channel's standing pressure source with <c>KingdomConversion</c>.
		/// Idempotent both here and there, and called from every entry point of this file rather
		/// than from a load hook, so a mid-session mod rebuild or a registry re-read cannot leave
		/// the exit unregistered.
		/// </summary>
		public static void Register()
		{
			KingdomConversion.AddPressureSource(Pressure);
		}

		// ==================================================================================
		// The Charter's own action
		// ==================================================================================

		/// <summary>
		/// The Charter's "share water with a settler" action: lists everyone standing here the
		/// founder could ask and what each would cost, and holds the rite on the one they pick.
		/// <para>
		/// Preconditions: a founded realm, the founder standing on its own claimed ground, and a
		/// creed the realm actually holds. Side effects: on a rite held, drams leave the dedicated
		/// stores, the settler's stored Creed may change through conversion or explicit adoption, the
		/// registers record the night, and the realm's rite cadence is stamped. Failure mode: every
		/// refusal to offer is a founder-facing line naming what would have to be different, and
		/// nothing is spent.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Founder">The Charter's own object, read for the ground it is standing on.</param>
		public static void OpenRite(KingdomSystem System, GameObject Founder)
		{
			KingdomSystem.Guard("water rite: charter", delegate
			{
				Register();
				if (System == null || Founder == null)
				{
					return;
				}
				if (!Enabled)
				{
					Popup.Show("You are not keeping account of what your people believe.");
					return;
				}
				if (!System.Founded)
				{
					Popup.Show("You rule nothing yet.");
					return;
				}
				Zone zone = Founder.CurrentZone;
				if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
				{
					Popup.Show(KingdomWaterRiteRules.BarLine(WaterRiteBar.NotOnOurGround, null, null, 0, 0));
					return;
				}
				string realmCreed = RealmCreed(System);
				if (string.IsNullOrEmpty(realmCreed))
				{
					Popup.Show(KingdomWaterRiteRules.BarLine(WaterRiteBar.RealmBelievesNothing, null, null, 0, 0));
					return;
				}
				while (true)
				{
					List<GameObject> people = CandidatesIn(System, zone);
					if (people.Count == 0)
					{
						Popup.Show("There is nobody standing in " + KingdomPresentation.Rich(System.SeatName) + " whose name the roll carries. Water is shared with a person, and a person has a name.");
						return;
					}
					int stored = KingdomGrowth.CountStoredWater(zone);
					List<RiteOffer> offers = new List<RiteOffer>();
					string[] options = new string[people.Count];
					for (int i = 0; i < people.Count; i++)
					{
						RiteOffer offer = OfferFor(System, zone, people[i], realmCreed, stored);
						offers.Add(offer);
						options[i] = KingdomWaterRiteRules.RowLabel(
							KingdomPresentation.Rich(NameOf(people[i])),
							KingdomCreed.CreedName(people[i].GetStringProperty(KingdomCreed.CreedProperty)),
							offer.Drams,
							offer.Bar,
							KingdomWaterRiteRules.AskedTooOften(people[i].GetIntProperty(RefusalsProperty)));
					}
					int picked = Popup.PickOption(
						Title: "Share water, at " + KingdomPresentation.Rich(System.SeatName),
						Intro: "The stores hold {{C|" + stored + " drams}}. " + KingdomPresentation.Rich(System.SeatName) + " holds with {{C|"
							+ KingdomCreed.CreedName(realmCreed) + "}}.\n\nYou are asking one person, and you are asking them once.",
						Options: options,
						AllowEscape: true);
					if (picked < 0 || picked >= offers.Count)
					{
						return;
					}
					RiteOffer chosen = offers[picked];
					if (chosen.Bar != WaterRiteBar.Ready)
					{
						Popup.Show(KingdomWaterRiteRules.BarLine(chosen.Bar,
							KingdomPresentation.Rich(NameOf(people[picked])),
							KingdomCreed.CreedName(realmCreed), chosen.Drams, stored));
						continue;
					}
					Hold(System, zone, people[picked], realmCreed, chosen);
					return;
				}
			});
		}

	}
}
