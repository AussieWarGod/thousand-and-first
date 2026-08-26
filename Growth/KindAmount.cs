using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One <c>kind:amount</c> pair out of a <c>Carries</c> list.</summary>
	public readonly struct KindAmount
	{
		public readonly string Kind;

		public readonly int Amount;

		public KindAmount(string Kind, int Amount)
		{
			this.Kind = Kind;
			this.Amount = Amount;
		}
	}
}
