using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		private static bool IsKingdomMode(XRLGame Game)
		{
			return Game != null && KingdomSuccessionRules.ModeOn(Game.gameMode,
				Game.GetBooleanGameState(KingdomSuccessionRules.ModeFlagStateKey));
		}

		private static bool SealEnabled()
		{
			return Options.GetOption("r_TAF_OptionSeal", "Yes") != "No";
		}

		private static bool LegacyImportEnabled()
		{
			return Options.GetOption("r_TAF_OptionLegacyImport", "No") == "Yes";
		}

		private static string DeathReason(AfterDieEvent Death)
		{
			if (!string.IsNullOrEmpty(Death?.ThirdPersonReason))
			{
				return Death.ThirdPersonReason;
			}
			if (!string.IsNullOrEmpty(Death?.Reason))
			{
				return Death.Reason;
			}
			return "died, and no one living can say how";
		}

		private static string DeathCategory(AfterDieEvent Death)
		{
			string category = Death?.Dying?.Physics?.LastDeathCategory;
			return string.IsNullOrEmpty(category) ? "unknown" : category;
		}

		private static string MintId()
		{
			return Guid.NewGuid().ToString("N");
		}

		private static string VersionOf(Assembly Assembly)
		{
			try
			{
				return Assembly?.GetName()?.Version?.ToString() ?? "unknown";
			}
			catch (Exception)
			{
				return "unknown";
			}
		}

		private static long SafeTick(long Tick)
		{
			return Tick < 0L ? 0L : Tick;
		}

		private void ReportFailure(string Action, string Failure, Exception Exception = null)
		{
			string failure = string.IsNullOrEmpty(Failure) ? "unknown failure" : Failure;
			string key = (Action ?? "seal") + "\u001f" + failure;
			if (string.Equals(LastFailureKey, key, StringComparison.Ordinal))
			{
				return;
			}
			LastFailureKey = key;
			try
			{
				Exception error = Exception ?? new InvalidOperationException(failure);
				MetricsManager.LogError("ThousandAndFirst: seal " + (Action ?? "action") + " failed closed", error);
				KingdomLog.Log("seal: " + (Action ?? "action") + " failed closed (" + failure + ")");
			}
			catch (Exception)
			{
				// A diagnostic failure must never escape into the game loop.
			}
		}

		private static void LogStaticFailure(string Action, Exception Exception)
		{
			try
			{
				MetricsManager.LogError("ThousandAndFirst: seal " + Action + " failed closed", Exception);
				KingdomLog.Log("seal: " + Action + " failed closed (" + Exception.GetType().Name
					+ ": " + Exception.Message + ")");
			}
			catch (Exception)
			{
			}
		}
	}
}
