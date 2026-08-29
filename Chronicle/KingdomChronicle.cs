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

	public static partial class KingdomChronicle
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

	}
}
