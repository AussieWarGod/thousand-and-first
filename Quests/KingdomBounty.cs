using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The posted price: a notice staked at the heart offering drams to whoever performs a named
	/// task, and the settlers and notables who read it and decide for themselves.
	/// <para>
	/// Three rules run through everything here.
	/// <b>Nothing is escrowed</b> &mdash; the price is a promise until the work is done, and the
	/// only water that ever leaves the stores is water paid to somebody who finished something.
	/// <b>Nothing nags</b> &mdash; an unclaimed notice just stands there, with no expiry, no
	/// reminder, and no penalty; the founder takes it down for free whenever they like, and the
	/// chronicle remembers that they did. <b>Nothing stalls in silence</b> (STANDARDS 7b) &mdash;
	/// a notice that cannot be moved says why once, and a notice that can never be attempted at
	/// all says so once and then keeps quiet forever.
	/// </para>
	/// <para>
	/// Every founder-facing entry point does its own eligibility check, its own messaging, and its
	/// own chronicle entry, and surfaces only a decline &mdash; the <c>KingdomLarder</c> idiom the
	/// rest of the mod follows. A refusal changes nothing.
	/// </para>
	/// </summary>
	public static partial class KingdomBounty
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionBounty") != "No";

		/// <summary>The one blueprint a staked notice can be, named here rather than inferred.</summary>
		public const string NoticeBlueprint = "r_KingdomNotice";

		/// <summary>
		/// String property written on a container the founder marks for a fetch notice, carrying
		/// the notice's own object id. The mark <b>is</b> the designation the protection law
		/// requires: nothing is ever carried out of a container that does not name a live notice
		/// of this settlement's.
		/// </summary>
		public const string FetchMarkProperty = "KingdomFetchNotice";

		/// <summary>
		/// Takeover point for the carry-sign, which generalises the fetch task past this
		/// settlement's own ground: distance-scaled days, chronicled porters, and a load that can
		/// be lost to the road.
		/// <para>
		/// Left null, the fetch task resolves the short way below &mdash; a marked pile standing in
		/// the same ground as the notice, carried into the dedicated stockpiles. Set, it is
		/// consulted first and its answer is final: return true once the haul has been taken over
		/// (this file then only pays the price when the hook reports the load home), false to let
		/// the short way run.
		/// </para>
		/// <para>
		/// Arguments, in order: the realm, the marked pile, the porter's name, and the tick the
		/// haul was taken. Not supported API &mdash; a coordination seam between two systems of
		/// this mod, and it moves when they do.
		/// </para>
		/// </summary>
		public static Func<KingdomSystem, GameObject, string, long, bool> HaulHook;

		private static readonly int[] PriceLadder = new int[8] { 1, 2, 3, 5, 8, 12, 20, 40 };

		// ==================================================================================
		// Posting
		// ==================================================================================

	}
}
