using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	/// <summary>Async-flow source capture for Harmony's prefix on XRLGame.LoadGame. A postfix
	/// cannot be used: the original is async and sends AfterGameLoaded after awaits.</summary>
	internal static class KingdomInheritanceLoadSourceFlow
	{
		private sealed class LoadSource
		{
			internal readonly string Path;

			internal bool Consumed;

			internal LoadSource(string Path)
			{
				this.Path = Path ?? "";
			}
		}

		private static readonly AsyncLocal<LoadSource> Current = new AsyncLocal<LoadSource>();

		internal static void Record(string Path)
		{
			Current.Value = new LoadSource(Path);
		}

		internal static bool TryConsume(out string Path)
		{
			Path = "";
			LoadSource source = Current.Value;
			if (source == null || source.Consumed)
			{
				return false;
			}
			source.Consumed = true;
			Path = source.Path;
			Current.Value = null;
			return true;
		}

		internal static void Clear()
		{
			Current.Value = null;
		}
	}

}
