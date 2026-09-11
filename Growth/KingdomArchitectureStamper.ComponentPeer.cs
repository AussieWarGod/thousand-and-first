using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// The one component of the other generation that may stand at a slot during this upgrade,
		/// resolved from the frozen retained pairing and the receipt that owns it.
		///
		/// <para>Resolved per census call, never cached: the peer's retain state changes while the
		/// pass runs, so the same peer may be allowed at one slot's settle and already gone by the
		/// next. Returns null when the delta names no pair at this slot, when the receipt records
		/// no identity for it, or when its placement has no exact world cell -- and a null peer
		/// means the census permits no other generation at all.</para>
		/// </summary>
		/// <param name="AfterCensus">True when settling the successor generation, so the peer
		/// sought is the predecessor's; false when settling the predecessor.</param>
		private static ArchitectureComponentPeer ResolveComponentPeer(GameObject Owner, Zone Z,
			ArchitectureLayoutDelta Delta, KingdomArchitectureIntent Before,
			KingdomArchitectureIntent After, string Lot, string Slot, bool AfterCensus)
		{
			ArchitecturePlacement peer;
			ArchitecturePlacement retainKey;
			if (Owner == null || Z == null || string.IsNullOrEmpty(Lot)
				|| !KingdomArchitectureComponentCensusRules.TryPeerPlacement(Delta, Slot,
					AfterCensus, out peer, out retainKey)) return null;
			KingdomArchitectureIntent intent = AfterCensus ? Before : After;
			if (intent == null) return null;
			string idProperty = OutputId(retainKey);
			string id = Owner.GetStringProperty(idProperty);
			if (Owner.HasIntProperty(idProperty) || string.IsNullOrEmpty(id)) return null;
			string retainProperty = UpgradeRetain(retainKey);
			if (!Owner.HasIntProperty(retainProperty) || Owner.HasStringProperty(retainProperty))
				return null;
			string peerId;
			string peerToken;
			bool allowed;
			if (!KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot, intent.SnapshotHash,
				peer, id, Owner.GetIntProperty(retainProperty), AfterCensus, out peerId,
				out peerToken, out allowed)) return null;
			ArchitectureLayoutSnapshot snapshot;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out _)
				|| !KingdomArchitectureRuntime.TryWorldPlacement(snapshot, intent.Rect, peer,
					out x, out y, out _)) return null;
			Cell cell = Z.GetCell(x, y);
			if (cell == null) return null;
			return new ArchitectureComponentPeer(peerId, peerToken, cell, allowed);
		}
	}
}
