using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Qud.API;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const string NativeArchiveAttributePrefix = "taf:retired-archive:v1:";

		private sealed class NativeCivicNotePlan
		{
			internal JournalObservation Note;
			internal bool AddToList;
			internal bool AddToIndex;
		}

		internal static bool TryInspectCivicSemantics(KingdomSystem System,
			out List<string> Rows, out string Failure)
		{
			if (!TryInspectExperienceSemantics(System, out Rows, out Failure)) return false;
			if (!TryInspectCivicMemory(System, Rows, out Failure)) return false;
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		private static bool TryInspectExperienceSemantics(KingdomSystem System,
			out List<string> Rows, out string Failure)
		{
			return TryExperienceRows(System?.Experience, System?.RealmId, out Rows,
				out Failure);
		}

		private static bool TryExperienceRows(KingdomExperienceLedger Ledger, string RealmId,
			out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			if (Ledger == null || !KingdomExperienceRules.TryValidate(Ledger, out Failure))
				return Fail(Failure ?? "civic semantic ledger is absent or foreign", out Failure);
			int count = Ledger.Offices.Count + Ledger.Remembrances.Count + Ledger.Voices.Count
				+ Ledger.FirstFeasts.Count;
			if (count > 0 && Ledger.RealmId != RealmId)
				return Fail("civic semantic ledger belongs to another realm", out Failure);
			for (int i = 0; i < Ledger.Offices.Count; i++)
				Rows.Add("office\u001f" + Canonical(Ledger.Offices[i]));
			for (int i = 0; i < Ledger.Remembrances.Count; i++)
				Rows.Add("remembrance\u001f" + Canonical(Ledger.Remembrances[i]));
			for (int i = 0; i < Ledger.Voices.Count; i++)
				Rows.Add("voice\u001f" + Canonical(Ledger.Voices[i]));
			for (int i = 0; i < Ledger.FirstFeasts.Count; i++)
				Rows.Add("first-feast\u001f" + Canonical(Ledger.FirstFeasts[i]));
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryInspectCivicRetirementProjection(KingdomSystem System,
			long Tick, out List<string> Live, out List<string> Projected,
			out int PendingWitnessRows, out string Failure)
		{
			Live = null; Projected = null; PendingWitnessRows = 0; Failure = null;
			if (!TryInspectExperienceSemantics(System, out List<string> liveExperience,
				out Failure) || !TryProjectCivicMemoryRetirement(System, Tick,
					out List<string> liveC18, out List<string> projectedC18,
					out PendingWitnessRows, out Failure)) return false;
			KingdomExperienceLedger terminal = KingdomExperienceRules.Clone(System.Experience);
			if (terminal.Voices.Count > 0 && !KingdomExperienceRules.TryRetireCivicVoices(
				terminal, System.RealmId, terminal.Revision, out Failure)) return false;
			if (terminal.FirstFeasts.Count > 0 && !KingdomExperienceRules.TryRetireFirstFeasts(
				terminal, System.RealmId, terminal.Revision, out Failure)) return false;
			if (!TryExperienceRows(terminal, System.RealmId,
				out List<string> projectedExperience, out Failure)) return false;
			Live = new List<string>(liveExperience); Live.AddRange(liveC18);
			Projected = new List<string>(projectedExperience); Projected.AddRange(projectedC18);
			Live.Sort(StringComparer.Ordinal); Projected.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryRetireCivicSemantics(KingdomSystem System, long Tick,
			string ExpectedProjectedDigest, out int Converted, out string Failure)
		{
			Converted = 0; Failure = null;
			if (!TryInspectCivicRetirementProjection(System, Tick, out List<string> _,
				out List<string> projected, out int pendingWitness, out Failure)
				|| pendingWitness != 0
				|| KingdomRetirementDigestRules.Evidence("removal-preview-civic-semantics",
					projected) != ExpectedProjectedDigest)
				return Fail(Failure ?? "civic retirement projection is not terminal or frozen",
					out Failure);
			List<NativeCivicNotePlan> notes = new List<NativeCivicNotePlan>();
			if (!TryPrepareExperienceNotes(System, Tick, notes, out Failure)
				|| !TryPrepareCivicMemoryNotes(System, Tick, notes, out Failure)
				|| !TryPublishNativeCivicNotes(notes, out Failure)) return false;
			Converted = notes.Count;
			if (System.Experience.Voices.Count > 0
				&& !KingdomExperienceRules.TryRetireCivicVoices(System.Experience,
					System.RealmId, System.Experience.Revision, out Failure)) return false;
			if (System.Experience.FirstFeasts.Count > 0
				&& !KingdomExperienceRules.TryRetireFirstFeasts(System.Experience,
					System.RealmId, System.Experience.Revision, out Failure)) return false;
			return TryInspectCivicRetirementProjection(System, Tick, out List<string> after,
				out List<string> terminal, out pendingWitness, out Failure)
				&& pendingWitness == 0 && SameRows(after, terminal)
				&& KingdomRetirementDigestRules.Evidence(
					"removal-preview-civic-semantics", after) == ExpectedProjectedDigest
				&& TryPrepareCivicMemoryNotes(System, Tick,
					new List<NativeCivicNotePlan>(),
					out Failure)
				|| Fail(Failure ?? "civic semantics changed during additive native preservation",
					out Failure);
		}

		private static bool TryPrepareNativeCivicNote(KingdomSystem System, long Tick,
			object Row,
			out NativeCivicNotePlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (System == null || Row == null || Tick < 0L
				|| !TryReadableCivicText(System, Row, out string text, out Failure)) return false;
			string wire = Canonical(Row);
			string digest = KingdomRetirementDigestRules.Evidence("civic-memory-native-v1",
				new List<string> { System.RealmId, wire });
			JournalObservation note = new JournalObservation
			{
				Time = Tick, Category = "civic history", RevealText = null, Rumor = false,
				ID = "taf-civic-memory-" + digest, History = "",
				Text = CivicText(System, text, digest),
				LearnedFrom = "Retired realms", Revealed = true, Tradable = false,
				Attributes = new List<string> { "civic memory", "retired realm",
					NativeArchiveAttribute(wire) }
			};
			return TryPlanNativeCivicNote(note, out Plan, out Failure);
		}

		private static bool TryPlanNativeCivicNote(JournalObservation Note,
			out NativeCivicNotePlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (JournalAPI.Observations == null || JournalAPI.NotesByID == null)
				return Fail("native journal indexes are absent", out Failure);
			JournalObservation listed = null;
			for (int i = 0; i < JournalAPI.Observations.Count; i++)
				if (JournalAPI.Observations[i]?.ID == Note.ID)
				{
					if (listed != null) return Fail("native civic-memory note identity is duplicated",
						out Failure);
					listed = JournalAPI.Observations[i];
				}
			if (listed != null && !SameNativeCivicNote(listed, Note))
				return Fail("native civic-memory note identity collides", out Failure);
			bool indexed = JournalAPI.NotesByID.TryGetValue(Note.ID,
				out IBaseJournalEntry existing);
			JournalObservation prior = existing as JournalObservation;
			if (indexed && (prior == null || prior.GetType() != typeof(JournalObservation)
				|| !SameNativeCivicNote(prior, Note)
				|| (listed != null && !ReferenceEquals(prior, listed))))
				return Fail("native civic-memory note identity collides", out Failure);
			Plan = new NativeCivicNotePlan
			{
				Note = prior ?? listed ?? Note,
				AddToList = listed == null,
				AddToIndex = !indexed
			};
			return true;
		}

		private static bool TryPublishNativeCivicNotes(IList<NativeCivicNotePlan> Plans,
			out string Failure)
		{
			Failure = null;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < (Plans?.Count ?? 0); i++)
				if (Plans[i]?.Note == null || !ids.Add(Plans[i].Note.ID))
					return Fail("native civic-memory publication plan is invalid or duplicated",
						out Failure);
			for (int i = 0; i < Plans.Count; i++)
			{
				NativeCivicNotePlan plan = Plans[i];
				try
				{
					if (plan.AddToList) JournalAPI.Observations.Add(plan.Note);
					if (plan.AddToIndex) JournalAPI.NotesByID.Add(plan.Note.ID, plan.Note);
				}
				catch (Exception error)
				{
					return Fail("native civic-memory publication stopped after an exact prefix ("
						+ error.GetType().Name + ")", out Failure);
				}
				if (!TryPlanNativeCivicNote(plan.Note, out NativeCivicNotePlan proved,
					out Failure) || proved.AddToList || proved.AddToIndex)
					return Fail(Failure ?? "native civic-memory note did not reach both indexes",
						out Failure);
			}
			return true;
		}

		private static bool SameNativeCivicNote(JournalObservation A, JournalObservation B)
		{
			return A != null && B != null && A.GetType() == typeof(JournalObservation)
				&& B.GetType() == typeof(JournalObservation) && A.Time == B.Time
				&& A.Category == B.Category && A.RevealText == B.RevealText && A.Rumor == B.Rumor
				&& A.ID == B.ID && A.History == B.History
				&& A.Text == B.Text && A.LearnedFrom == B.LearnedFrom && A.Weight == B.Weight
				&& A.Revealed == B.Revealed && A.Tradable == B.Tradable
				&& SameAttributes(A.Attributes, B.Attributes);
		}

		private static bool SameAttributes(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static string NativeArchiveAttribute(string Wire)
		{
			return NativeArchiveAttributePrefix + (Wire ?? "");
		}

		private static string Canonical(object Row)
		{
			List<string> values = new List<string>();
			FieldInfo[] fields = Row.GetType().GetFields(BindingFlags.Instance
				| BindingFlags.Public);
			Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
			for (int i = 0; i < fields.Length; i++)
			{
				object raw = fields[i].GetValue(Row);
				string value = raw == null ? "<null>" : Convert.ToString(raw,
					CultureInfo.InvariantCulture);
				values.Add(fields[i].Name + "=" + value.Length.ToString(
					CultureInfo.InvariantCulture) + ":" + value);
			}
			return string.Join("\u001e", values.ToArray());
		}

		private static bool SameRows(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}
	}
}
