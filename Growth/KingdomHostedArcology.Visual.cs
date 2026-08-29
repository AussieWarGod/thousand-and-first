using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Additive, idempotent realization of paid furniture on a currently loaded floor.</summary>
	public static class KingdomHostedArcologyVisual
	{
		private sealed class Fixture
		{
			internal int X; internal int Y; internal string Blueprint; internal string Role;
			internal Fixture(int X, int Y, string Blueprint, string Role)
			{ this.X = X; this.Y = Y; this.Blueprint = Blueprint; this.Role = Role; }
		}

		private static readonly Fixture[] Ward = new Fixture[] {
			new Fixture(8,5,"r_KingdomFixtureBedMetal","bed01"), new Fixture(8,9,"r_KingdomFixtureBedMetal","bed02"),
			new Fixture(8,15,"r_KingdomFixtureBedMetal","bed03"), new Fixture(8,19,"r_KingdomFixtureBedMetal","bed04"),
			new Fixture(25,5,"r_KingdomFixtureBedMetal","bed05"), new Fixture(25,19,"r_KingdomFixtureBedMetal","bed06"),
			new Fixture(54,5,"r_KingdomFixtureBedMetal","bed07"), new Fixture(54,19,"r_KingdomFixtureBedMetal","bed08"),
			new Fixture(71,5,"r_KingdomFixtureBedMetal","bed09"), new Fixture(71,9,"r_KingdomFixtureBedMetal","bed10"),
			new Fixture(71,15,"r_KingdomFixtureBedMetal","bed11"), new Fixture(71,19,"r_KingdomFixtureBedMetal","bed12"),
			new Fixture(15,12,"r_KingdomFixtureLockerScrap","locker01"), new Fixture(64,12,"r_KingdomFixtureLockerScrap","locker02"),
			new Fixture(37,12,"r_KingdomFixtureTableMarble","table"), new Fixture(35,12,"r_KingdomFixtureChairMetal","chair01"),
			new Fixture(40,12,"r_KingdomFixtureChairMetal","chair02"), new Fixture(44,12,"r_KingdomFixtureChairMetal","chair03")
		};

		private static readonly Fixture[] Terrace = new Fixture[] {
			new Fixture(9,5,"r_KingdomArcologyGrowbed","bed01"), new Fixture(16,5,"r_KingdomArcologyGrowbed","bed02"),
			new Fixture(23,5,"r_KingdomArcologyGrowbed","bed03"), new Fixture(30,5,"r_KingdomArcologyGrowbed","bed04"),
			new Fixture(49,5,"r_KingdomArcologyGrowbed","bed05"), new Fixture(56,5,"r_KingdomArcologyGrowbed","bed06"),
			new Fixture(63,5,"r_KingdomArcologyGrowbed","bed07"), new Fixture(70,5,"r_KingdomArcologyGrowbed","bed08"),
			new Fixture(9,18,"r_KingdomArcologyGrowbed","bed09"), new Fixture(16,18,"r_KingdomArcologyGrowbed","bed10"),
			new Fixture(23,18,"r_KingdomArcologyGrowbed","bed11"), new Fixture(30,18,"r_KingdomArcologyGrowbed","bed12"),
			new Fixture(49,18,"r_KingdomArcologyGrowbed","bed13"), new Fixture(70,18,"r_KingdomArcologyGrowbed","bed14"),
			new Fixture(39,8,"r_KingdomArcologyRiser","riser"), new Fixture(39,16,"r_KingdomArcologyConduit","tap"),
			new Fixture(37,12,"r_KingdomFixtureTableStone","table"), new Fixture(42,12,"r_KingdomFixtureChairMetal","chair")
		};

		public static bool Reconcile(Zone Z, string LotKey)
		{
			GameObject shell = KingdomHostedArcology.RootOf(Z);
			r_KingdomArcology root = shell?.GetPart<r_KingdomArcology>();
			KingdomHostedLotReceipt receipt; string failure;
			if (root == null || !KingdomHostedArcology.TryReceipt(root, LotKey,
				out receipt, out failure)) return false;
			if (receipt == null || receipt.Phase != KingdomHostedLotPhase.Active) return true;
			string shellId = shell.IDIfAssigned;
			if (string.IsNullOrEmpty(shellId))
			{
				KingdomHostedArcology.Quarantine(root, "The hosted shell lacks assigned identity.");
				return false;
			}
			Fixture[] fixtures = LotKey == "arcologyward" ? Ward
				: LotKey == "arcologyterrace" ? Terrace : null;
			if (fixtures == null) return true;
			for (int i = 0; i < fixtures.Length; i++)
			{
				Fixture spec = fixtures[i];
				string id = KingdomHostedArcologyRules.StableChildId(shellId,
					LotKey + ":fixture:" + spec.Role);
				GameObject exact = null; int count = 0;
				foreach (GameObject item in Z.GetObjects())
					if (item.IDIfAssigned == id) { exact = item; count++; }
				if (count > 1 || (count == 1 && (exact.Blueprint != spec.Blueprint
					|| exact.CurrentCell != Z.GetCell(spec.X, spec.Y))))
				{
					KingdomHostedArcology.Quarantine(root, "A hosted-floor fixture ID is duplicated or displaced.");
					return false;
				}
				if (count == 1) continue;
				try
				{
					GameObject created = GameObject.Create(spec.Blueprint); created.ID = id;
					GameObject accepted = Z.GetCell(spec.X, spec.Y).AddObject(created,
						Forced: true, System: true, NoStack: true, Silent: true);
					if (!ReferenceEquals(created, accepted)) throw new InvalidOperationException();
				}
				catch
				{
					KingdomHostedArcology.Quarantine(root, "A paid hosted-floor fixture could not be realized exactly.");
					return false;
				}
			}
			return true;
		}
	}
}
