using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		/// <summary>
		/// Writes an event whose two registers do not agree about it.
		/// <para>
		/// <see cref="Record"/> derives the outsider line from the official one mechanically, which
		/// is right for events the world has no reason to contest &mdash; a claim, a stage-up, a
		/// charter. It is wrong for the events the two-register chronicle exists for. When a realm
		/// puts its founder out, the founder's book and the roads are not telling the same story in
		/// two voices; they are telling two stories, and only this overload can carry both.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom system.</param>
		/// <param name="Text">The official register's clause, as <see cref="Record"/> takes it.</param>
		/// <param name="OutsiderText">The rumour register's own clause, already in third person
		/// (see <see cref="FounderName"/>), or null to derive it from <paramref name="Text"/> the
		/// ordinary way. Either way it is wrapped in the rumour grammar's hedges.</param>
		/// <param name="Accomplishment">True to also file a journal accomplishment.</param>
		/// <param name="MuralText">Authored mural line, or null. See <see cref="Record"/> for why
		/// null is what almost everything should pass.</param>
		public static void RecordDisputed(KingdomSystem System, string Text, string OutsiderText, bool Accomplishment = false, string MuralText = null)
		{
			KingdomChronicleReceiptRules.AppendBounded(System.ChronicleEntries,
				"On the " + XRL.World.Calendar.GetDay() + " of "
				+ XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear()
				+ " AR, " + Text + ".");
			string founder = FounderName();
			int roll = DrawOutsiderRoll(System);
			// Converted even when authored: a stray "your" in a hand-written rumour would put the
			// founder's own voice into the register that is supposed to be arguing with it.
			KingdomChronicleReceiptRules.AppendBounded(System.OutsiderEntries,
				KingdomRules.ComposeOutsider(KingdomRules.ToThirdPerson(
					OutsiderText ?? Text, founder), roll));
			if (Accomplishment && XRL.UI.Options.GetOption("r_TAF_OptionChronicle") != "No")
			{
				bool wantsMural = !string.IsNullOrEmpty(MuralText);
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".", wantsMural ? MuralText : null, null, null, "general", MuralCategory.CreatesSomething, wantsMural ? MuralWeight.Medium : MuralWeight.Nil, null, -1L);
			}
		}

		/// <summary>
		/// The founder as strangers would name them, for composing a rumour-register line that is
		/// already in third person.
		/// </summary>
		/// <returns>The player's stripped display name, or "the founder" when there is no player
		/// to ask &mdash; never null.</returns>
		public static string FounderName()
		{
			return The.Player?.BaseDisplayNameStripped ?? "the founder";
		}

		/// <summary>
		/// Draws the outsider register's roll through <see cref="CounterRandom"/> instead of an
		/// ordinary <c>Stat.Random</c> call, keyed on the settlement and this event's position in
		/// the outsider stream (<see cref="System.OutsiderEntries"/>'s length before the add) so
		/// the same chronicle event always drifts the same way on reload — an ordinary
		/// pseudorandom call cannot promise that, because its cursor position depends on every
		/// unrelated roll made since the process started.
		/// <para>
		/// An academy district narrows the drawable range via
		/// <see cref="KingdomRules.DistrictsDriftPercent"/>: fewer of the six lead/tail
		/// combinations are reachable, so the outsider telling stays closer to the true record.
		/// </para>
		/// </summary>
		private static int DrawOutsiderRoll(KingdomSystem System)
		{
			int fullRange = KingdomRules.OutsiderLeads.Length * KingdomRules.OutsiderTails.Length;
			// The ordinal is the tick the event happened on, not the entry count. The register
			// is trimmed to MaxEntries, so its count stops rising at 200 and every later entry
			// would key identically and drift identically. Ticks only ever go forward. Two
			// events recorded on the same tick share a drift, which is a cosmetic tie and not
			// the silent single-value collapse the count produced.
			ulong ordinal = (ulong)The.Game.TimeTicks;
			string settlementId = SettlementId(System);
			SemanticEventKey key;
			KernelFaultCode fault;
			int roll;
			if (SemanticEventKey.TryCreate(OutsiderRulesVersion, settlementId, OutsiderEventStreamId, OutsiderEventKind, ordinal, out key, out fault))
			{
				ulong value;
				if (CounterRandom.TryDrawBelow(OutsiderSeed, key, 0u, (ulong)fullRange, out value, out fault))
				{
					roll = (int)value;
					return ApplyScriptorium(System, roll, key);
				}
			}
			// Missing immutable identity or cryptographic failure grants no mutable random cursor.
			// Keep the entry, but choose the unembellished telling deterministically.
			return PlainTailIndex() * KingdomRules.OutsiderLeads.Length;
		}

		/// <summary>
		/// An academy district is a scriptorium: someone there is writing the tellings down and
		/// checking them. <see cref="KingdomRules.DistrictsDriftPercent"/> is the chance the
		/// telling still embellishes; the rest of the time the copy that travels keeps its lead
		/// but loses the embroidered tail, which is the only part of the composition that claims
		/// something the record does not say.
		/// <para>
		/// Narrowing the draw's range would do the opposite of what it looks like: the plain tail
		/// is the LAST entry of <see cref="KingdomRules.OutsiderTails"/>, so a smaller bound makes
		/// the register more florid, not less.
		/// </para>
		/// </summary>
		private static int ApplyScriptorium(KingdomSystem System, int Roll, SemanticEventKey Key)
		{
			int driftPercent = KingdomRules.DistrictsDriftPercent(System.ZoneDistricts.Values);
			// Addendum 6: a great scriptorium with an archivist at its head reaches the whole city,
			// so it checks the telling wherever the academy district would have. Best wins and
			// nothing stacks, the same law the districts themselves aggregate under.
			if (KingdomReach.CityShaded(System, KingdomReach.LearningSupport)
				&& KingdomRules.DistrictAcademyDriftPercent < driftPercent)
			{
				driftPercent = KingdomRules.DistrictAcademyDriftPercent;
			}
			if (driftPercent >= 100)
			{
				return Roll;
			}
			if (driftPercent < 0)
			{
				driftPercent = 0;
			}
			int chance;
			ulong value;
			KernelFaultCode fault;
			if (CounterRandom.TryDrawBelow(OutsiderSeed, Key, ScriptoriumDrawIndex, 100uL, out value, out fault))
			{
				chance = (int)value;
			}
			else return PlainTailIndex() * KingdomRules.OutsiderLeads.Length;
			if (chance < driftPercent)
			{
				return Roll;
			}
			int leads = KingdomRules.OutsiderLeads.Length;
			return (Roll % leads) + (PlainTailIndex() * leads);
		}

	}
}
