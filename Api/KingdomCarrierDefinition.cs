using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One extension-owned carrier kind. Jobs retain the exact blueprint and pace they
	/// were opened with, so a later definition change cannot rewrite a journey in flight.</summary>
	public readonly struct KingdomCarrierDefinition
	{
		/// <summary>Owner-local stable key.</summary>
		public readonly string Key;
		/// <summary>Exact Qud blueprint used for the embodied transient.</summary>
		public readonly string Blueprint;
		/// <summary>World ticks spent per Chebyshev cell; must be positive.</summary>
		public readonly int WalkTicksPerCell;
		/// <summary>Maximum units one job may put on this carrier.</summary>
		public readonly int Capacity;

		/// <summary>Builds one proposed carrier kind.</summary>
		public KingdomCarrierDefinition(string Key, string Blueprint, int WalkTicksPerCell, int Capacity)
		{
			this.Key = Key;
			this.Blueprint = Blueprint;
			this.WalkTicksPerCell = WalkTicksPerCell;
			this.Capacity = Capacity;
		}
	}
}
