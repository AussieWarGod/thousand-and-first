namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Realm due-window receipt; contains no zone, actor, or old-realm identity.</summary>
		public KingdomPolityDispatchState PolityDispatch = new KingdomPolityDispatchState();

		/// <summary>Process-local diagnostics dedupe: one plus the dispatch window whose fact drift
		/// was last reported. Never persisted and never read by authority.</summary>
		[System.NonSerialized]
		private ulong PolityDriftNotedWindow;

		internal bool TryNotePolityDrift(ulong Window)
		{
			if (PolityDriftNotedWindow == Window + 1UL) return false;
			PolityDriftNotedWindow = Window + 1UL; return true;
		}
	}
}
