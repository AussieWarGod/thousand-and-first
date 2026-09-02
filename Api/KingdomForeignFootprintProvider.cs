using System;
using XRL.World;

namespace ThousandAndFirst.Api
{
	/// <summary>Marks a read-only foreign spatial source which may supplement an explicit TAF
	/// adoption. A footprint is evidence only; it grants no role, cap, or benefit by itself.</summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class KingdomForeignFootprintProviderAttribute : Attribute
	{
	}

	public sealed class KingdomForeignFootprint
	{
		public string ProviderId;
		public string ProviderVersion;
		public string Identity;
		public string Revision;
		/// <summary>Empty for accepted evidence. A bounded nonempty value quarantines these
		/// exact cells so an explicit adoption cannot silently bypass an ambiguous foreign row.</summary>
		public string Refusal;
		public string ZoneId;
		public string SectorId;
		public int DeclaredCount;
		public int OriginX;
		public int OriginY;
		/// <summary>Complete unique in-zone cell set as Api cells; the host translates and
		/// bounds-checks them, refusing the row on any out-of-zone or duplicate cell.</summary>
		public KingdomApiCell[] Cells;
	}

	public interface IKingdomForeignFootprintProvider
	{
		string ProviderId { get; }
		string ProviderVersion { get; }

		/// <summary>Returns true with a nonnull (possibly empty) row array for a complete
		/// observation. False with both outputs null is the sole known-absence result. Every
		/// other return/output shape is a provider-wide fault and none of its rows are consumed.</summary>
		bool TryObserve(Zone ActiveZone, out KingdomForeignFootprint[] Footprints,
			out string Failure);
	}
}
