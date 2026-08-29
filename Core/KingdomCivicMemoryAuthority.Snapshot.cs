using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemoryAuthority
	{
		/// <summary>
		/// Reads an untrusted list once while the mutation gate is already held. A caller in this
		/// build may author at most one row per known family, so a larger count is impossible and
		/// refused before indexing or allocation.
		/// </summary>
		private static bool Snapshot(IList<KingdomCivicMemorySection> Candidate,
			out List<KingdomCivicMemorySection> Snapshot, out string Failure)
		{
			Snapshot = new List<KingdomCivicMemorySection>();
			if (Candidate == null)
			{
				Failure = "civic memory was offered no section list";
				return false;
			}
			try
			{
				int count = Candidate.Count;
				if (count < 0 || count > KingdomCivicMemoryLimits.KnownSectionCount)
				{
					Failure = "civic memory commit count " + count + " is outside 0 through "
						+ KingdomCivicMemoryLimits.KnownSectionCount;
					return false;
				}
				Snapshot = new List<KingdomCivicMemorySection>(count);
				for (int i = 0; i < count; i++) Snapshot.Add(Candidate[i]);
			}
			catch (Exception e) when (RecoverableInspectionFailure(e))
			{
				Failure = "civic memory could not take a stable snapshot of the offered sections ("
					+ e.Message + ")";
				return false;
			}
			Failure = "";
			return true;
		}

		private static bool RecoverableInspectionFailure(Exception Failure)
		{
			return !(Failure is OutOfMemoryException)
				&& !(Failure is StackOverflowException)
				&& !(Failure is AccessViolationException)
				&& !(Failure is AppDomainUnloadedException);
		}
	}
}
