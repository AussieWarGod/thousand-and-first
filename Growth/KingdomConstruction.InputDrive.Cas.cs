namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool InputSourcePhase(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal,
			KingdomConstructionInputSourcePhase next, out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			KingdomConstructionInputSourceLine line = receipt.SourceAt(ordinal);
			if (!KingdomConstructionInputRules.TryTransitionSource(receipt, receipt.Revision,
				ordinal, line.Phase, next, out updated, out fault))
				return InputFault("source transition", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputCargoPhase(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal,
			KingdomConstructionInputCargoPhase next, out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			KingdomConstructionInputCargoLine line = receipt.CargoAt(ordinal);
			if (!KingdomConstructionInputRules.TryTransitionCargo(receipt, receipt.Revision,
				ordinal, line.Phase, next, out updated, out fault))
				return InputFault("cargo transition", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputSourceEvidence(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal, string remainder,
			string before, string after, int lost, out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryUpdateSourceEvidence(receipt,
				receipt.Revision, ordinal, remainder, before, after, lost,
				out updated, out fault)) return InputFault("source evidence", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputCargoEvidence(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal, string objectId,
			KingdomConstructionInputTopology topology, string owner, string zone,
			int x, int y, string before, string after, int spent, int lost,
			out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryUpdateCargoEvidence(receipt,
				receipt.Revision, ordinal, objectId, topology, owner, zone, x, y,
				before, after, spent, lost, out updated, out fault))
				return InputFault("cargo evidence", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputCargoPhaseEvidence(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal,
			KingdomConstructionInputCargoPhase next, string objectId,
			KingdomConstructionInputTopology topology, string owner, string zone,
			int x, int y, string before, string after, int spent, int lost,
			out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			KingdomConstructionInputCargoLine line = receipt.CargoAt(ordinal);
			if (!KingdomConstructionInputRules.TryTransitionCargoWithEvidence(receipt,
				receipt.Revision, ordinal, line.Phase, next, objectId, topology, owner,
				zone, x, y, before, after, spent, lost, out updated, out fault))
				return InputFault("cargo transition evidence", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputChildEvidence(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, int ordinal, int phase,
			long revision, out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryUpdateChildCentral(receipt,
				receipt.Revision, ordinal, phase, revision, out updated, out fault))
				return InputFault("central child evidence", fault, out failure);
			return PublishInputReceipt(job, updated, out job, out failure);
		}

		private static bool InputFault(string operation, KingdomConstructionInputFault fault,
			out string failure)
		{
			failure = "The routed-input " + operation + " was refused (" + fault + ").";
			return false;
		}
	}
}
