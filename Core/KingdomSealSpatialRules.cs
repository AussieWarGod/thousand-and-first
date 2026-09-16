namespace ThousandAndFirst
{
	/// <summary>
	/// One rule, read by both the daily settlement pass and the cold-load reconciliation, for what
	/// a refused spatial capture means.
	/// </summary>
	internal static class KingdomSealSpatialRules
	{
		/// <summary>
		/// Whether a refused capture is a fault the operator must be told about.
		///
		/// <para>Pending is the settlement saying "not yet": the public entrance has no witnessed
		/// street to the zone edge, which the roads errand lane produces over days. The daily pass
		/// has always carried that quietly. The load path did not, and the same young settlement
		/// that ran all session raised two MODERRORs the moment it was saved and reloaded (issue
		/// #181). Nothing about the save is different -- only which code asked.</para>
		///
		/// <para>Everything else still fails closed: Malformed is a broken reading, and Unavailable
		/// means the ground could not be read at all. Neither is a settlement waiting on a road.</para>
		/// </summary>
		/// <param name="Captured">Whether the capture succeeded.</param>
		/// <param name="Spatial">What the spatial capture reported.</param>
		internal static bool SpatialCaptureIsFault(bool Captured,
			KingdomInheritanceSpatialCaptureResult Spatial)
		{
			return !Captured && Spatial != KingdomInheritanceSpatialCaptureResult.Pending;
		}
	}
}
