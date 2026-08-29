using XRL.UI;
using XRL.Wish;

namespace ThousandAndFirst
{
	/// <summary>Explicit diagnostic surface only. Recording is separately opt-in and no export is
	/// written during load, turn processing, presentation, or save.</summary>
	public static class KingdomExperienceWishes
	{
		[WishCommand("kingdom:experience-export")]
		public static void ExportExperienceSession()
		{
			if (KingdomExperienceRuntime.TryExport(out string path, out string failure))
				Popup.Show("The bounded experience session was written locally to:\n\n" + path);
			else Popup.Show("No experience session was written.\n\n" + failure);
		}
	}
}
