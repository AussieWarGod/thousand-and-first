using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>
		/// Who lives in the seated city, as the creed stack has to see them: the head count, the
		/// countries they walked in from, what they hold with, and what they have held and left
		/// (<c>KingdomSystem.CreedPastCounts</c>).
		/// <para>
		/// Read off the city's own tallies rather than off the ground, so it answers for a city
		/// whose people are not loaded &mdash; which is every city the founder is not standing in,
		/// and the seated one before its zone has been walked.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded yields
		/// <c>BuilderRoll.Unknown</c>, which permits every creed gate.</param>
		public static BuilderRoll BuilderRollOf(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return BuilderRoll.Unknown;
			}
			return new BuilderRoll(System.Population, System.OriginCounts, System.CreedCounts, System.CreedPastCounts);
		}

		/// <summary>
		/// Whether a design is OFFERED to this settlement at all &mdash; the one question every
		/// menu that lists the catalogue asks, so that they all ask it the same way.
		/// <para>
		/// Style, stage, and one more: Addendum 14's visibility law as Addendum 16 applies it to
		/// creed-works. <b>You see what you have unlocked, you do not see what you have not, and
		/// you especially cannot see what you CAN'T unlock.</b> Everything else in this file's
		/// gates is a door with a key somewhere &mdash; a disk to carry home, a machine to certify,
		/// a parasang to claim, ground to name &mdash; and every one of those designs stays in the
		/// list wearing the tag that says which key (<see cref="GateNote"/>), because a list that
		/// silently shortens teaches nothing.
		/// </para>
		/// <para>
		/// A creed nobody here holds and nobody here has ever held is the one gate with no key at
		/// all. There is nothing the founder could go and do about it, so naming it would be noise
		/// dressed as guidance, and the design is not shown. The moment one person aligns &mdash;
		/// by arriving, by converting, or by having converted away years ago &mdash; the design
		/// appears, tagged with whatever is still in its way.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded offers nothing.</param>
		/// <param name="Entry">The design. Null is not offered.</param>
		public static bool Offered(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null)
			{
				return false;
			}
			if (KingdomHostedArcologyRules.IsHostedLotKey(Entry.Key)) return false;
			// The five civic-heart records are rite-owned internal growth rungs. They are never
			// independent commissions: successor work renovates and/or expands the same heart identity.
			if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
			{
				return false;
			}
			if (!KingdomRules.StyleAllows(Entry.Styles, KingdomData.StyleKeys(System.Style))
				|| System.Stage < Entry.MinStage)
			{
				return false;
			}
			return Visible(System, Entry);
		}

		/// <summary>
		/// The visibility half of <see cref="Offered"/> on its own, for a caller that has already
		/// answered style and stage.
		/// <para>
		/// Fails OPEN, like every other judgment in this file: if the question throws, the design
		/// is shown. A founder who sees one design they cannot raise is told why by
		/// <see cref="GateNote"/>; a founder who cannot see a design they CAN raise has no way to
		/// find out it exists.
		/// </para>
		/// </summary>
		public static bool Visible(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			bool hidden = false;
			KingdomSystem.Guard("zoning visibility", delegate
			{
				if (!Enabled || System == null || !System.Founded || Entry == null || string.IsNullOrEmpty(Entry.Key))
				{
					return;
				}
				ZoneGate gate = GateFor(Entry.Key);
				// The second gate with no key: a design that waits on a thing the founder has never
				// heard of. Named here rather than in the catalogue or in the map, because this is
				// the one question every menu, every map row and every refusal already funnels
				// through -- so a third party's building gated on a hidden node is filtered by
				// exactly the code that filters ours. Vanilla's own precedent for an unknown recipe
				// is total omission: no greyed row, no silhouette, no count.
				if (!KingdomResearch.KnowledgeGateHeardOf(System, gate.Knowledge))
				{
					hidden = true;
					return;
				}
				if (string.IsNullOrEmpty(gate.Creed))
				{
					return;
				}
				hidden = KingdomZoningRules.NoPathToCreed(BuilderRollOf(System), gate.Creed);
			});
			return !hidden;
		}

		/// <summary>
		/// The settlement's verdict on raising one design on one piece of ground, with the module
		/// switch and every null case already folded in.
		/// </summary>
		/// <param name="System">The realm. Null permits &mdash; there is nothing to gate for.</param>
		/// <param name="ZoneID">Zone the work would stand in; its district is looked up in
		/// <c>KingdomSystem.ZoneDistricts</c>. Null or unclaimed ground reads as undistricted.</param>
		/// <param name="Entry">The design. Null permits.</param>
		public static ZoningJudgement Judge(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry)
		{
			return JudgeAt(System, DistrictOf(System, ZoneID), Entry, StratumOf(ZoneID));
		}

		/// <summary>
		/// Whether a zone id names ground below the surface, read off the id itself rather than
		/// off a loaded zone: the stratum is in the id, so the offer can be narrowed for ground
		/// the founder is standing on and for ground they are only planning on. An id this build
		/// cannot parse reads as the surface, which gates nothing.
		/// </summary>
		public static bool StratumOf(string ZoneID)
		{
			if (string.IsNullOrEmpty(ZoneID) || !KingdomRules.TryParseZoneID(ZoneID, out _, out _, out _, out int z))
			{
				return false;
			}
			return KingdomPlotRules.IsUnderground(z);
		}

		/// <summary>
		/// Whether a design may be raised here, with a whole player-facing sentence when it may
		/// not. The one call a commission path needs.
		/// <para>
		/// Fails OPEN: if judging itself throws, the design is permitted and the fault is logged.
		/// A bug in a gate must never be able to make a settlement unbuildable, and a founder who
		/// gets one building they should not have is a far smaller harm than one who gets a
		/// refusal nothing in the game can explain.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="ZoneID">Zone the work would stand in.</param>
		/// <param name="Entry">The design.</param>
		/// <param name="Failure">Set to the refusal when this returns false; untouched otherwise.</param>
		public static bool Permits(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry, out string Failure)
		{
			if (Entry != null && Entry.Key == KingdomHostedArcology.ArcologyKey
				&& !KingdomHostedArcology.CanReserveAt(System, ZoneID, out Failure)) return false;
			string refusal = null;
			KingdomSystem.Guard("zoning gate", delegate
			{
				ZoningJudgement judgement = Judge(System, ZoneID, Entry);
				if (!judgement.Permitted)
				{
					refusal = Refusal(System, ZoneID, Entry, judgement);
				}
			});
			Failure = refusal;
			return refusal == null;
		}

		/// <summary>
		/// The short coloured tag a commission or plan menu line carries when a design is
		/// blocked, so the founder sees the whole catalog and which parts of it are out of reach
		/// rather than a list that silently shortens. Null when the design may be raised.
		/// </summary>
		public static string GateNote(KingdomSystem System, string ZoneID, KingdomRules.BuildEntry Entry)
		{
			string note = null;
			KingdomSystem.Guard("zoning note", delegate
			{
				ZoningJudgement judgement = Judge(System, ZoneID, Entry);
				if (!judgement.Permitted && judgement.Note != null)
				{
					note = " {{K|[" + judgement.Note + "]}}";
				}
			});
			return note;
		}

		/// <summary>
		/// What designating this ground would cost, named before it costs it: the designs the
		/// founder can raise here today and could not raise here afterward. Zoning is meant to be
		/// a decision, and a decision the founder cannot see the price of is a trap.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="ZoneID">The ground about to be designated.</param>
		/// <param name="District">The district key being proposed.</param>
		/// <returns>A founder-facing sentence, or null when nothing would be shut out.</returns>
		public static string LockoutWarning(KingdomSystem System, string ZoneID, string District)
		{
			string warning = null;
			KingdomSystem.Guard("zoning lockout warning", delegate
			{
				if (System == null || !System.Founded || !Enabled)
				{
					return;
				}
				string current = DistrictOf(System, ZoneID);
				List<string> lost = new List<string>();
				foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
				{
					if (!Offered(System, entry))
					{
						continue;
					}
					// Judged on this ground's own stratum on both sides, so the warning names what
					// the DISTRICT would cost and never what the rock already forbids.
					bool underground = StratumOf(ZoneID);
					if (JudgeAt(System, current, entry, underground).Permitted && !JudgeAt(System, District, entry, underground).Permitted && !lost.Contains(entry.Name))
					{
						lost.Add(entry.Name);
					}
				}
				if (lost.Count > 0)
				{
					warning = "Naming this ground the " + KingdomRules.DistrictName(District) + " puts "
						+ KingdomZoningRules.JoinAnd(lost) + " beyond what may be raised here. Nothing already standing is touched, and the ground can be named again later.";
				}
			});
			return warning;
		}

	}
}
