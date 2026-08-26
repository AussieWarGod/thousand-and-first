using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm's one water manifest in flight between its two cities: drams already drawn
	/// from the origin's stores, waiting to be poured into the destination's the next time the
	/// founder stands on that city's ground and it activates.
	/// <para>
	/// Held by <see cref="KingdomSystem"/> directly, not by <see cref="KingdomSettlement"/> — it
	/// is realm-level state, like <c>Standings</c> and the chronicle registers, so it survives
	/// every seat swap untouched rather than travelling with whichever city happens to be seated.
	/// A manifest names its origin and destination by settlement name for exactly that reason:
	/// "seat" and "Away" are roles that swap, but the manifest is addressed to a place, not a role.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomManifest
#if !TAF_TESTS
		: IComposite
#endif
	{
		/// <summary>The city the drams were physically drawn from.</summary>
		public string OriginName;

		/// <summary>The city the drams are addressed to. Delivery fires when this equals the
		/// currently seated city's own name.</summary>
		public string DestinationName;

		/// <summary>Drams actually drawn from the origin's stores at load time. Escrow, not a
		/// promise: this water already left the origin and belongs to no vessel until delivered.</summary>
		public int Drams;

		/// <summary>Tick the manifest was loaded.</summary>
		public long LoadedTick;

		/// <summary>Absolute tick past which the manifest is void if undelivered.</summary>
		public long DeadlineTick;

		/// <summary>
		/// True once the window closed and the load turned for home. A manifest turns back at
		/// most once: the water is never destroyed for the founder having been elsewhere, but a
		/// load cannot bounce between two cities forever either.
		/// </summary>
		public bool TurnedBack;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomManifest));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomManifest));
		}
#endif
	}
}
