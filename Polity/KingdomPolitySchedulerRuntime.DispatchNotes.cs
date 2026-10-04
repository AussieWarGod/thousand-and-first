using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolitySchedulerRuntime
	{
		/// <summary>Logs what the dispatch window did, without refusal wording: each committed
		/// withdrawal once, then continued drift at most once per window per loaded game
		/// (KingdomSystem instance); any load, cold or in the same process, may log it again. The
		/// lines come from the engine-free KingdomPolityDispatchRules.DispatchNotes.</summary>
		private static void NoteDispatch(KingdomSystem System, ulong Window, bool FactsDrifted,
			List<string> Withdrawn)
		{
			List<string> lines = KingdomPolityDispatchRules.DispatchNotes(Window, Withdrawn,
				FactsDrifted && System.TryNotePolityDrift(Window));
			for (int i = 0; i < lines.Count; i++) KingdomLog.Log(lines[i]);
		}
	}
}
