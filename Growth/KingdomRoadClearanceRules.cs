using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Open route role plus deterministic preferred/minimum occupied width.</summary>
	public readonly struct KingdomRoadFrontage
	{
		public readonly string Role;
		public readonly int PreferredWidth;
		public readonly int MinimumWidth;

		public KingdomRoadFrontage(string Role, int PreferredWidth, int MinimumWidth)
		{
			this.Role = Role;
			this.PreferredWidth = PreferredWidth;
			this.MinimumWidth = MinimumWidth;
		}
	}

	/// <summary>One extensible building-to-frontage declaration.</summary>
	public sealed class KingdomRoadFrontageRule
	{
		public string Key { get; private set; }
		public string BuildingKey { get; private set; }
		public KingdomRoadFrontage Frontage { get; private set; }
		public int Priority { get; private set; }

		public KingdomRoadFrontageRule(string Key, string BuildingKey, string Role,
			int PreferredWidth, int MinimumWidth, int Priority = 0)
		{
			this.Key = Key == null ? null : Key.Trim().ToLowerInvariant();
			this.BuildingKey = BuildingKey == null ? null : BuildingKey.Trim().ToLowerInvariant();
			this.Frontage = new KingdomRoadFrontage(
				Role == null ? null : Role.Trim().ToLowerInvariant(), PreferredWidth, MinimumWidth);
			this.Priority = Priority;
		}
	}

	/// <summary>Pure frontage classification and deterministic optional two-cell clearance.</summary>
	public static class KingdomRoadClearanceRules
	{
		public const int MaximumWidth = 2;
		public const int MaxRegisteredRules = 64;

		private static readonly KingdomRoadFrontageRule[] BuiltIns =
		{
			R("bazaar", "bazaar", KingdomRoadPaletteRules.MarketRole, 2),
			R("caravanserai", "caravanserai", KingdomRoadPaletteRules.CaravanRole, 2),
			R("dromad-shade", "dromadcaravanshade", KingdomRoadPaletteRules.CaravanRole, 2),
			R("great-court", "heartcourt", KingdomRoadPaletteRules.MonumentalRole, 2),
			R("crown-hall", "crownhall", KingdomRoadPaletteRules.MonumentalRole, 2),
			R("mirror-gate", "mirrorgate", KingdomRoadPaletteRules.MonumentalRole, 2),
			R("assenting-moot", "assentingmoot", KingdomRoadPaletteRules.MonumentalRole, 2),
			R("arcology", "arcology", KingdomRoadPaletteRules.MonumentalRole, 2)
		};

		private static readonly object Sync = new object();
		private static readonly List<KingdomRoadFrontageRule> Registered =
			new List<KingdomRoadFrontageRule>();

		public static KingdomRoadFrontage ForRoute(KingdomRoadRules.RouteKind Kind)
		{
			return Kind == KingdomRoadRules.RouteKind.HeartToGate
				? new KingdomRoadFrontage(KingdomRoadPaletteRules.GateRole, 2, 1)
				: new KingdomRoadFrontage(KingdomRoadPaletteRules.LocalRole, 1, 1);
		}

		public static KingdomRoadFrontage ForArchitecture(string BuildingKey,
			ArchitectureLayoutSnapshot Snapshot, ArchitectureAnchor Entrance)
		{
			List<KingdomRoadFrontageRule> rules = new List<KingdomRoadFrontageRule>(BuiltIns);
			lock (Sync) rules.AddRange(Registered);
			return Resolve(rules, BuildingKey, Snapshot, Entrance);
		}

		public static KingdomRoadFrontage Resolve(IList<KingdomRoadFrontageRule> Rules,
			string BuildingKey, ArchitectureLayoutSnapshot Snapshot, ArchitectureAnchor Entrance)
		{
			KingdomRoadFrontage result = new KingdomRoadFrontage(
				KingdomRoadPaletteRules.LocalRole, 1, 1);
			if (Entrance != null && IsServiceEntrance(Entrance.Key))
				result = Merge(result, new KingdomRoadFrontage(
					KingdomRoadPaletteRules.ServiceRole, 2, 1));
			if (Snapshot != null && Snapshot.Anchors != null && Entrance != null)
			{
				for (int i = 0; i < Snapshot.Anchors.Count; i++)
				{
					ArchitectureAnchor anchor = Snapshot.Anchors[i];
					if (anchor == null || anchor.X != Entrance.X || anchor.Y != Entrance.Y) continue;
					if (anchor.Key == "service:loading" || IsServiceEntrance(anchor.Key))
						result = Merge(result, new KingdomRoadFrontage(
							KingdomRoadPaletteRules.ServiceRole, 2, 1));
					else if (anchor.Key == "market:stall")
						result = Merge(result, new KingdomRoadFrontage(
							KingdomRoadPaletteRules.MarketRole, 2, 1));
				}
			}
			KingdomRoadFrontageRule best = null;
			if (Rules != null && !string.IsNullOrEmpty(BuildingKey))
			{
				for (int i = 0; i < Rules.Count; i++)
				{
					KingdomRoadFrontageRule rule = Rules[i];
					if (!Valid(rule) || !string.Equals(rule.BuildingKey, BuildingKey,
						StringComparison.OrdinalIgnoreCase)) continue;
					if (best == null || rule.Priority > best.Priority
						|| (rule.Priority == best.Priority
							&& string.CompareOrdinal(rule.Key, best.Key) < 0)) best = rule;
				}
			}
			return best == null ? result : Merge(result, best.Frontage);
		}

		public static bool RegisterFrontageRule(KingdomRoadFrontageRule Rule,
			out string Failure)
		{
			Failure = null;
			if (!Valid(Rule))
			{
				Failure = "The road-frontage rule is malformed.";
				return false;
			}
			lock (Sync)
			{
				KingdomRoadFrontageRule prior = Find(BuiltIns, Rule.Key) ?? Find(Registered, Rule.Key);
				if (prior != null)
				{
					if (Equivalent(prior, Rule)) return true;
					Failure = "Road-frontage key " + Rule.Key + " is already registered differently.";
					return false;
				}
				if (Registered.Count >= MaxRegisteredRules)
				{
					Failure = "The road-frontage extension registry is full.";
					return false;
				}
				Registered.Add(Rule);
				return true;
			}
		}

		/// <summary>
		/// Expands an already-proved wearable centreline. North then south is canonical for a
		/// mostly-horizontal way; west then east for a mostly-vertical way. If preferred width
		/// cannot fit, the declared minimum decides whether one cell is accepted or all refused.
		/// </summary>
		public static bool TryExpand(KingdomRoadRules.CellFilter Clear, int Width, int Height,
			int FromX, int FromY, int ToX, int ToY, IList<int> Centreline,
			KingdomRoadFrontage Frontage, IList<int> Cells, out int ActualWidth)
		{
			ActualWidth = 0;
			if (Cells == null) return false;
			Cells.Clear();
			if (Clear == null || Width <= 0 || Height <= 0 || Centreline == null
				|| Frontage.MinimumWidth < 1 || Frontage.PreferredWidth < Frontage.MinimumWidth
				|| Frontage.PreferredWidth > MaximumWidth
				|| Centreline.Count > KingdomRoadRules.MaxRouteCells
				|| !KingdomRoadRules.InBounds(FromX, FromY, Width, Height)
				|| !KingdomRoadRules.InBounds(ToX, ToY, Width, Height)) return false;
			int from = KingdomRoadRules.Pack(FromX, FromY, Width);
			int to = KingdomRoadRules.Pack(ToX, ToY, Width);
			HashSet<int> centre = new HashSet<int>();
			for (int i = 0; i < Centreline.Count; i++)
			{
				int cell = Centreline[i];
				int x = KingdomRoadRules.UnpackX(cell, Width);
				int y = KingdomRoadRules.UnpackY(cell, Width);
				if (!KingdomRoadRules.InBounds(x, y, Width, Height)
					|| KingdomRoadRules.Pack(x, y, Width) != cell || cell == from || cell == to
					|| !centre.Add(cell)) return false;
			}
			if (Centreline.Count == 0)
			{
				if (Frontage.MinimumWidth > 1) return false;
				ActualWidth = 1;
				return true;
			}
			if (Frontage.PreferredWidth == 2)
			{
				HashSet<int> blocked = new HashSet<int>(centre) { from, to };
				long dx = (long)ToX - FromX;
				long dy = (long)ToY - FromY;
				if (Math.Abs(dx) >= Math.Abs(dy))
				{
					if (TrySide(Clear, Width, Height, Centreline, blocked, 0, -1, Cells)
						|| TrySide(Clear, Width, Height, Centreline, blocked, 0, 1, Cells))
					{
						ActualWidth = 2;
						return true;
					}
				}
				else if (TrySide(Clear, Width, Height, Centreline, blocked, -1, 0, Cells)
					|| TrySide(Clear, Width, Height, Centreline, blocked, 1, 0, Cells))
				{
					ActualWidth = 2;
					return true;
				}
			}
			if (Frontage.MinimumWidth > 1) return false;
			for (int i = 0; i < Centreline.Count; i++) Cells.Add(Centreline[i]);
			ActualWidth = 1;
			return true;
		}

		public static KingdomRoadFrontage Merge(KingdomRoadFrontage A,
			KingdomRoadFrontage B)
		{
			string a = KingdomRoadPaletteRules.TryRole(A.Role, out var ar)
				? ar : KingdomRoadPaletteRules.LocalRole;
			string b = KingdomRoadPaletteRules.TryRole(B.Role, out var br)
				? br : KingdomRoadPaletteRules.LocalRole;
			int aw = ClampWidth(A.PreferredWidth), bw = ClampWidth(B.PreferredWidth);
			bool chooseB = Rank(b) > Rank(a) || (Rank(b) == Rank(a)
				&& (bw > aw || (bw == aw && string.CompareOrdinal(b, a) < 0)));
			string role = chooseB ? b : a;
			return new KingdomRoadFrontage(role, Math.Max(aw, bw),
				Math.Max(1, Math.Min(Math.Max(A.MinimumWidth, B.MinimumWidth), MaximumWidth)));
		}

		private static bool TrySide(KingdomRoadRules.CellFilter Clear, int Width, int Height,
			IList<int> Centreline, ISet<int> Centre, int DX, int DY, IList<int> Cells)
		{
			List<int> side = new List<int>();
			HashSet<int> seen = new HashSet<int>();
			for (int i = 0; i < Centreline.Count; i++)
			{
				int x = KingdomRoadRules.UnpackX(Centreline[i], Width) + DX;
				int y = KingdomRoadRules.UnpackY(Centreline[i], Width) + DY;
				if (!KingdomRoadRules.InBounds(x, y, Width, Height) || !Clear(x, y)) return false;
				int cell = KingdomRoadRules.Pack(x, y, Width);
				if (Centre.Contains(cell) || !seen.Add(cell)) return false;
				side.Add(cell);
			}
			Cells.Clear();
			for (int i = 0; i < Centreline.Count; i++) Cells.Add(Centreline[i]);
			for (int i = 0; i < side.Count; i++) Cells.Add(side[i]);
			return true;
		}

		private static int Rank(string Role)
		{
			if (Role == KingdomRoadPaletteRules.MonumentalRole) return 60;
			if (Role == KingdomRoadPaletteRules.GateRole) return 50;
			if (Role == KingdomRoadPaletteRules.CaravanRole) return 40;
			if (Role == KingdomRoadPaletteRules.MarketRole) return 30;
			if (Role == KingdomRoadPaletteRules.ServiceRole) return 20;
			return Role == KingdomRoadPaletteRules.LocalRole ? 0 : 10;
		}

		private static int ClampWidth(int Width)
		{
			return Width < 1 ? 1 : (Width > MaximumWidth ? MaximumWidth : Width);
		}

		private static bool IsServiceEntrance(string Key)
		{
			return Key == "entrance:service" || (Key != null
				&& Key.StartsWith("entrance:service@", StringComparison.Ordinal));
		}

		private static bool Valid(KingdomRoadFrontageRule Rule)
		{
			return Rule != null && KingdomRoadPaletteRules.TryRole(Rule.Key, out _)
				&& KingdomRoadPaletteRules.TryRole(Rule.BuildingKey, out _)
				&& KingdomRoadPaletteRules.TryRole(Rule.Frontage.Role, out _)
				&& Rule.Frontage.MinimumWidth >= 1
				&& Rule.Frontage.PreferredWidth >= Rule.Frontage.MinimumWidth
				&& Rule.Frontage.PreferredWidth <= MaximumWidth
				&& Rule.Priority >= -1000 && Rule.Priority <= 1000;
		}

		private static KingdomRoadFrontageRule Find(
			IEnumerable<KingdomRoadFrontageRule> Rules, string Key)
		{
			foreach (KingdomRoadFrontageRule rule in Rules)
				if (rule != null && rule.Key == Key) return rule;
			return null;
		}

		private static bool Equivalent(KingdomRoadFrontageRule A, KingdomRoadFrontageRule B)
		{
			return A.Key == B.Key && A.BuildingKey == B.BuildingKey
				&& A.Frontage.Role == B.Frontage.Role
				&& A.Frontage.PreferredWidth == B.Frontage.PreferredWidth
				&& A.Frontage.MinimumWidth == B.Frontage.MinimumWidth
				&& A.Priority == B.Priority;
		}

		private static KingdomRoadFrontageRule R(string Key, string Building,
			string Role, int Width)
		{
			return new KingdomRoadFrontageRule(Key, Building, Role, Width, 1);
		}
	}
}
