using System;
using System.Globalization;
using ThousandAndFirst.Simulation.Kernel;
using XRL.Names;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRecruitment
	{
		/// <summary>Generate before publication without allocating a sample creature or consuming
		/// ambient game/naming draws. The native stack excludes lifetime level-up state.</summary>
		private static bool TryNativeName(KingdomSystem System, SemanticEventKey Key,
			GameObjectBlueprint Body, string Faction, out string Name, out string Failure)
		{
			Name = Failure = null;
			if (!CounterRandom.TryDrawBelow(System.SimulationSeed, Key, 32U, int.MaxValue,
				out ulong seed, out _)) { Failure = "settler name seed refused"; return false; }
			Random naming = Stat.NamingRnd;
			Stat.PushState("taf-recruit-name-v1:" + seed.ToString(CultureInfo.InvariantCulture));
			try
			{
				Stat.NamingRnd = new Random((int)seed);
				Name = NameMaker.MakeName(Species: Body.GetTag("Species", null),
					Culture: Body.GetTag("Culture", null), Faction: Faction,
					Region: Body.GetTag("NamingRegion", null), Tag: Body.GetTag("NamingTag", null),
					FailureOkay: true);
				if (ValidText(Name)) return true;
				Failure = "native settler naming returned no bounded name"; return false;
			}
			catch (Exception Error)
			{
				Failure = "native settler naming refused: " + Error.GetType().Name; return false;
			}
			finally { Stat.NamingRnd = naming; Stat.PopState(); }
		}
	}
}
