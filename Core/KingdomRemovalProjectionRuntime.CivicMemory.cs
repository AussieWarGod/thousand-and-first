using System;
using System.Collections.Generic;
using System.Globalization;
using Qud.API;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const int MaxCivicMemorySectionArchiveChars =
			((KingdomCivicMemoryLimits.MaxTreatyBytes + 2) / 3) * 4;

		private static bool TryInspectCivicMemory(KingdomSystem System,
			List<string> Rows, out string Failure)
		{
			if (!TryReadCivicMemory(out List<KingdomCivicMemorySection> sections,
				out Failure)) return false;
			AddCivicMemoryRows(sections, Rows);
			return true;
		}

		private static void AddCivicMemoryRows(IList<KingdomCivicMemorySection> Sections,
			List<string> Rows)
		{
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
			{
				KingdomCivicMemorySection section = FindSection(Sections, id);
				if (section == null) { Rows.Add("c18-section\u001f" + id + "\u001fabsent"); continue; }
				string payload = Convert.ToBase64String(section.Payload());
				Rows.Add("c18-section\u001f" + id + "\u001f" + section.Length + "\u001f"
					+ KingdomRetirementDigestRules.Evidence("c18-section-payload-v1",
						new List<string> { payload }));
			}
		}

		private static bool TryProjectCivicMemoryRetirement(KingdomSystem System, long Tick,
			out List<string> Live, out List<string> Projected, out int PendingWitnessRows,
			out string Failure)
		{
			Live = new List<string>(); Projected = new List<string>();
			PendingWitnessRows = 0; Failure = null;
			if (System == null || Tick < 0L
				|| !TryReadCivicMemory(out List<KingdomCivicMemorySection> sections,
					out Failure)) return false;
			AddCivicMemoryRows(sections, Live);
			List<KingdomCivicMemorySection> terminal = new List<KingdomCivicMemorySection>();
			for (int i = 0; i < sections.Count; i++)
				terminal.Add(new KingdomCivicMemorySection(sections[i].Id, sections[i].Payload()));
			KingdomCivicMemorySection artifacts = FindSection(terminal,
				KingdomCivicMemoryLimits.SectionCivicArtifacts);
			if (artifacts != null)
			{
				KingdomCivicArtifactsEnvelope value = KingdomCivicArtifactsStore.ReadForRealm(
					artifacts.Payload(), System.RealmId, out Failure);
				if (value == null || value.Quarantined || value.IsOpaqueFuture
					|| !string.IsNullOrEmpty(Failure)
					|| !KingdomCivicArtifactsStore.TryValidateIdentity(value, out Failure))
					return Fail(Failure ?? "section-1 witness authority is not current", out Failure);
				KingdomCivicArtifactsEnvelope next = KingdomCivicArtifactsStore.Copy(value);
				for (int i = 0; i < next.WitnessWorks.Rows.Count; i++)
				{
					KingdomWitnessWorkReceipt row = next.WitnessWorks.Rows[i];
					if (row.Phase != KingdomWitnessWorkPhase.CarrierPrepared
						&& row.Phase != KingdomWitnessWorkPhase.Projected) continue;
					PendingWitnessRows++;
					if (!KingdomWitnessWorkRules.TryReconcileCarrier(next.WitnessWorks,
						next.WitnessWorks.Revision, row.WorkId, true, true, Tick,
						out Failure)) return false;
				}
				if (PendingWitnessRows > 0)
				{
					if (!KingdomCivicArtifactsStore.TryWrite(next, out byte[] payload, out Failure))
						return false;
					for (int i = 0; i < terminal.Count; i++)
						if (terminal[i].Id == KingdomCivicMemoryLimits.SectionCivicArtifacts)
							terminal[i] = new KingdomCivicMemorySection(terminal[i].Id, payload);
				}
			}
			AddCivicMemoryRows(terminal, Projected);
			return true;
		}

		internal static bool TryValidateWitnessRetirementLocators(KingdomSystem System,
			IList<KingdomRemovalLocator> Locators, out string Failure)
		{
			Failure = null;
			if (System == null || Locators == null
				|| !TryReadCivicMemory(out List<KingdomCivicMemorySection> sections,
					out Failure)) return false;
			KingdomCivicMemorySection artifacts = FindSection(sections,
				KingdomCivicMemoryLimits.SectionCivicArtifacts);
			if (artifacts == null) return true;
			KingdomCivicArtifactsEnvelope value = KingdomCivicArtifactsStore.ReadForRealm(
				artifacts.Payload(), System.RealmId, out Failure);
			if (value == null || value.Quarantined || value.IsOpaqueFuture
				|| !string.IsNullOrEmpty(Failure)
				|| !KingdomCivicArtifactsStore.TryValidateIdentity(value, out Failure))
				return Fail(Failure ?? "section-1 witness authority is not current", out Failure);
			for (int i = 0; i < value.WitnessWorks.Rows.Count; i++)
			{
				KingdomWitnessWorkReceipt row = value.WitnessWorks.Rows[i];
				if (row.Phase != KingdomWitnessWorkPhase.CarrierPrepared
					&& row.Phase != KingdomWitnessWorkPhase.Projected) continue;
				if (string.IsNullOrEmpty(row.CarrierZoneId)
					|| !row.CarrierZoneId.StartsWith("taf:zone:", StringComparison.Ordinal))
					return Fail("live fixed-witness row has no canonical carrier zone", out Failure);
				string zone = row.CarrierZoneId.Substring("taf:zone:".Length);
				bool tracked = false;
				for (int j = 0; j < Locators.Count; j++)
					if (Locators[j].ZoneId == zone) { tracked = true; break; }
				if (!tracked)
					return Fail("live fixed-witness row lies outside attended retirement ground: "
						+ zone, out Failure);
			}
			return true;
		}

		private static bool TryPrepareCivicMemoryNotes(KingdomSystem System, long Tick,
			List<NativeCivicNotePlan> Notes, out string Failure)
		{
			Failure = null;
			if (System == null || Notes == null
				|| !TryReadCivicMemory(out List<KingdomCivicMemorySection> sections,
					out Failure)) return false;
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
			{
				KingdomCivicMemorySection section = FindSection(sections, id);
				bool present = section != null;
				string payload = present ? Convert.ToBase64String(section.Payload()) : "";
				string status = present ? "present" : "absent";
				string digest = KingdomRetirementDigestRules.Evidence("c18-native-v1",
					new List<string> { System.RealmId, id.ToString(
						CultureInfo.InvariantCulture), status, payload });
				if (payload.Length > MaxCivicMemorySectionArchiveChars)
					return Fail("civic-memory native archive exceeds one bounded section note",
						out Failure);
				JournalObservation note = NativeCivicMemoryNote(System, Tick, id, status,
					digest, 0, 1, payload);
				if (!TryClassifyNativeCivicMemoryNote(note,
					out NativeCivicNotePlan plan, out Failure)) return false;
				Notes.Add(plan);
			}
			return true;
		}

		private static bool TryReadCivicMemory(
			out List<KingdomCivicMemorySection> Sections, out string Failure)
		{
			Sections = null; Failure = null;
			if (The.Game?.Systems == null)
				return Fail("civic-memory registry is absent", out Failure);
			KingdomCivicMemorySystem found = null;
			for (int i = 0; i < The.Game.Systems.Count; i++)
				if (The.Game.Systems[i]?.GetType() == typeof(KingdomCivicMemorySystem))
				{
					if (found != null) return Fail("civic-memory system is duplicated", out Failure);
					found = (KingdomCivicMemorySystem)The.Game.Systems[i];
				}
			if (found == null) return Fail("civic-memory system is absent", out Failure);
			KingdomCivicMemoryState state = found.Read();
			if (state == null || state.Quarantined || state.IsFutureOuter || state.HasFutureSections)
				return Fail("civic memory is absent, quarantined, or future-versioned", out Failure);
			Sections = state.Sections();
			for (int i = 0; i < Sections.Count; i++)
				if (!KingdomCivicMemoryLimits.Known(Sections[i].Id)
					|| Sections[i].Length > KingdomCivicMemoryLimits.SectionCap(Sections[i].Id))
					return Fail("civic-memory section is unknown or outside its native cap", out Failure);
			return true;
		}

		private static KingdomCivicMemorySection FindSection(
			IList<KingdomCivicMemorySection> Sections, int Id)
		{
			for (int i = 0; i < (Sections?.Count ?? 0); i++)
				if (Sections[i].Id == Id) return Sections[i];
			return null;
		}

		private static JournalObservation NativeCivicMemoryNote(KingdomSystem System,
			long Tick, int Section, string Status, string Digest, int Index, int Count,
			string Chunk)
		{
			return new JournalObservation
			{
				Time = Tick, Category = "civic history", RevealText = null, Rumor = false,
				ID = "taf-c18-" + Section + "-" + Digest + "-" + Index,
				History = "",
				Text = "Before " + KingdomPresentation.Rich(System.KingdomDisplayName
					?? System.SeatName ?? "the realm")
					+ " retired its charter, civic-memory section " + Section + " was "
					+ (Status == "present" ? "preserved exactly" : "recorded as absent")
					+ " (part " + (Index + 1) + " of " + Count + ").",
				LearnedFrom = "Retired realms", Revealed = true, Tradable = false,
				Attributes = new List<string> { "civic memory", "retired realm", "exact archive",
					NativeArchiveAttribute("taf-c18-v1|" + Section + "|" + Status + "|"
						+ Index + "|" + Count + "|" + Chunk) }
			};
		}

		private static bool TryClassifyNativeCivicMemoryNote(JournalObservation Note,
			out NativeCivicNotePlan Plan, out string Failure)
		{
			return TryPlanNativeCivicNote(Note, out Plan, out Failure);
		}
	}
}
