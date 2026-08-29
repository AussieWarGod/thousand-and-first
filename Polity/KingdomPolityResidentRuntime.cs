using System.Globalization;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>Exact bridge from the seated resident book; never from a transient body id.</summary>
	public static class KingdomPolityResidentRuntime
	{
		public static bool TryReconcile(KingdomSystem System, long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded ||
				System.PolityLedger == null) return true;
			KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
			if (succession == null) return true;
			if (!succession.TryPolitySuccessorBridge(System, out int residentId,
				out string settlementId, out string name, out int revision,
				out bool present, out Failure)) return false;
			if (present)
				return KingdomPolityRules.TryEnsureResidentSuccessor(System.PolityLedger,
					System.PolityLedger.Revision, settlementId, residentId, name, revision, Tick,
					out KingdomPolityPublicationResult _, out Failure);
			string cause = KingdomPolityRules.ActivationId("taf:fact:successor-reconcile:v1:",
				"successor-reconcile-v1", System.RealmId,
				revision.ToString(CultureInfo.InvariantCulture));
			return KingdomPolityRules.TryRetireResidentSuccessor(System.PolityLedger,
				System.PolityLedger.Revision, cause, Tick,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
