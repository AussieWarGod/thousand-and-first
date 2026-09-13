using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The one component of the other generation that may legitimately stand at a slot during an
	/// authored upgrade, resolved at the call site that owns the receipt.
	///
	/// <para>It is resolved per census call rather than once per pass, because the retain state it
	/// depends on changes while the pass runs: a peer that was pending when one slot settled may
	/// be settled by the time the next one does. It carries only what proves identity -- the
	/// recorded output id, the peer's own world cell, its component token, and whether its retain
	/// state still lets it stand -- so the census itself reads no receipts, no delta and no
	/// successor target.</para>
	///
	/// <para>A null peer means NO other generation may stand at that slot, which is the truth at
	/// every call site with no upgrade receipt in scope.</para>
	/// </summary>
	public sealed class ArchitectureComponentPeer
	{
		/// <summary>The engine identity the receipt recorded for that retained pair.</summary>
		public readonly string Id;

		/// <summary>The peer's component token, built from its own generation and placement.</summary>
		public readonly string Token;

		/// <summary>The peer's own world cell, compared by reference.</summary>
		public readonly Cell Cell;

		/// <summary>Whether the peer's retain state still permits it to stand here.</summary>
		public readonly bool Allowed;

		public ArchitectureComponentPeer(string Id, string Token, Cell Cell, bool Allowed)
		{
			this.Id = Id;
			this.Token = Token;
			this.Cell = Cell;
			this.Allowed = Allowed;
		}
	}
}
