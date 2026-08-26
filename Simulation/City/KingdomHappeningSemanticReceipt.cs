using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal readonly struct KingdomHappeningSemanticReceipt
	{
		internal readonly KingdomPhysicalHappeningKind Kind;
		internal readonly int SubjectA;
		internal readonly int SubjectB;

		internal KingdomHappeningSemanticReceipt(KingdomPhysicalHappeningKind kind,
			int subjectA, int subjectB)
		{
			Kind = kind;
			if (kind == KingdomPhysicalHappeningKind.Wedding && subjectB < subjectA)
			{
				SubjectA = subjectB;
				SubjectB = subjectA;
			}
			else
			{
				SubjectA = subjectA;
				SubjectB = subjectB;
			}
		}
	}
}
