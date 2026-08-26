using ConsoleLib.Console;

namespace ThousandAndFirst
{
	/// <summary>Engine-coupled presentation boundary for plain persisted founder text.</summary>
	public static class KingdomPresentation
	{
		public static string Rich(string Plain)
		{
			return ColorUtility.EscapeFormatting(Plain ?? "");
		}
	}
}
