using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure, deterministic receipt for an exact kept-parts debit.</summary>
	internal sealed class KingdomKeptSpendPlan
	{
		public readonly int Owed;
		public readonly List<KingdomKeptSpendStep> Steps;

		public int Finalizers
		{
			get
			{
				int total = 0;
				for (int i = 0; i < Steps.Count; i++)
				{
					if (Steps[i].NeedsFinalization)
					{
						total++;
					}
				}
				return total;
			}
		}

		public KingdomKeptSpendPlan(int Owed, List<KingdomKeptSpendStep> Steps)
		{
			this.Owed = Owed;
			this.Steps = Steps;
		}
	}

}
