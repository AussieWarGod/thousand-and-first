namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Realm due-window receipt; contains no zone, actor, or old-realm identity.</summary>
		public KingdomPolityDispatchState PolityDispatch = new KingdomPolityDispatchState();

		/// <summary>Instance-local diagnostics dedupe, reset on every load; never persisted and
		/// never read by authority: one plus the dispatch window whose fact drift was last
		/// reported by this loaded game.</summary>
		[System.NonSerialized]
		private ulong PolityDriftNotedWindow;

		internal bool TryNotePolityDrift(ulong Window)
		{
			if (PolityDriftNotedWindow == Window + 1UL) return false;
			PolityDriftNotedWindow = Window + 1UL; return true;
		}
	}
}
