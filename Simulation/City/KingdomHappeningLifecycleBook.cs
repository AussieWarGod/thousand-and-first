using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed class KingdomHappeningLifecycleBook
	{
		internal readonly int Sequence;
		internal readonly KingdomHappeningOperation Active;
		internal readonly KingdomHappeningSemanticReceipt[] SemanticReceipts;

		internal KingdomHappeningLifecycleBook(int sequence, KingdomHappeningOperation active)
			: this(sequence, active, null)
		{
		}

		internal KingdomHappeningLifecycleBook(int sequence, KingdomHappeningOperation active,
			KingdomHappeningSemanticReceipt[] semanticReceipts)
		{
			Sequence = sequence;
			Active = active;
			SemanticReceipts = semanticReceipts == null
				? new KingdomHappeningSemanticReceipt[0]
				: (KingdomHappeningSemanticReceipt[])semanticReceipts.Clone();
		}

		internal KingdomHappeningSemanticReceipt[] CopySemanticReceipts()
		{
			return (KingdomHappeningSemanticReceipt[])SemanticReceipts.Clone();
		}

		internal static KingdomHappeningLifecycleBook Empty
		{
			get { return new KingdomHappeningLifecycleBook(0, null); }
		}
	}
}
