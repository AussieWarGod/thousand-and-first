using System;

namespace ThousandAndFirst
{
	/// <summary>Pure classifiers for outer realm callback receipts. Engine dispatchers supply
	/// bounded hashes; these rules admit only the frozen before or one declared after state.</summary>
	internal static class KingdomRealmCallbackProofRules
	{
		internal static bool ChronicleListsMatch(KingdomChronicleSinkDisposition OfficialState,
			string OfficialCurrent, string OfficialBefore, string OfficialAfter,
			KingdomChronicleSinkDisposition OutsiderState, string OutsiderCurrent,
			string OutsiderBefore, string OutsiderAfter, bool Terminal, out bool AnyLost)
		{
			AnyLost = false;
			if (!ValidHash(OfficialCurrent) || !ValidHash(OfficialBefore) ||
				!ValidHash(OfficialAfter) || !ValidHash(OutsiderCurrent) ||
				!ValidHash(OutsiderBefore) || !ValidHash(OutsiderAfter) ||
				!SinkMatches(OfficialState, OfficialCurrent, OfficialBefore, OfficialAfter,
					Terminal, out bool officialLost) ||
				!SinkMatches(OutsiderState, OutsiderCurrent, OutsiderBefore, OutsiderAfter,
					Terminal, out bool outsiderLost)) return false;
			AnyLost = officialLost || outsiderLost;
			return true;
		}

		/// <summary>The Chronicle fault register is diagnostic state, but it is still part of
		/// the archived realm graph. A declared callback may leave the frozen value alone or
		/// publish only the exact diagnostic implied by its last Lost sink. It may not smuggle
		/// an unrelated fault through an otherwise valid receipt transition.</summary>
		internal static bool ChronicleFaultMatches(bool Present, bool Terminal,
			KingdomChronicleSinkDisposition OfficialState,
			KingdomChronicleSinkDisposition OutsiderState,
			KingdomChronicleSinkDisposition JournalState,
			string Current, string Before)
		{
			if (Current == null || Current.Length > 160 || Before == null || Before.Length > 160)
				return false;
			if (string.Equals(Current, Before, StringComparison.Ordinal)) return true;
			if (!Present || !Terminal) return false;
			if (JournalState == KingdomChronicleSinkDisposition.Lost)
				return Current == "0:journal-attempt-uncertain" ||
					Current == "0:journal-callback-uncertain";
			if (JournalState != KingdomChronicleSinkDisposition.Delivered &&
				JournalState != KingdomChronicleSinkDisposition.Skipped) return false;
			if (OutsiderState == KingdomChronicleSinkDisposition.Lost)
				return ListFaultMatches("outsider", Current);
			if (OutsiderState != KingdomChronicleSinkDisposition.Delivered) return false;
			if (OfficialState == KingdomChronicleSinkDisposition.Lost)
				return ListFaultMatches("official", Current);
			return OfficialState == KingdomChronicleSinkDisposition.Delivered &&
				string.Equals(Current, Before, StringComparison.Ordinal);
		}

		private static bool ListFaultMatches(string Register, string Current)
		{
			return Current == "0:list-hash" ||
				Current == "0:" + Register + "-interleaved" ||
				Current == "0:" + Register + "-rehash" ||
				Current == "0:" + Register + "-interleaved-after-intent" ||
				Current == "0:" + Register + "-append" ||
				Current == "0:" + Register + "-after-mismatch";
		}

		private static bool SinkMatches(KingdomChronicleSinkDisposition State,
			string Current, string Before, string After, bool Terminal, out bool Lost)
		{
			Lost = false;
			switch (State)
			{
				case KingdomChronicleSinkDisposition.Delivered:
					return string.Equals(Current, After, StringComparison.Ordinal);
				case KingdomChronicleSinkDisposition.Lost:
					Lost = true;
					return string.Equals(Current, Before, StringComparison.Ordinal);
				case KingdomChronicleSinkDisposition.Pending:
				case KingdomChronicleSinkDisposition.Attempting:
					return !Terminal && (string.Equals(Current, Before,
						StringComparison.Ordinal) || string.Equals(Current, After,
						StringComparison.Ordinal));
				default:
					return false;
			}
		}

		private static bool ValidHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') ||
					(Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}
	}
}
