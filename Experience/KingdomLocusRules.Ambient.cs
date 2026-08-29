using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure staffed-locus truth and cosmetic-use bounds. No time window, random draw,
	/// output, or engine body appears here.</summary>
	public static partial class KingdomLocusRules
	{
		public enum KeeperServiceState : byte
		{
			MissingGround = 0,
			Disabled = 1,
			Unstaffed = 2,
			KeeperMissing = 3,
			OtherGround = 4,
			Ready = 5,
			AuthorityUnknown = 6
		}

		/// <summary>The entire O4 ambient vocabulary. None is refusal; the two positive values
		/// are fixed cosmetic readings of existing resident bodies.</summary>
		public enum AmbientUse : byte
		{
			None = 0,
			ShareNews = 1,
			KeepCompany = 2
		}

		public readonly struct AmbientCue
		{
			public readonly AmbientUse Use;
			public readonly string Text;
			public readonly char Color;

			public AmbientCue(AmbientUse Use, string Text, char Color)
			{
				this.Use = Use;
				this.Text = Text;
				this.Color = Color;
			}

			public bool Exists => Use != AmbientUse.None && !string.IsNullOrEmpty(Text);
		}

		public const int AmbientUseCount = 2;
		public const int AmbientDistance = 2;
		public const long AmbientThrottleTicks = 50L;

		/// <summary>The first exact gathering-ground row in the bounded city book is the one
		/// locus authority. Row order is persistent; later grounds cannot silently mint a second
		/// keeper. A malformed column pair or duplicate stable id fails closed.</summary>
		public static int SelectLocusWork(IList<int> WorkIds, IList<string> WorkBlueprints,
			string BenchBlueprint)
		{
			if (WorkIds == null || WorkBlueprints == null
				|| WorkIds.Count != WorkBlueprints.Count
				|| string.IsNullOrEmpty(BenchBlueprint)) return 0;
			int selected = 0;
			for (int i = 0; i < WorkIds.Count; i++)
			{
				if (WorkBlueprints[i] != BenchBlueprint) continue;
				if (WorkIds[i] <= 0) return 0;
				if (selected == 0) selected = WorkIds[i];
			}
			if (selected == 0) return 0;
			int exactIdRows = 0;
			for (int i = 0; i < WorkIds.Count; i++)
				if (WorkIds[i] == selected) exactIdRows++;
			if (exactIdRows != 1) return 0;
			return selected;
		}

		/// <summary>Stable variety without a semantic or cosmetic random draw.</summary>
		public static AmbientUse AmbientUseFor(int ResidentId)
		{
			if (ResidentId <= 0) return AmbientUse.None;
			return (ResidentId & 1) == 0 ? AmbientUse.KeepCompany : AmbientUse.ShareNews;
		}

		public static AmbientCue Cue(AmbientUse Use)
		{
			switch (Use)
			{
			case AmbientUse.ShareNews:
				return new AmbientCue(Use, "*sharing news*", 'C');
			case AmbientUse.KeepCompany:
				return new AmbientCue(Use, "*keeping company*", 'w');
			default:
				return new AmbientCue(AmbientUse.None, null, ' ');
			}
		}

		/// <summary>One in-memory rate limiter, not a due date. Reload forgets it; elapsed time
		/// never banks an act and no missed or future event exists.</summary>
		public static bool MayUse(bool HasUsed, long LastUseTick, long NowTick)
		{
			if (NowTick < 0L || LastUseTick < 0L || (HasUsed && NowTick < LastUseTick)) return false;
			return !HasUsed || NowTick - LastUseTick >= AmbientThrottleTicks;
		}

		/// <summary>All gates checked before the idle event spends the resident's turn. Work-posted
		/// and staged actors are refused; the hook never moves anyone toward the locus.</summary>
		public static bool MayClaim(bool AuthorityEnabled, bool ExistingResident,
			bool SameGround, bool IsKeeper, bool HasWorkPost, bool IsStaged, bool IsPlayer,
			bool IsPlayerLed, int Distance, bool HasUsed, long LastUseTick, long NowTick)
		{
			return AuthorityEnabled && ExistingResident && SameGround && !IsKeeper
				&& !HasWorkPost && !IsStaged && !IsPlayer && !IsPlayerLed
				&& Distance >= 0 && Distance <= AmbientDistance
				&& MayUse(HasUsed, LastUseTick, NowTick);
		}

		public static string BenchDescription(KeeperServiceState State, string KeeperName)
		{
			switch (State)
			{
			case KeeperServiceState.MissingGround:
				return "No owned gathering ground is present, so no civic keeper is serving here.";
			case KeeperServiceState.Disabled:
				return "Split logs worn smooth by sitting. The civic keeper service is disabled; this is ordinary seating.";
			case KeeperServiceState.Unstaffed:
				return "Split logs worn smooth by sitting. The gathering ground has no posted hand and is not operating as a civic locus.";
			case KeeperServiceState.KeeperMissing:
				return "Split logs worn smooth by sitting. A hand is rostered here, but no exact keeper is present on this ground.";
			case KeeperServiceState.OtherGround:
				return "Split logs worn smooth by sitting. The settlement's keeper serves at another gathering ground; this is ordinary seating.";
			case KeeperServiceState.Ready:
				return "Split logs worn smooth by sitting. "
					+ (string.IsNullOrEmpty(KeeperName) ? "The posted keeper" : KeeperName)
					+ " keeps this bench, and most of what is worth knowing about the settlement passes across it sooner or later.";
			case KeeperServiceState.AuthorityUnknown:
				return "Split logs worn smooth by sitting. The settlement cannot prove which gathering ground owns the civic keeper, so this is ordinary seating.";
			default:
				return "The gathering ground's service state cannot be read safely.";
			}
		}
	}
}
