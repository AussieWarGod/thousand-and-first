
namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>
		/// Repairs a book read from a save written by an older build, or handed in by a caller.
		/// <para>
		/// Two failures matter here and nothing else does. A null column is an absent named field
		/// and becomes an empty one. <b>Ragged columns are truncated to the shortest</b>, because a
		/// row half of whose fields are missing is not a row — a reader that trusted the longest
		/// column would invent a zone out of a default id, and nothing is invented for ground the
		/// game has never looked at. Everything past a cap is dropped for the same reason
		/// &sect;1.4 states the caps at all: no dimension of this model grows.
		/// </para>
		/// </summary>
		public void Normalize()
		{
			NormalizeSidecarFields();
			NormalizeZoneColumns();
			NormalizeWorkColumns();
			NormalizeResidentColumns();
			NormalizeClockColumns();
			NormalizeToldColumns();
			NormalizeCityMetadata();
		}
	}
}
