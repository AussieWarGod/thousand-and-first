using System;

namespace ThousandAndFirst
{
	/// <summary>The adapter owns exact parent, carrier and attachment custody. Current must not
	/// inspect the released carrier. Every observation/write independently reproves that custody.</summary>
	internal interface IKingdomSubsidenceReleasePort
	{
		KingdomSubsidenceRungPlan Plan { get; }
		bool Current { get; }
		bool TryObserve(out KingdomSubsidenceWearReceipt receipt);
		bool Publish(KingdomSubsidenceRungPlan expected, KingdomSubsidenceRungPlan next);
		bool Write(int field, KingdomSubsidenceWearReceipt expected, KingdomSubsidenceWearReceipt target);
	}

	internal static class KingdomSubsidenceReleaseDriver
	{
		internal static bool Resume(IKingdomSubsidenceReleasePort port, int index)
		{
			try
			{
				if (port == null || !port.Current) return false;
				KingdomSubsidenceRungPlan plan = port.Plan;
				if (!KingdomSubsidenceRungRules.Valid(plan) || index < 0 || index >= plan.Works.Count)
					return false;
				// Durable parent proof retires the dependency; never inspect the former carrier here.
				if (plan.Works[index].ReleasePhase == KingdomSubsidenceReleasePhase.Released)
					return Current(port, plan);
				if (!Observe(port, plan, out KingdomSubsidenceWearReceipt observed)) return false;
				if (plan.Works[index].ReleasePhase == KingdomSubsidenceReleasePhase.Pending)
				{
					if (!KingdomSubsidenceRungRules.TryArmRelease(plan, index, true, observed,
						out KingdomSubsidenceRungPlan intent) || !Publish(port, plan, intent)) return false;
					plan = port.Plan;
				}
				KingdomSubsidenceRungWork work = plan.Works[index];
				for (int attempt = 0; attempt <= 4; attempt++)
				{
					if (!Observe(port, plan, out observed)
						|| !KingdomSubsidenceReleaseRules.TryNextWrite(work.ReleaseBefore, work.ReleaseAfter,
							observed, out int field)) return false;
					if (field == 4)
						return KingdomSubsidenceRungRules.TryProveRelease(plan, index, true, observed,
							out KingdomSubsidenceRungPlan released) && Publish(port, plan, released);
					KingdomSubsidenceWearReceipt target = KingdomSubsidenceReleaseRules.AfterWrite(
						work.ReleaseBefore, work.ReleaseAfter, field + 1);
					if (!Current(port, plan) || !port.Write(field, observed, target)
						|| !Observe(port, plan, out KingdomSubsidenceWearReceipt measured)
						|| !KingdomSubsidenceReleaseRules.Same(target, measured)) return false;
				}
				return false;
			}
			catch (Exception) { return false; }
		}

		private static bool Current(IKingdomSubsidenceReleasePort port, KingdomSubsidenceRungPlan plan)
			=> port.Current && ReferenceEquals(port.Plan, plan);

		private static bool Observe(IKingdomSubsidenceReleasePort port, KingdomSubsidenceRungPlan plan,
			out KingdomSubsidenceWearReceipt receipt)
		{
			receipt = null;
			return Current(port, plan) && port.TryObserve(out receipt) && receipt != null && Current(port, plan);
		}

		private static bool Publish(IKingdomSubsidenceReleasePort port, KingdomSubsidenceRungPlan prior,
			KingdomSubsidenceRungPlan next)
		{
			return Current(port, prior) && port.Publish(prior, next) && port.Current
				&& KingdomSubsidenceRungCodec.TryEncode(next, out string expected)
				&& KingdomSubsidenceRungCodec.TryEncode(port.Plan, out string actual) && expected == actual;
		}
	}
}
