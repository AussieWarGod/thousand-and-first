namespace ThousandAndFirst.Api
{
	/// <summary>
	/// One exact-cell civic designation exactly as an extension reports it. Only Api types cross
	/// this seam. The host re-derives caps, accepted tags, and per-cell use from
	/// <see cref="BuildingKey"/>, bounds-checks every cell against the active zone, and refuses a
	/// faulty row outright rather than repairing it. External cells are open-yard evidence: no
	/// covered, interior, or ingress claim is ever granted from this row.
	/// </summary>
	public sealed class KingdomApiDesignation
	{
		public string ProviderId;
		public string ProviderVersion;
		/// <summary>Stable within the provider; the host prefixes it with the provider id.</summary>
		public string Identity;
		/// <summary>Must change whenever membership or the trusted foreign evidence changes.</summary>
		public string Revision;
		public string ZoneId;
		/// <summary>Global id of the exact physical root object standing on this ground.</summary>
		public string RootId;
		/// <summary>A catalogue building key; the host refuses keys it does not publish.</summary>
		public string BuildingKey;
		public string LotId;
		/// <summary>Complete unique in-zone cell set, at least one cell.</summary>
		public KingdomApiCell[] Cells;
	}
}
