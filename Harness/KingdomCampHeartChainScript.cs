using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The two sealed forms of the paid heart chain. The four-rung form is unchanged and its
	/// steps are byte-identical to what shipped; the five-rung form is a strict extension of it,
	/// so a persona that drives rungs one to four cannot silently acquire the arcology leg and a
	/// persona that drives the arcology leg cannot skip a rung below it.
	/// </summary>
	internal static class KingdomCampHeartChainScript
	{
		internal const string Setup = "camp-heart-chain-setup";
		internal const string Check = "camp-heart-chain-check";
		internal const string Supply = "camp-heart-chain-supply";
		internal const string Capital = "camp-heart-chain-capital";
		private static readonly string[] Steps = {
			"stagedigest", "camp-heart-setup", "advance 1200", "camp-heart-check",
			"advance 3600", "camp-heart-check", "advance 1200", "camp-heart-check", "stagedigest",
			Setup, "advance 1200", Supply, "advance 1200", Check, "advance 7200", Check,
			Supply, "advance 1200", Check, "advance 6600", "advance 6600", Check, "advance 1200", Check };

		// The arcology leg. The capital seed runs first and spends no turns; the four 6600-turn
		// waits cover the fifth rung's own quoted labour, which is the largest on the ladder.
		private static readonly string[] Rung5Extra = {
			Capital, Supply, "advance 1200", Check, "advance 6600", "advance 6600",
			"advance 6600", "advance 6600", Check, "advance 1200", Check };

		internal static bool Matches(IList<string> Script)
		{
			return Script != null && Script.Count == Steps.Length && Prefix(Script);
		}

		/// <summary>The five-rung form: the four-rung steps, then the arcology leg.</summary>
		internal static bool MatchesRung5(IList<string> Script)
		{
			if (Script == null || Script.Count != Steps.Length + Rung5Extra.Length
				|| !Prefix(Script)) return false;
			for (int i = 0; i < Rung5Extra.Length; i++)
				if (Script[Steps.Length + i] != Rung5Extra[i]) return false;
			return true;
		}

		/// <summary>The highest rung the sealed script drives, or zero when neither exact form is
		/// sealed. Every chain read asks this rather than testing one form, so an unsealed or
		/// edited script is refused by number rather than falling through to the lower leg.</summary>
		internal static int SealedTargetRung(IList<string> Script)
		{
			if (MatchesRung5(Script)) return 5;
			return Matches(Script) ? 4 : 0;
		}

		private static bool Prefix(IList<string> Script)
		{
			for (int i = 0; i < Steps.Length; i++) if (Script[i] != Steps[i]) return false;
			return true;
		}
	}
}
