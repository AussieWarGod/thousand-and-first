using XRL;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Runs only at the completed stationary settlement boundary.</summary>
		private void ReconcileStationaryPolity()
		{
			if (!Simulation.City.KingdomSemanticDispatcher.IsStationaryDispatch) return;
			if (!KingdomPolityActiveRuntime.TryReconcile(this, The.Game.TimeTicks,
				out string failure))
				KingdomLog.Log("polity: daily reconciliation refused (" + failure + ")");
		}
	}
}
