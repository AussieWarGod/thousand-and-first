using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Dev-only evidence framing. At a DPI-scaled tile size the native view shows about eighteen
	/// zone rows, one short of an XL footprint with its yard; Qud's own play-area scale override
	/// fits the whole 80-by-25 stage into the window instead. Presentation only: the option
	/// override lives in memory for this process, the sealed profile's options are untouched, and
	/// no architecture, receipt, or production state changes.
	/// </summary>
	internal static class KingdomScenarioFit
	{
		internal const string Verb = "fit";

		internal static string Run(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			if (!GameObject.Validate(player) || player.CurrentZone == null)
				return "{{R|Fit refused}}: fit runs only with a live player in a loaded zone.";
			GameManager manager = GameManager.Instance;
			if (manager == null || manager.uiQueue == null || GameManager.MainCameraLetterbox == null)
				return "{{R|Fit refused}}: Qud's native game manager, UI queue, or letterbox camera is unavailable.";
			try
			{
				manager.uiQueue.awaitTask(delegate
				{
					Options.PlayScaleOverride = Options.PlayAreaScaleTypes.Fit;
					manager.RefreshLayout(updateForceFullscreenIfSwapped: true);
					GameManager.MainCameraLetterbox.OnUpdate();
				});
			}
			catch (System.Exception exception)
			{
				return "{{R|Fit refused}}: Qud refused the UI-thread layout: "
					+ KingdomScenarioRules.Bounded(exception.Message);
			}
			Ok = true;
			return "Fitted the whole 80x25 stage into the native window for evidence capture; the "
				+ "override lives only in this process.";
		}
	}
}
