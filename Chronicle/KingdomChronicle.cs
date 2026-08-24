using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	internal sealed class KingdomChronicleDeclaration
	{
		internal readonly string EventId;
		internal readonly string Text;
		internal readonly bool Accomplishment;
		internal readonly string MuralText;
		internal readonly string Fingerprint;
		internal readonly string Official;
		internal readonly string Outsider;
		internal readonly string OfficialBefore;
		internal readonly string OfficialAfter;
		internal readonly string OutsiderBefore;
		internal readonly string OutsiderAfter;

		internal KingdomChronicleDeclaration(string EventId, string Text,
			bool Accomplishment, string MuralText, string Fingerprint, string Official,
			string Outsider, string OfficialBefore, string OfficialAfter,
			string OutsiderBefore, string OutsiderAfter)
		{
			this.EventId = EventId; this.Text = Text; this.Accomplishment = Accomplishment;
			this.MuralText = MuralText; this.Fingerprint = Fingerprint;
			this.Official = Official; this.Outsider = Outsider;
			this.OfficialBefore = OfficialBefore; this.OfficialAfter = OfficialAfter;
			this.OutsiderBefore = OutsiderBefore; this.OutsiderAfter = OutsiderAfter;
		}
	}

	public static class KingdomChronicle
	{
		public const int MaxEntries = KingdomChronicleReceiptRules.MaxEntries;
		private const string EventRegistryState = "r_TAF_ChronicleEventRegistry_v1";
		private const string EventRegistryFaultState = "r_TAF_ChronicleEventRegistryFault_v3";

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

		/// <summary>Caller-keyed chronicle publication. Each sink persists an explicit
		/// Pending/Attempting/Delivered/Skipped/Lost disposition. Inspectable list sinks recover
		/// only from exact canonical before/after hashes. The uninspectable journal sink never
		/// repeats an Attempting callback. A true return means every sink is durably settled,
		/// including an honestly Lost sink; it does not relabel loss as delivery.</summary>
		public static bool RecordOnce(KingdomSystem System, string EventId, string Text,
			bool Accomplishment = false, string MuralText = null)
		{
			return RecordOnceCore(System, EventId, Text, Accomplishment, MuralText, null);
		}

		/// <summary>Freezes exact caller content and both inspectable-list CAS tuples without
		/// publishing a Chronicle receipt or invoking any sink.</summary>
		internal static bool TryDeclareOnce(KingdomSystem System, string EventId, string Text,
			bool Accomplishment, string MuralText, out KingdomChronicleDeclaration Declaration)
		{
			Declaration = null;
			if (System == null || The.Game == null || System.ChronicleEntries == null ||
				System.OutsiderEntries == null || System.ChronicleEntries.Count > MaxEntries ||
				System.OutsiderEntries.Count > MaxEntries ||
				!KingdomChronicleReceiptRules.TryFingerprint(EventId, Text, Accomplishment,
					MuralText, out string fingerprint)) return false;
			string official;
			string outsider;
			try
			{
				official = "On the " + XRL.World.Calendar.GetDay() + " of "
					+ XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear()
					+ " AR, " + Text + ".";
				outsider = KingdomRules.ComposeOutsider(KingdomRules.ToThirdPerson(Text,
					FounderName()), DrawOutsiderRoll(System));
			}
			catch { return false; }
			if (string.IsNullOrEmpty(official) ||
				official.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
				string.IsNullOrEmpty(outsider) ||
				outsider.Length > KingdomChronicleReceiptRules.MaxEntryChars ||
				!KingdomChronicleReceiptRules.TryHashList("official", System.ChronicleEntries,
					out string officialBefore) ||
				!KingdomChronicleReceiptRules.TryHashAfter("official", System.ChronicleEntries,
					official, out string officialAfter) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", System.OutsiderEntries,
					out string outsiderBefore) ||
				!KingdomChronicleReceiptRules.TryHashAfter("outsider", System.OutsiderEntries,
					outsider, out string outsiderAfter)) return false;
			Declaration = new KingdomChronicleDeclaration(EventId, Text, Accomplishment,
				MuralText, fingerprint, official, outsider, officialBefore, officialAfter,
				outsiderBefore, outsiderAfter);
			return true;
		}

		internal static bool RecordDeclaredOnce(KingdomSystem System,
			KingdomChronicleDeclaration Declaration)
		{
			return Declaration != null && RecordOnceCore(System, Declaration.EventId,
				Declaration.Text, Declaration.Accomplishment, Declaration.MuralText, Declaration);
		}

		private static bool RecordOnceCore(KingdomSystem System, string EventId, string Text,
			bool Accomplishment, string MuralText, KingdomChronicleDeclaration Declaration)
		{
			string fingerprint;
			if (System == null || The.Game == null
				|| !KingdomChronicleReceiptRules.TryFingerprint(EventId, Text, Accomplishment,
					MuralText, out fingerprint) || (Declaration != null &&
					(!string.Equals(Declaration.EventId, EventId, StringComparison.Ordinal) ||
					 !string.Equals(Declaration.Text, Text, StringComparison.Ordinal) ||
					 Declaration.Accomplishment != Accomplishment ||
					 !string.Equals(Declaration.MuralText, MuralText, StringComparison.Ordinal) ||
					 !string.Equals(Declaration.Fingerprint, fingerprint,
						 StringComparison.Ordinal)))) return false;
			System.ChronicleEntries = System.ChronicleEntries ?? new List<string>();
			System.OutsiderEntries = System.OutsiderEntries ?? new List<string>();
			if (System.ChronicleEntries.Count > MaxEntries || System.OutsiderEntries.Count > MaxEntries)
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, "list-bound", true);
				return false;
			}
			string raw;
			try { raw = The.Game.GetStringGameState(EventRegistryState, ""); }
			catch
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, "registry-read", true);
				return false;
			}
			List<KingdomChronicleReceipt> rows;
			bool migratedLegacy;
			KingdomChronicleRegistryFault fault;
			if (!KingdomChronicleReceiptRules.TryParseRegistry(raw, out rows,
				out migratedLegacy, out fault))
			{
				ReportFault(fault, "registry-parse", true);
				return false;
			}
			if (migratedLegacy && !WriteEventReceipts(rows, "legacy-migration")) return false;

			KingdomChronicleReceipt receipt = null;
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].EventId, EventId, StringComparison.Ordinal)) receipt = rows[i];
			if (receipt != null && receipt.LegacyBlocked)
			{
				string ignoredJob;
				string ignoredCoordinate;
				if (KingdomChronicleReceiptRules.TryConstructionIdentity(EventId,
					out ignoredJob, out ignoredCoordinate))
				{
					// v1 FNV data cannot authorize another append. Construction callers need
					// a terminal answer so an old ceremony job cannot remain pinned forever.
					ReportFault(KingdomChronicleRegistryFault.None, "legacy-construction-lost", false);
					return true;
				}
				ReportFault(KingdomChronicleRegistryFault.None, "legacy-replay-blocked", true);
				return false;
			}
			if (receipt != null && !string.Equals(receipt.Fingerprint, fingerprint,
				StringComparison.Ordinal))
			{
				ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"fingerprint-mismatch", true);
				return false;
			}
			if (receipt != null && Declaration != null && !receipt.Compact &&
				(!string.Equals(receipt.Official, Declaration.Official, StringComparison.Ordinal) ||
				 !string.Equals(receipt.Outsider, Declaration.Outsider, StringComparison.Ordinal) ||
				 !string.Equals(receipt.OfficialBefore, Declaration.OfficialBefore,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OfficialAfter, Declaration.OfficialAfter,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OutsiderBefore, Declaration.OutsiderBefore,
					 StringComparison.Ordinal) ||
				 !string.Equals(receipt.OutsiderAfter, Declaration.OutsiderAfter,
					 StringComparison.Ordinal)))
			{
				ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
					"declaration-mismatch", true);
				return false;
			}
			if (receipt != null && receipt.Compact)
				return KingdomChronicleReceiptRules.IsTerminal(receipt);
			if (receipt != null && KingdomChronicleReceiptRules.IsTerminal(receipt))
				return WriteEventReceipts(rows, "terminal-compaction");
			if (receipt == null)
			{
				// No receipt is ever evicted: terminal identity is permanent replay proof.
				if (rows.Count >= KingdomChronicleReceiptRules.MaxReceipts)
				{
					ReportFault(KingdomChronicleRegistryFault.TooManyRows, "capacity", true);
					return false;
				}
				KingdomChronicleDeclaration declaration = Declaration;
				if (declaration == null && !TryDeclareOnce(System, EventId, Text,
					Accomplishment, MuralText, out declaration))
				{
					ReportFault(KingdomChronicleRegistryFault.CryptoUnavailable,
						"receipt-declaration", true);
					return false;
				}
				if (!KingdomChronicleReceiptRules.TryHashList("official",
						System.ChronicleEntries, out string declaredOfficialBefore) ||
					!KingdomChronicleReceiptRules.TryHashAfter("official",
						System.ChronicleEntries, declaration.Official,
						out string declaredOfficialAfter) ||
					!KingdomChronicleReceiptRules.TryHashList("outsider",
						System.OutsiderEntries, out string declaredOutsiderBefore) ||
					!KingdomChronicleReceiptRules.TryHashAfter("outsider",
						System.OutsiderEntries, declaration.Outsider,
						out string declaredOutsiderAfter) ||
					!string.Equals(declaredOfficialBefore, declaration.OfficialBefore,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOfficialAfter, declaration.OfficialAfter,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOutsiderBefore, declaration.OutsiderBefore,
						StringComparison.Ordinal) ||
					!string.Equals(declaredOutsiderAfter, declaration.OutsiderAfter,
						StringComparison.Ordinal))
				{
					ReportFault(KingdomChronicleRegistryFault.DuplicateIdentity,
						"declaration-list-mismatch", true);
					return false;
				}
				receipt = new KingdomChronicleReceipt
				{
					EventId = EventId,
					Fingerprint = fingerprint,
					Official = declaration.Official,
					Outsider = declaration.Outsider,
					OfficialBefore = declaration.OfficialBefore,
					OfficialAfter = declaration.OfficialAfter,
					OutsiderBefore = declaration.OutsiderBefore,
					OutsiderAfter = declaration.OutsiderAfter,
					OfficialState = KingdomChronicleSinkDisposition.Pending,
					OutsiderState = KingdomChronicleSinkDisposition.Pending,
					JournalState = Accomplishment
						? KingdomChronicleSinkDisposition.Pending
						: KingdomChronicleSinkDisposition.Skipped,
					Updated = Now()
				};
				rows.Add(receipt);
				if (!WriteEventReceipts(rows, "receipt-create")) return false;
			}
			if (!DeliverList(rows, receipt, System.ChronicleEntries, true)) return false;
			if (!DeliverList(rows, receipt, System.OutsiderEntries, false)) return false;
			if (!DeliverJournal(rows, receipt, Accomplishment, Text, MuralText)) return false;
			return KingdomChronicleReceiptRules.IsTerminal(receipt);
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
			System.ChronicleEntries.Add("On the " + XRL.World.Calendar.GetDay() + " of "
				+ XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear()
				+ " AR, " + Text + ".");
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

		private static bool DeliverList(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, List<string> Values, bool Official)
		{
			if (Rows == null || Receipt == null || Values == null || Values.Count > MaxEntries)
				return false;
			KingdomChronicleSinkDisposition state = Official
				? Receipt.OfficialState : Receipt.OutsiderState;
			if (KingdomChronicleReceiptRules.IsSettled(state)) return true;
			string register = Official ? "official" : "outsider";
			string value = Official ? Receipt.Official : Receipt.Outsider;
			string before = Official ? Receipt.OfficialBefore : Receipt.OutsiderBefore;
			string after = Official ? Receipt.OfficialAfter : Receipt.OutsiderAfter;
			string current;
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current))
				return LoseList(Rows, Receipt, Official, "list-hash");
			KingdomChronicleListAction action = KingdomChronicleReceiptRules.ListAction(
				state, current, before, after);
			if (action == KingdomChronicleListAction.ConfirmDelivered)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
				return WriteEventReceipts(Rows, register + "-confirm");
			}
			if (action != KingdomChronicleListAction.Append)
				return LoseList(Rows, Receipt, Official, register + "-interleaved");
			if (state == KingdomChronicleSinkDisposition.Pending)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Attempting);
				if (!WriteEventReceipts(Rows, register + "-intent")) return false;
			}
			// Persistence is an inspectable seam only through exact list state. Recompute
			// after intent: exact after confirms, exact before authorizes one append, and
			// anything else is unrelated interleaving and becomes Lost.
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current))
				return LoseList(Rows, Receipt, Official, register + "-rehash");
			action = KingdomChronicleReceiptRules.ListAction(
				KingdomChronicleSinkDisposition.Attempting, current, before, after);
			if (action == KingdomChronicleListAction.ConfirmDelivered)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
				return WriteEventReceipts(Rows, register + "-confirm-after-intent");
			}
			if (action != KingdomChronicleListAction.Append)
				return LoseList(Rows, Receipt, Official, register + "-interleaved-after-intent");
			try { KingdomChronicleReceiptRules.AppendBounded(Values, value); }
			catch { return LoseList(Rows, Receipt, Official, register + "-append"); }
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current)
				|| !string.Equals(current, after, StringComparison.Ordinal))
				return LoseList(Rows, Receipt, Official, register + "-after-mismatch");
			SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
			return WriteEventReceipts(Rows, register + "-delivered");
		}

		private static bool LoseList(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, bool Official, string Context)
		{
			SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Lost);
			bool written = WriteEventReceipts(Rows, Context + "-lost");
			if (written) ReportFault(KingdomChronicleRegistryFault.None, Context, true);
			return written;
		}

		private static void SetListState(KingdomChronicleReceipt Receipt, bool Official,
			KingdomChronicleSinkDisposition State)
		{
			if (Official) Receipt.OfficialState = State;
			else Receipt.OutsiderState = State;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
		}

		private static bool DeliverJournal(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, bool Accomplishment, string Text, string MuralText)
		{
			KingdomChronicleSinkDisposition state = Receipt.JournalState;
			if (KingdomChronicleReceiptRules.IsSettled(state)) return true;
			if (!Accomplishment)
			{
				Receipt.JournalState = state == KingdomChronicleSinkDisposition.Attempting
					? KingdomChronicleSinkDisposition.Lost
					: KingdomChronicleSinkDisposition.Skipped;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-not-requested");
			}
			if (state == KingdomChronicleSinkDisposition.Attempting)
			{
				// JournalAPI exposes no searchable key or receipt. Attempting after a save
				// cut is uncertain and must never repeat the callback.
				Receipt.JournalState = KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				bool written = WriteEventReceipts(Rows, "journal-reload-lost");
				if (written) ReportFault(KingdomChronicleRegistryFault.None,
					"journal-attempt-uncertain", true);
				return written;
			}
			bool enabled;
			try { enabled = XRL.UI.Options.GetOption("r_TAF_OptionChronicle") != "No"; }
			catch
			{
				ReportFault(KingdomChronicleRegistryFault.None, "journal-option", false);
				return false;
			}
			if (!enabled)
			{
				// Option-off is a frozen terminal choice for this event. Re-enabling does
				// not backlog old journal accomplishments.
				Receipt.JournalState = KingdomChronicleSinkDisposition.Skipped;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-skipped");
			}
			Receipt.JournalState = KingdomChronicleSinkDisposition.Attempting;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
			if (!WriteEventReceipts(Rows, "journal-intent")) return false;
			try
			{
				bool wantsMural = !string.IsNullOrEmpty(MuralText);
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".",
					wantsMural ? MuralText : null, null, null, "general",
					MuralCategory.CreatesSomething,
					wantsMural ? MuralWeight.Medium : MuralWeight.Nil, null, -1L);
			}
			catch
			{
				Receipt.JournalState = KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				bool written = WriteEventReceipts(Rows, "journal-callback-lost");
				if (written) ReportFault(KingdomChronicleRegistryFault.None,
					"journal-callback-uncertain", true);
				return written;
			}
			Receipt.JournalState = KingdomChronicleSinkDisposition.Delivered;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
			return WriteEventReceipts(Rows, "journal-delivered");
		}

		private static bool WriteEventReceipts(List<KingdomChronicleReceipt> Rows,
			string Context)
		{
			if (The.Game == null || Rows == null)
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context, false);
				return false;
			}
			for (int i = 0; i < Rows.Count; i++)
			{
				if (!Rows[i].Compact && KingdomChronicleReceiptRules.IsTerminal(Rows[i]))
				{
					KingdomChronicleReceipt compact = KingdomChronicleReceiptRules.Compact(Rows[i]);
					if (compact == null)
					{
						ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context, true);
						return false;
					}
					Rows[i] = compact;
				}
			}
			string value;
			KingdomChronicleRegistryFault fault;
			if (!KingdomChronicleReceiptRules.TryWriteRegistry(Rows, out value, out fault))
			{
				ReportFault(fault, Context,
					fault == KingdomChronicleRegistryFault.TooManyRows
					|| fault == KingdomChronicleRegistryFault.RegistryTooLong);
				return false;
			}
			try
			{
				The.Game.SetStringGameState(EventRegistryState, value);
				if (string.Equals(The.Game.GetStringGameState(EventRegistryState, ""), value,
					StringComparison.Ordinal)) return true;
			}
			catch { }
			ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context + "-write", true);
			return false;
		}

		/// <summary>Captures the exact realm-scoped replay registry before exile clears it.
		/// Parsing is required here so malformed evidence is preserved in place, not moved into an
		/// archive that a later return would treat as authority.</summary>
		internal static bool TryCaptureRealmRegistry(out string Registry, out string Fault,
			out string Failure)
		{
			Registry = null;
			Fault = null;
			Failure = null;
			if (The.Game == null)
			{
				Failure = "chronicle game state is unavailable";
				return false;
			}
			try
			{
				Registry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				Fault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
			}
			catch
			{
				Failure = "chronicle registry could not be read";
				return false;
			}
			List<KingdomChronicleReceipt> rows;
			bool migrated;
			KingdomChronicleRegistryFault parseFault = KingdomChronicleRegistryFault.None;
			if (Fault.Length > 160 ||
				!KingdomChronicleReceiptRules.TryParseRegistry(Registry, out rows,
					out migrated, out parseFault) || migrated)
			{
				Failure = "chronicle registry is malformed or noncanonical (" + parseFault + ")";
				return false;
			}
			return true;
		}

		/// <summary>Exact before/after CAS for exile. Either value may already be empty after a
		/// save cut; any third value is unrelated realm evidence and refuses the transition.</summary>
		internal static bool TryClearRealmRegistry(string ExpectedRegistry, string ExpectedFault,
			out string Failure)
		{
			return TryMoveRealmRegistry(ExpectedRegistry ?? "", ExpectedFault ?? "", "", "",
				out Failure);
		}

		/// <summary>Exact inverse CAS for return. A new realm's receipt graph is never overwritten;
		/// return is allowed only into the genuinely empty unfounded interval.</summary>
		internal static bool TryRestoreRealmRegistry(string ArchivedRegistry, string ArchivedFault,
			out string Failure)
		{
			return TryMoveRealmRegistry("", "", ArchivedRegistry ?? "", ArchivedFault ?? "",
				out Failure);
		}

		private static bool TryMoveRealmRegistry(string BeforeRegistry, string BeforeFault,
			string AfterRegistry, string AfterFault, out string Failure)
		{
			Failure = null;
			if (The.Game == null)
			{
				Failure = "chronicle game state is unavailable";
				return false;
			}
			try
			{
				string currentRegistry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				string currentFault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
				if ((currentRegistry != BeforeRegistry && currentRegistry != AfterRegistry) ||
					(currentFault != BeforeFault && currentFault != AfterFault))
				{
					Failure = "chronicle registry carries a third realm value";
					return false;
				}
				if (currentRegistry == BeforeRegistry)
					The.Game.SetStringGameState(EventRegistryState, AfterRegistry);
				currentRegistry = The.Game.GetStringGameState(EventRegistryState, "") ?? "";
				if (currentRegistry != AfterRegistry)
				{
					Failure = "chronicle registry CAS did not settle";
					return false;
				}
				currentFault = The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "";
				if (currentFault == BeforeFault)
					The.Game.SetStringGameState(EventRegistryFaultState, AfterFault);
				if ((The.Game.GetStringGameState(EventRegistryFaultState, "") ?? "") == AfterFault)
					return true;
			}
			catch { }
			Failure = "chronicle fault-register CAS did not settle";
			return false;
		}

		private static long Now()
		{
			return Math.Max(0L, The.Game == null ? 0L : The.Game.TimeTicks);
		}

		private static void ReportFault(KingdomChronicleRegistryFault Fault,
			string Context, bool PlayerVisible)
		{
			string code = ((int)Fault).ToString() + ":" + (Context ?? "unknown");
			if (code.Length > 160) code = code.Substring(0, 160);
			bool first = true;
			try
			{
				if (The.Game != null)
				{
					first = !string.Equals(The.Game.GetStringGameState(EventRegistryFaultState, ""),
						code, StringComparison.Ordinal);
					if (first) The.Game.SetStringGameState(EventRegistryFaultState, code);
				}
			}
			catch { }
			try { KingdomLog.Log("chronicle v3 refused " + code); }
			catch { }
			if (!PlayerVisible || !first) return;
			try
			{
				string line = Context == "capacity"
					? "{{r|The kingdom chronicle registry is full. This telling was refused; no replay receipt was discarded.}}"
					: "{{r|A kingdom chronicle receipt could not be proved. This telling was settled as lost or refused; no receipt was discarded.}}";
				XRL.Messages.MessageQueue.AddPlayerMessage(line);
			}
			catch { }
		}

		/// <summary>Returns the current city's persisted immutable id. Missing or mismatched
		/// provenance returns null; names are prose and never a draw or replay subject.</summary>
		internal static string SettlementId(KingdomSystem System)
		{
			return System?.CurrentSettlementId;
		}
	}
}
