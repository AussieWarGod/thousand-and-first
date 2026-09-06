using System;

namespace ThousandAndFirst
{
	/// <summary>Only an exact saved capacity refusal can bypass the existing Chronicle publisher.
	/// This settles telling alone; accounting, role closure and body custody remain with the journal.</summary>
	internal static class KingdomResidentDepartureStoryRules
	{
		internal static bool TrySettle(KingdomResidentDepartureOperation operation, string archive,
			Func<bool> exact, Func<KingdomChronicleCapacityWitness> observe, Func<bool> reprove,
			Func<string, bool> save, Func<bool> record)
		{
			if (!KingdomResidentDepartureRules.Valid(operation) || !operation.Chronicled
				|| operation.Phase != (int)KingdomResidentDeparturePhase.RolesClosed
				|| exact == null || observe == null || reprove == null || save == null || record == null) return false;
			try
			{
				if (!exact() || !KingdomResidentDepartureCapacityArchive.TryMatch(archive, operation, out bool retained)) return false;
				if (retained) return exact();
				KingdomChronicleCapacityWitness witness = observe();
				if (witness != null)
				{
					if (!KingdomResidentDepartureCapacityArchive.TryRetain(archive, operation, witness, out string next)
						|| !exact() || !reprove() || !save(next) || !reprove()) return false;
					return exact();
				}
				return exact() && record() && exact();
			}
			catch { return false; }
		}
	}
}
