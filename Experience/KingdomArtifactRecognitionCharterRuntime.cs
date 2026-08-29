using System;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The Charter door onto D6, and the only production caller of the explicit selector, the
	/// recognition rules, and the civic-artifacts section of civic memory.
	/// <para>
	/// Everything reachable from here is non-custodial. The founder points at one object that is
	/// already lying beside them, the city writes a sentence about it, and the object itself is
	/// never moved, held, locked, escrowed, retagged, priced, counted, or consumed. Nothing here
	/// opens an inventory: <see cref="KingdomArtifactRecognitionSelectionRuntime.TryCollectNearby"/>
	/// reads the founder's own cell and its immediate neighbours and nothing else.
	/// </para>
	/// <para>
	/// Reading the register, browsing the choices, and walking away at the disclosure all spend
	/// nothing. The one governance commit in this family sits behind a durable civic-memory commit,
	/// so a cancelled recognition costs no action, no energy, and no byte of the save.
	/// </para>
	/// <para>
	/// The city named here is the one that owns the ground under the founder's feet, resolved
	/// through the realm's settlement topology. It is deliberately not the seat: the seat is a
	/// cursor that moves with the player, and a realm's second city must be told apart from its
	/// first by its own name rather than by whichever one happens to be seated.
	/// </para>
	/// <para>
	/// Qud 2.0.211.51 API evidence: <c>Popup.cs</c> 1650 (PickOption signature) and 2251-2324
	/// (ShowYesNo); <c>GameObject.cs</c> 424-434 (IDIfAssigned, the only pure identity read) and
	/// 515-532 (holder/cell/zone truth); <c>Cell.cs</c> 4854-4857 (that cell's root objects) and
	/// 7443-7462 (local adjacency).
	/// </para>
	/// </summary>
	public static partial class KingdomArtifactRecognitionCharterRuntime
	{
		/// <summary>Exactly the loaded ground, realm, settlement, and authority D6 may act on.</summary>
		internal sealed class Ground
		{
			internal KingdomSystem System;
			internal GameObject Founder;
			internal Zone Zone;
			internal string RealmId;
			internal string SettlementId;

			/// <summary>The owning settlement's own name, never the seat's and never the realm's.</summary>
			internal string SettlementName;
			internal IKingdomCivicMemoryAuthority Memory;
			internal long Tick;
		}

		/// <summary>
		/// Opens the realm's recognition register, then offers the one action that can add to it.
		/// <para>
		/// The register comes first on purpose. Everything D6 will ever say about an object is in
		/// there, it is read back from the save rather than remembered, and a founder who only
		/// wanted to check what the city already keeps never has to enter the flow that changes
		/// anything.
		/// </para>
		/// <para>
		/// <b>One lease means one lease, across the whole conversation.</b> Section one is opened
		/// exactly once, here, and that same lease object is carried through the register, the
		/// choices, the disclosure, and the commit. The founder is shown the exact words a
		/// particular payload would produce, and those are the words offered back against that
		/// payload; if anything else has written to the section in between, the commit is refused
		/// as stale and nothing changes. Re-reading the section to write would have made the
		/// disclosure a statement about a save that had already moved on.
		/// </para>
		/// </summary>
		public static void Open(KingdomSystem System, GameObject Founder)
		{
			if (!TryGround(System, Founder, out Ground ground, out string failure))
			{
				Popup.Show("Recognition is written on the exact loaded ground of one of your own "
					+ "cities.\n\n" + KingdomPresentation.Rich(failure));
				return;
			}
			if (!KingdomArtifactRecognitionLease.TryReadAuthority(ground.Memory, ground.RealmId,
				out KingdomCivicMemorySectionLease lease,
				out KingdomCivicArtifactsEnvelope held, out failure))
			{
				Popup.Show("{{W|What this city remembers of your travels}}\n\n"
					+ "The register is unavailable, and nothing was changed.\n\n"
					+ KingdomPresentation.Rich(failure));
				return;
			}
			int pick = Popup.PickOption(
				Title: "What " + KingdomPresentation.Rich(ground.SettlementName) + " remembers",
				Intro: KingdomPresentation.Rich(
					KingdomArtifactRecognitionRegister.Register(held.Recognitions)),
				Options: new string[2]
				{
					"Recognize one thing you are standing beside",
					"{{K|Close the register}}"
				},
				Hotkeys: new char[2] { 'r', 'x' }, AllowEscape: true);
			if (pick != 0) return;
			Recognize(ground, lease, held);
		}

		/// <summary>
		/// Whether this is exactly one loaded, uniquely owned settlement's ground, with a realm
		/// identity and a civic-memory authority to answer for it.
		/// <para>
		/// <see cref="KingdomCurrentCityEvidenceRuntime"/> is the shared owner of that question, and
		/// asking it here rather than re-deriving it means D6 cannot drift from D1 and D12 about
		/// what "this city, right now" means.
		/// </para>
		/// </summary>
		private static bool TryGround(KingdomSystem System, GameObject Founder,
			out Ground Result, out string Failure)
		{
			Result = null;
			Failure = null;
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.Founded || !GameObject.Validate(Founder)
				|| !Founder.IsPlayer() || !ReferenceEquals(Founder, The.Player) || zone == null)
			{
				Failure = "The Charter bearer is not standing on loaded ground.";
				return false;
			}
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(System, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context context, out Failure)) return false;
			if (!System.TryGetCurrentIdentity(out string realmId, out string settlementId)
				|| !string.Equals(settlementId, context.SettlementId, StringComparison.Ordinal)
				|| !System.OwnedZone(zone.ZoneID))
			{
				Failure = "The current realm and loaded-city identity do not agree on this ground.";
				return false;
			}
			if (!TrySettlementName(System, settlementId, out string settlementName, out Failure))
				return false;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				Failure = "Civic memory is unavailable in this save, so nothing could be recorded.";
				return false;
			}
			long tick = The.Game?.TimeTicks ?? -1L;
			if (tick < 0L)
			{
				Failure = "The current time could not be read exactly.";
				return false;
			}
			Result = new Ground
			{
				System = System,
				Founder = Founder,
				Zone = zone,
				RealmId = realmId,
				SettlementId = settlementId,
				SettlementName = settlementName,
				Memory = memory,
				Tick = tick
			};
			return true;
		}

		/// <summary>
		/// The owning settlement's own name, resolved through the realm's topology rather than read
		/// off the seat.
		/// <para>
		/// <c>SeatName</c> is the wrong authority twice over. It follows the seat, which is a cursor
		/// that moves as the founder travels, so on a second city's ground it would name the first;
		/// and when a settlement has no name of its own it falls back to the realm's display name,
		/// which would have a city's recognitions attributed to the whole realm. So the settlement
		/// that owns this exact ground is looked up by its own id, and a name that cannot be
		/// resolved is a refusal &mdash; a recognition the realm cannot place is not disclosed, and
		/// nothing is ever inferred from what the zone happens to be called.
		/// </para>
		/// </summary>
		private static bool TrySettlementName(KingdomSystem System, string SettlementId,
			out string Name, out string Failure)
		{
			Name = null;
			Failure = null;
			if (!System.TryFindSettlement(SettlementId, out bool seated,
				out KingdomSettlement settlement))
			{
				Failure = "This ground's city could not be resolved in the realm's topology.";
				return false;
			}
			string resolved = seated ? System.SettlementName : settlement.SettlementName;
			if (string.IsNullOrWhiteSpace(resolved))
			{
				Failure = "This ground's city has no name of its own to record a recognition under.";
				return false;
			}
			Name = resolved;
			return true;
		}
	}
}
