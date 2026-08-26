using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One undirected capacity edge between node indexes.</summary>
	public readonly struct KingdomExtensionNetworkEdge
	{
		/// <summary>First node index.</summary>
		public readonly int A;
		/// <summary>Second node index.</summary>
		public readonly int B;
		/// <summary>Units per world day this edge can carry.</summary>
		public readonly int CapacityPerDay;

		/// <summary>Builds one proposed edge.</summary>
		public KingdomExtensionNetworkEdge(int A, int B, int CapacityPerDay)
		{
			this.A = A;
			this.B = B;
			this.CapacityPerDay = CapacityPerDay;
		}
	}
}
