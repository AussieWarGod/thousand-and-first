using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>Normalized carrier kind used only during one host pass.</summary>
	internal readonly struct KingdomCarrierKindRow
	{
		internal readonly string Key;
		internal readonly string Blueprint;
		internal readonly int WalkTicksPerCell;
		internal readonly int Capacity;

		internal KingdomCarrierKindRow(string key, string blueprint, int walkTicksPerCell, int capacity)
		{
			Key = key;
			Blueprint = blueprint;
			WalkTicksPerCell = walkTicksPerCell;
			Capacity = capacity;
		}
	}
}
