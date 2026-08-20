using System.Text;
using Qud.API;
using XRL;
using XRL.Rules;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static class KingdomChronicle
	{
		public const int MaxEntries = 200;

		/// <summary>
		/// Rules version pinned into every outsider-register draw's <see cref="SemanticEventKey"/>.
		/// The key owns its rules version forever (see <c>KernelContracts.cs</c>), so this only
		/// moves if the outsider-register draw itself is redefined in a way that must not compare
		/// equal to what came before.
		/// </summary>
		private const int OutsiderRulesVersion = 1;

		/// <summary>Ordinal lane for outsider-register draws — one per settlement, never shared
		/// with any other kernel-backed draw.</summary>
		private const string OutsiderEventStreamId = "taf:chronicle:outsider:v1";

		private const uint OutsiderEventKind = 1u;

		/// <summary>
		/// Second draw index on the same key: the scriptorium's check on whether this telling
		/// embellishes. A distinct index rather than a distinct key, so both draws stay tied to
		/// the one chronicle event they describe.
		/// </summary>
		private const uint ScriptoriumDrawIndex = 1u;

		/// <summary>
		/// Fixed, all-zero seed. <c>KernelSeed128</c> documents an all-zero value as legal input;
		/// domain separation for this draw comes entirely from the settlement id, stream, kind,
		/// and ordinal baked into its <see cref="SemanticEventKey"/> (frozen precedence in
		/// <c>KernelContracts.cs</c>), so two different settlements — or the same settlement's
		/// two different chronicle events — never draw the same roll. A per-realm seed would only
		/// matter if this draw needed to be unguessable, and outsider flavor text does not.
		/// </summary>
		private static readonly KernelSeed128 OutsiderSeed = default(KernelSeed128);

		/// <summary>
		/// Prefix for the sanitized settlement id fed into every kernel draw key. Kept distinct
		/// from the faction's own <c>taf_kingdom_*</c> naming so a collision there can never alias
		/// a settlement id here.
		/// </summary>
		private const string SettlementIdPrefix = "taf:settlement:";

		/// <summary>
		/// Writes an event into the kingdom's chronicle in both registers: the official
		/// entry (dated in Qud's calendar) and an outsider retelling (third person, wrapped
		/// in rumor grammar). Both lists are capped at <see cref="MaxEntries"/>.
		/// </summary>
		/// <param name="System">The kingdom system; must be founded for names to read correctly.</param>
		/// <param name="Text">Lower-case clause with no trailing period, written from the
		/// founder's perspective, e.g. "the well ran dry" or "you poured the first water".
		/// Second-person phrasing is converted automatically for the outsider register.</param>
		/// <param name="Accomplishment">True to also file a journal accomplishment. Reserve this
		/// for milestones; ordinary events would spam the journal.</param>
		/// <param name="MuralText">
		/// Authored third-person line for the player's end-of-game murals, or null &mdash; which
		/// is what almost everything should pass.
		/// <para>
		/// Mural space is scarce and shared with the player's own life.
		/// <c>PlayerMuralController.initializeMurals</c> keeps at most sixteen accomplishments
		/// with non-empty mural text, and vanilla's Coda then draws its gospel from the first ten
		/// of those. A settlement that files a mural for every trade charter and repelled raid
		/// would push the player's actual history out of their own murals.
		/// </para>
		/// <para>
		/// Passing null is <b>not</b> enough on its own to stay out: `AddAccomplishment` derives
		/// mural text from the accomplishment text whenever mural text is null and the weight is
		/// not <c>Nil</c>, and silently overrides the category to <c>DoesSomethingRad</c> while
		/// doing it. Staying out requires the weight, which is why the two travel together here.
		/// </para>
		/// </param>
		public static void Record(KingdomSystem System, string Text, bool Accomplishment = false, string MuralText = null)
		{
			RecordDisputed(System, Text, null, Accomplishment, MuralText);
		}

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
			System.ChronicleEntries.Add("On the " + Calendar.GetDay() + " of " + Calendar.GetMonth() + ", " + Calendar.GetYear() + " AR, " + Text + ".");
			if (System.ChronicleEntries.Count > MaxEntries)
			{
				System.ChronicleEntries.RemoveAt(0);
			}
			string founder = FounderName();
			int roll = DrawOutsiderRoll(System);
			// Converted even when authored: a stray "your" in a hand-written rumour would put the
			// founder's own voice into the register that is supposed to be arguing with it.
			System.OutsiderEntries.Add(KingdomRules.ComposeOutsider(KingdomRules.ToThirdPerson(OutsiderText ?? Text, founder), roll));
			if (System.OutsiderEntries.Count > MaxEntries)
			{
				System.OutsiderEntries.RemoveAt(0);
			}
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
			string settlementId = SettlementId(System.KingdomFactionName);
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
			// The kernel draw refused — the settlement has no name yet, or this machine's crypto
			// provider is failing. Outsider flavor text is not gameplay-critical, so the chronicle
			// entry still gets written; it just loses the reload-stable drift for this one line
			// rather than being lost itself.
			roll = Stat.Random(0, fullRange - 1);
			return ApplyScriptorium(System, roll, key);
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
			else
			{
				chance = Stat.Random(0, 99);
			}
			if (chance < driftPercent)
			{
				return Roll;
			}
			int leads = KingdomRules.OutsiderLeads.Length;
			return (Roll % leads) + (PlainTailIndex() * leads);
		}

		/// <summary>
		/// Index of the empty tail — the telling that adds nothing after the deed. Found rather
		/// than hardcoded, so adding a tail to <see cref="KingdomRules.OutsiderTails"/> cannot
		/// silently repoint the scriptorium at an embellishment.
		/// </summary>
		private static int PlainTailIndex()
		{
			for (int i = 0; i < KingdomRules.OutsiderTails.Length; i++)
			{
				if (string.IsNullOrEmpty(KingdomRules.OutsiderTails[i]))
				{
					return i;
				}
			}
			return KingdomRules.OutsiderTails.Length - 1;
		}

		/// <summary>
		/// Folds a player-chosen kingdom name into the frozen <c>taf:</c> semantic-id grammar
		/// (<c>KernelSemanticId</c>): lowercase ASCII, digits, and <c>. _ : -</c> only, 5 to 128
		/// bytes. Anything else in the name becomes <c>-</c> so two differently-punctuated
		/// spellings of the same name still draw independent settlement ids rather than colliding
		/// on a stripped-down one.
		/// <para>
		/// The one definition of a settlement's kernel identity, shared rather than copied: every
		/// kernel-backed draw a settlement makes &mdash; the outsider register here, the named
		/// voices in <c>KingdomVoiceRules</c>, and whatever comes next &mdash; must key on the
		/// same id, or two of them would disagree about which settlement they belong to. Not
		/// supported API on purpose (STANDARDS.md &sect;9): the grammar it folds into is frozen,
		/// but which string this mod hands the kernel is ours to change.
		/// </para>
		/// </summary>
		/// <param name="FactionName">The realm's runtime faction name, or null before founding
		/// &mdash; which yields the id an unfounded settlement draws under.</param>
		/// <returns>An id that always satisfies the <c>taf:</c> grammar. Never null.</returns>
		internal static string SettlementId(string FactionName)
		{
			StringBuilder builder = new StringBuilder(SettlementIdPrefix);
			if (!string.IsNullOrEmpty(FactionName))
			{
				foreach (char c in FactionName)
				{
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == ':' || c == '-')
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length < SettlementIdPrefix.Length + 1)
			{
				builder.Append("unfounded");
			}
			if (builder.Length > 128)
			{
				builder.Length = 128;
			}
			return builder.ToString();
		}
	}
}
