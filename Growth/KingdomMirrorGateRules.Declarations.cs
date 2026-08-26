using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to key an arch came to.
	/// <para>
	/// A refusal is reserved for something the founder actually asked for and cannot have, so that
	/// the telling means something when it fires &mdash; the same shape
	/// <c>KingdomJoinVerdict</c> keeps one lane over.
	/// </para>
	/// </summary>
	internal enum KingdomGateVerdict : byte
	{
		/// <summary>This end is keyed and stands waiting for a twin in another city.</summary>
		Offered = 0,

		/// <summary>The two ends answer each other. The crossing exists.</summary>
		Joined = 1,

		/// <summary>An arch was taken back out of the register.</summary>
		Released = 2,

		/// <summary>This city already keeps a keyed arch. One crossing to a city: a second arch in
		/// the same place would answer the same ground the first one does.</summary>
		RefusedCityKeyed = 3,

		/// <summary>This very arch is already in the register. Release it before keying it again;
		/// the caller offers that instead of refusing outright.</summary>
		RefusedAlreadyKeyed = 4,

		/// <summary>Nothing to release: this arch was never keyed.</summary>
		RefusedUnkeyed = 5,

		/// <summary>The register will hold no more. Bounded because it is a string the game state
		/// carries, and hostile-input discipline applies to our own writing too.</summary>
		RefusedFull = 6,

		/// <summary>The key or the city could not be written down &mdash; empty, or carrying one of
		/// the register's own separators. Refused rather than escaped: a name that cannot be stored
		/// whole is a name the register would give back wrong.</summary>
		RefusedNamed = 7
	}

	/// <summary>
	/// What became of the standing draw over one span of days.
	/// </summary>
	internal enum KingdomGateHold : byte
	{
		/// <summary>No day boundary was crossed, so nothing was owed and nothing is decided.</summary>
		Unchanged = 0,

		/// <summary>The works paid the whole of it. The arch stands open.</summary>
		Held = 1,

		/// <summary>The works could not pay it. The arch closes and says so.</summary>
		Lost = 2
	}

	/// <summary>
	/// One arch in the realm's register: which gate it is, which city keeps it, and which other
	/// arch it answers.
	/// <para>
	/// <b>The register is the pairing, and that is the whole of the QB-1 seam.</b> An arch cannot
	/// write on its twin, because its twin is usually standing in a zone nobody has loaded; so
	/// neither end stores the other. Both ends store nothing but their own key, and the register
	/// &mdash; one string the game carries, exactly as the keepers' knowledge roster is carried
	/// (<c>KingdomZoning.Roster</c>) &mdash; says who answers whom. Re-keying the realm onto a
	/// capital hub when the crown wave lands is then a rewrite of the <see cref="Partner"/> column
	/// and nothing else: no arch is visited, no arch is rebuilt, and nothing is lost.
	/// </para>
	/// <para>
	/// Frozen, and every transition below returns a new array rather than editing this one.
	/// </para>
	/// </summary>
	internal readonly struct KingdomGateRow
	{
		/// <summary>The game-state key under which this arch publishes its own cell address.
		/// Composed from the ground it stands on (<see cref="KingdomMirrorGateRules.ComposeLocationKey"/>),
		/// so it is the same key after a reload and the same key after a rebuild on the same
		/// cell.</summary>
		internal readonly string Key;

		/// <summary>The city this arch stands in, as the founder names it.</summary>
		internal readonly string City;

		/// <summary>The key of the arch this one answers, or the empty string while it waits.
		/// Never this row's own key.</summary>
		internal readonly string Partner;

		internal KingdomGateRow(string key, string city, string partner)
		{
			Key = key ?? "";
			City = city ?? "";
			Partner = partner ?? "";
		}

		/// <summary>This row with a different partner. Copy-on-write: the old row is untouched.</summary>
		internal KingdomGateRow WithPartner(string partner)
		{
			return new KingdomGateRow(Key, City, partner);
		}
	}

}
