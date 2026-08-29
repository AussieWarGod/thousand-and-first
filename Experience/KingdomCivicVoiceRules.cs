using System;

namespace ThousandAndFirst
{
	/// <summary>Pure, bounded authority for three facts-first two-resident renderings.</summary>
	public static class KingdomCivicVoiceRules
	{
		public const int MaxReceipts = 3;
		public const int MaxCandidates = 128;
		public const int MaxFactsBytes = 384;

		public static bool TryPrepare(KingdomExperienceLedger Ledger,
			KingdomCivicDecisionPreview Preview, KingdomCivicVoiceCandidate[] Candidates,
			out KingdomCivicVoiceReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!KingdomExperienceRules.TryValidate(Ledger, out Failure)
				|| !ValidPreview(Preview)
				|| !KingdomExperienceRules.CanEmit(Ledger,
					KingdomExperienceOptionKind.CivicStory, Preview.CauseTick)
				|| Preview.EnableEpoch != Ledger.Story.EnableEpoch)
				return Fail(Failure ?? "civic voice preview is not enabled", out Failure);
			if (Index(Ledger, Preview.Fixture) >= 0)
				return Fail("that civic voice fixture is already recorded", out Failure);
			if (!TryPair(Preview.Fixture, Candidates, out KingdomCivicVoiceCandidate first,
				out KingdomCivicVoiceCandidate second))
				return Fail("two exact standing witnesses are unavailable", out Failure);
			Receipt = new KingdomCivicVoiceReceipt
			{
				Fixture = Preview.Fixture, SourceVersion = Preview.SourceVersion,
				SourceId = Preview.SourceId, SettlementId = Preview.SettlementId,
				Facts = Preview.Facts, CauseTick = Preview.CauseTick,
				EnableEpoch = Preview.EnableEpoch, FirstResidentId = first.ResidentId,
				FirstName = first.Name, SecondResidentId = second.ResidentId,
				SecondName = second.Name
			};
			return Valid(Receipt) || Fail("prepared civic voice receipt is invalid", out Failure);
		}

		public static bool TryPublish(KingdomExperienceLedger Ledger, long ExpectedRevision,
			KingdomCivicVoiceReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!KingdomExperienceRules.TryValidate(Ledger, out Failure) || !Valid(Receipt))
				return Fail(Failure ?? "civic voice receipt is invalid", out Failure);
			int at = Index(Ledger, Receipt.Fixture);
			if (at >= 0)
				return Exact(Ledger.Voices[at], Receipt)
					|| Fail("civic voice duplicate mismatches its source", out Failure);
			if (ExpectedRevision != Ledger.Revision)
				return Fail("civic voice revision conflict", out Failure);
			if (!KingdomExperienceRules.CanEmit(Ledger,
				KingdomExperienceOptionKind.CivicStory, Receipt.CauseTick)
				|| Receipt.EnableEpoch != Ledger.Story.EnableEpoch)
				return Fail("civic voice source is outside the current story epoch", out Failure);
			if (Ledger.Voices.Count >= MaxReceipts || Ledger.Revision == long.MaxValue)
				return Fail("civic voice capacity or revision is exhausted", out Failure);
			KingdomExperienceLedger next = KingdomExperienceRules.Clone(Ledger);
			next.Voices.Add(Receipt.Copy());
			next.Voices.Sort((A, B) => ((byte)A.Fixture).CompareTo((byte)B.Fixture));
			next.Revision++;
			if (!KingdomExperienceRules.TryValidate(next, out Failure)) return false;
			Ledger.CopyFrom(next); return true;
		}

		public static string Render(KingdomCivicVoiceReceipt Receipt,
			bool FirstAvailable, bool SecondAvailable)
		{
			if (!Valid(Receipt)) return "";
			if (!FirstAvailable || !SecondAvailable) return Receipt.Facts;
			return Receipt.Facts + "\n\n{{W|" + Receipt.FirstName + "}}: \""
				+ Line(Receipt.Fixture, 0) + "\"\n{{W|" + Receipt.SecondName + "}}: \""
				+ Line(Receipt.Fixture, 1) + "\"";
		}

		public static bool TryConsumeCallback(KingdomExperienceLedger Ledger,
			long ExpectedRevision, string SourceId, int ResidentId, bool ResidentAvailable,
			long Tick, out string Text, out string Failure)
		{
			Text = null; Failure = null;
			if (!ResidentAvailable || Tick < 0L
				|| !KingdomExperienceRules.TryValidate(Ledger, out Failure)
				|| !KingdomExperienceRules.CanEmit(Ledger,
					KingdomExperienceOptionKind.CivicStory, Tick))
				return Fail(Failure ?? "recorded witness is unavailable", out Failure);
			int at = SourceIndex(Ledger, SourceId);
			if (at < 0) return Fail("civic voice source is absent", out Failure);
			KingdomCivicVoiceReceipt row = Ledger.Voices[at];
			if (row.CallbackConsumed || Tick < row.CauseTick
				|| (ResidentId != row.FirstResidentId && ResidentId != row.SecondResidentId))
				return Fail("civic voice callback is exhausted or mismatched", out Failure);
			if (ExpectedRevision != Ledger.Revision || Ledger.Revision == long.MaxValue)
				return Fail("civic voice callback revision is unavailable", out Failure);
			KingdomExperienceLedger next = KingdomExperienceRules.Clone(Ledger);
			next.Voices[at].CallbackConsumed = true; next.Voices[at].CallbackTick = Tick;
			next.Revision++;
			if (!KingdomExperienceRules.TryValidate(next, out Failure)) return false;
			string name = ResidentId == row.FirstResidentId ? row.FirstName : row.SecondName;
			Text = "{{W|" + name + "}} remembers the ruling:\n\n" + row.Facts;
			Ledger.CopyFrom(next); return true;
		}

		internal static bool Valid(KingdomCivicVoiceReceipt R)
		{
			return R != null && R.Version == KingdomCivicVoiceReceipt.CurrentVersion
				&& R.Fixture >= KingdomCivicVoiceFixture.CreedDeclaration
				&& R.Fixture <= KingdomCivicVoiceFixture.AssentingMoot && R.SourceVersion >= 1
				&& KingdomExperienceRules.TypedId(R.SourceId, "taf:")
				&& KingdomExperienceRules.TypedId(R.SettlementId, "taf:settlement:")
				&& KingdomExperienceRules.VoiceText(R.Facts) && R.CauseTick >= 0L
				&& R.EnableEpoch >= 1L && R.FirstResidentId > 0 && R.SecondResidentId > 0
				&& R.FirstResidentId != R.SecondResidentId
				&& KingdomExperienceRules.CivicText(R.FirstName, true)
				&& KingdomExperienceRules.CivicText(R.SecondName, true)
				&& (R.CallbackConsumed ? R.CallbackTick >= R.CauseTick : R.CallbackTick == 0L);
		}

		internal static int Index(KingdomExperienceLedger L, KingdomCivicVoiceFixture Fixture)
		{
			if (L?.Voices == null) return -1;
			for (int i = 0; i < L.Voices.Count; i++) if (L.Voices[i].Fixture == Fixture) return i;
			return -1;
		}

		private static int SourceIndex(KingdomExperienceLedger L, string SourceId)
		{
			if (L?.Voices == null || SourceId == null) return -1;
			for (int i = 0; i < L.Voices.Count; i++) if (L.Voices[i].SourceId == SourceId) return i;
			return -1;
		}

		private static bool ValidPreview(KingdomCivicDecisionPreview P)
		{
			return P != null && P.Fixture >= KingdomCivicVoiceFixture.CreedDeclaration
				&& P.Fixture <= KingdomCivicVoiceFixture.AssentingMoot && P.SourceVersion >= 1
				&& KingdomExperienceRules.TypedId(P.SourceId, "taf:")
				&& KingdomExperienceRules.TypedId(P.SettlementId, "taf:settlement:")
				&& KingdomExperienceRules.VoiceText(P.Facts) && P.CauseTick >= 0L
				&& P.EnableEpoch >= 1L;
		}

		private static bool TryPair(KingdomCivicVoiceFixture Fixture,
			KingdomCivicVoiceCandidate[] Candidates, out KingdomCivicVoiceCandidate First,
			out KingdomCivicVoiceCandidate Second)
		{
			First = default(KingdomCivicVoiceCandidate);
			Second = default(KingdomCivicVoiceCandidate);
			if (Candidates == null || Candidates.Length > MaxCandidates) return false;
			KingdomCivicVoiceCandidate[] rows = (KingdomCivicVoiceCandidate[])Candidates.Clone();
			Array.Sort(rows, (A, B) => A.ResidentId.CompareTo(B.ResidentId));
			for (int i = 0; i < rows.Length; i++)
				if (rows[i].ResidentId <= 0 || !KingdomExperienceRules.CivicText(rows[i].Name, true)
					|| i > 0 && rows[i - 1].ResidentId == rows[i].ResidentId) return false;
			int at = (((int)Fixture) - 1) * 2;
			if (at < 0 || at + 1 >= rows.Length) return false;
			First = rows[at]; Second = rows[at + 1]; return true;
		}

		private static string Line(KingdomCivicVoiceFixture Fixture, int Speaker)
		{
			if (Fixture == KingdomCivicVoiceFixture.CreedDeclaration)
				return Speaker == 0 ? "A declaration tells newcomers which road they join."
					: "It also tells every passed-over creed what this realm chose.";
			if (Fixture == KingdomCivicVoiceFixture.VillageCovenant)
				return Speaker == 0 ? "Asking leaves their ground theirs."
					: "Water binds the word; it does not found a city.";
			return Speaker == 0 ? "Assent is named, not assumed."
				: "An exemption is named too, and the ward accounts for it.";
		}

		private static bool Exact(KingdomCivicVoiceReceipt A, KingdomCivicVoiceReceipt B)
		{
			return A.Version == B.Version && A.Fixture == B.Fixture
				&& A.SourceVersion == B.SourceVersion && A.SourceId == B.SourceId
				&& A.SettlementId == B.SettlementId && A.Facts == B.Facts
				&& A.CauseTick == B.CauseTick && A.EnableEpoch == B.EnableEpoch
				&& A.FirstResidentId == B.FirstResidentId && A.FirstName == B.FirstName
				&& A.SecondResidentId == B.SecondResidentId && A.SecondName == B.SecondName
				&& A.CallbackConsumed == B.CallbackConsumed && A.CallbackTick == B.CallbackTick;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
