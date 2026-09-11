namespace ThousandAndFirst
{
	/// <summary>
	/// One candidate component as a slot census reads it: the lot and slot it claims, the
	/// component token it carries, the engine identity it was assigned, and whether it stands on
	/// the mapped peer's own world cell.
	///
	/// <para>The census decides membership from these values alone, so the decision is executable
	/// outside a live game while the caller keeps the engine reads that produce them.</para>
	/// </summary>
	public sealed class ArchitectureComponentCensusRow
	{
		public ArchitectureComponentCensusRow(string Lot, string Slot, string Token, string Id,
			bool AtPeerCell)
		{
			this.Lot = Lot;
			this.Slot = Slot;
			this.Token = Token;
			this.Id = Id;
			this.AtPeerCell = AtPeerCell;
		}

		/// <summary>The lot the candidate claims.</summary>
		public string Lot { get; private set; }

		/// <summary>The layout-local slot name the candidate claims.</summary>
		public string Slot { get; private set; }

		/// <summary>The candidate's own component token, empty or null when it carries none.</summary>
		public string Token { get; private set; }

		/// <summary>The candidate's engine identity, null when it was never assigned one.</summary>
		public string Id { get; private set; }

		/// <summary>Whether the candidate stands on the mapped peer's own world cell.</summary>
		public bool AtPeerCell { get; private set; }
	}
}
