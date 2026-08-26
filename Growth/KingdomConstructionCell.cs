using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>One stable cell in a route payload.</summary>
	public struct KingdomConstructionCell
	{
		public readonly int X;
		public readonly int Y;

		public KingdomConstructionCell(int X, int Y)
		{
			this.X = X;
			this.Y = Y;
		}
	}

}
