using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	/// <summary>Pure durable-slot rule for load recovery deferred by the realm master switch.
	/// One successful load replaces the slot; only an allowed later wake consumes it.</summary>
	internal static class KingdomInheritanceResumeRules
	{
		internal static bool TryConsume(bool Pending, int LoadKindValue,
			string SourceFailure, bool AutomaticWorkAllowed,
			out KingdomInheritanceLoadKind LoadKind, out string Failure)
		{
			LoadKind = KingdomInheritanceLoadKind.Unknown;
			Failure = SourceFailure ?? "";
			if (!Pending || !AutomaticWorkAllowed) return false;
			if (Enum.IsDefined(typeof(KingdomInheritanceLoadKind), LoadKindValue))
			{
				LoadKind = (KingdomInheritanceLoadKind)LoadKindValue;
			}
			else
			{
				Failure = "the saved deferred inheritance load kind was invalid";
			}
			return true;
		}
	}

}
