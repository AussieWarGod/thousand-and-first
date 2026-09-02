using System;
using XRL.World;

namespace ThousandAndFirst.Api
{
	/// <summary>Marks a read-only exact-cell civic designation source.</summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class KingdomDesignationProviderAttribute : Attribute
	{
	}

	/// <summary>Extension seam for another mod's persistent building/designation system. Only
	/// <c>ThousandAndFirst.Api</c> types cross it: the host translates every
	/// <see cref="KingdomApiDesignation"/>, re-derives caps and accepted tags from its BuildingKey,
	/// and validates every exact cell before the row can supply or quarantine ground.</summary>
	public interface IKingdomDesignationProvider
	{
		string ProviderId { get; }
		string ProviderVersion { get; }

		/// <summary>Returns true with a nonnull (possibly empty) row array for a complete
		/// observation. False with both outputs null is the sole known-absence result. Every
		/// other return/output shape is a provider-wide fault and none of its rows are consumed.</summary>
		bool TryObserve(Zone ActiveZone, out KingdomApiDesignation[] Designations,
			out string Failure);
	}
}
