using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolitySchedulerRuntime
	{
		/// <summary>Reports what the dispatch window did, without refusal wording. Withdrawals are
		/// committed and logged once each; continued drift is logged once per window per process.</summary>
		private static void NoteDispatch(KingdomSystem System, ulong Window, bool FactsDrifted,
			List<string> Withdrawn)
		{
			for (int i = 0; i < Withdrawn.Count; i++) KingdomLog.Log("polity: " + Withdrawn[i]);
			if (!FactsDrifted || !System.TryNotePolityDrift(Window)) return;
			KingdomLog.Log("polity: dispatch window " + Window.ToString(CultureInfo.InvariantCulture)
				+ " continues with endpoint facts changed since it opened; no new dispatch until window "
				+ (Window + 1UL).ToString(CultureInfo.InvariantCulture));
		}
	}
}
