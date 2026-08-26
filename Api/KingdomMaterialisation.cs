using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One physical projection a work now owes. The sidecar retains this debt until an
	/// attended materialisation acknowledges it; it is never treated as already landed.</summary>
	public readonly struct KingdomMaterialisation
	{
		/// <summary>Exact Qud object blueprint.</summary>
		public readonly string Blueprint;
		/// <summary>Number of objects owed; positive and bounded.</summary>
		public readonly int Count;

		/// <summary>Builds one proposed materialisation debt.</summary>
		public KingdomMaterialisation(string Blueprint, int Count)
		{
			this.Blueprint = Blueprint;
			this.Count = Count;
		}
	}
}
