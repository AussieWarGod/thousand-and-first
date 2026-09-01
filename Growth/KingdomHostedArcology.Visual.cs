using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Idempotent, fail-closed realization of receipt-backed hosted fixtures.</summary>
	public static class KingdomHostedArcologyVisual
	{
		private sealed class PreparedFixture
		{
			internal KingdomArcologyFixtureSpec Spec;
			internal string Id;
			internal Cell Cell;
			internal GameObject Output;
		}

		public static bool Reconcile(Zone Z, r_KingdomArcologyZoneAnchor Anchor)
		{
			if (Z == null || Anchor == null || Anchor.ParentObject?.CurrentZone != Z) return false;
			GameObject shell = KingdomHostedArcology.RootOf(Z);
			r_KingdomArcology root = shell?.GetPart<r_KingdomArcology>();
			// A legacy, exiled, foreign, uncrowned, or merely resident lookalike is inert.
			// Reconciliation may place fixtures and quarantine state, so the pure fixed-slot
			// authority proof must precede every such mutation regardless of caller.
			if (root == null || !KingdomHostedArcology.IsOperationalPure(shell)) return false;
			if (!KingdomHostedArcologyTopology.IsHostedLotZone(Anchor.LotKey,
				Anchor.ZoneX, Anchor.ZoneY, Anchor.ZoneZ)
				|| Z.X != Anchor.ZoneX || Z.Y != Anchor.ZoneY || Z.Z != Anchor.ZoneZ)
				return Quarantine(root, "A hosted-floor anchor names the wrong designated zone.");
			string anchorId = KingdomHostedArcologyRules.StableChildId(shell.IDIfAssigned,
				KingdomHostedArcologyTopology.StableRole(Anchor.ZoneX, Anchor.ZoneY,
					Anchor.ZoneZ, "anchor"));
			int anchorCount = 0;
			{
				// Scan assigned identity only. Keeping the expected value in the conventional
				// `id` local makes this recovery read visibly identical to the other global
				// identity scans and prevents an accidental identity-assigning read here.
				string id = anchorId;
				foreach (GameObject item in Z.GetObjects())
					if (item.IDIfAssigned == id) anchorCount++;
			}
			if (Anchor.ParentObject.IDIfAssigned != anchorId || anchorCount != 1
				|| Anchor.ParentObject.CurrentCell != Z.GetCell(40, 3))
				return Quarantine(root, "The designated hosted-floor anchor is displaced or ambiguous.");
			KingdomHostedLotReceipt receipt;
			string failure;
			if (!KingdomHostedArcology.TryReceipt(root, Anchor.LotKey,
				out receipt, out failure)) return Quarantine(root, failure);
			if (receipt == null || receipt.Phase != KingdomHostedLotPhase.Active)
			{
				if (Anchor.FixturesRealized)
					KingdomHostedArcology.Quarantine(root,
						"Hosted fixtures stand without an active exact receipt.");
				return !Anchor.FixturesRealized;
			}
			if (string.IsNullOrEmpty(shell.IDIfAssigned))
			{
				KingdomHostedArcology.Quarantine(root,
					"The hosted shell lacks assigned identity.");
				return false;
			}
			KingdomArcologyProgramme programme = KingdomHostedArcologyTopology.ProgrammeAt(
				Anchor.ZoneX, Anchor.ZoneY, Anchor.ZoneZ);
			KingdomHostedLotDefinition definition;
			KingdomArcologyFixtureSpec[] fixtures;
			if (!KingdomHostedArcologyRules.TryHostedLot(Anchor.LotKey, out definition)
				|| definition.ReadOnly || definition.InteriorCell != KingdomHostedArcologyTopology.Schema
				|| !KingdomHostedArcologyProgrammeBuilder.TryPaidFixtures(
					Anchor.LotKey, programme, out fixtures)) return Quarantine(root,
					"The designated hosted floor has no exact fixture programme.");
			List<PreparedFixture> prepared;
			if (!TryPreflight(Z, shell.IDIfAssigned, Anchor, definition, fixtures,
				out prepared, out failure)) return Quarantine(root, failure);
			if (prepared.Count > 0 && !TryPlacePrepared(Z, shell.IDIfAssigned,
				Anchor, fixtures, prepared, out failure)) return Quarantine(root, failure);
			if (!ProvesExactFixtures(Z, shell.IDIfAssigned, Anchor, fixtures))
				return Quarantine(root, "The hosted-floor fixture manifest failed final proof.");
			Anchor.FixturesRealized = true;
			return true;
		}

		private static bool TryPreflight(Zone Z, string RootId,
			r_KingdomArcologyZoneAnchor Anchor, KingdomHostedLotDefinition Definition,
			KingdomArcologyFixtureSpec[] Fixtures, out List<PreparedFixture> Prepared,
			out string Failure)
		{
			Prepared = new List<PreparedFixture>(); Failure = null;
			if (Fixtures == null || Fixtures.Length < 1 || Fixtures.Length > 256)
				return Fail("The paid hosted-floor fixture manifest is malformed.", out Failure);
			Dictionary<string, GameObject> exact = new Dictionary<string, GameObject>(
				StringComparer.Ordinal);
			HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> cells = new HashSet<string>(StringComparer.Ordinal);
			int producers = 0;
			for (int i = 0; i < Fixtures.Length; i++)
			{
				KingdomArcologyFixtureSpec spec = Fixtures[i];
				if (spec == null)
					return Fail("The paid hosted-floor fixture manifest is malformed.", out Failure);
				string id = FixtureId(RootId, Anchor, spec);
				string cellKey = spec.X + ":" + spec.Y;
				if (string.IsNullOrEmpty(spec.Blueprint) || string.IsNullOrEmpty(spec.Role)
					|| string.IsNullOrEmpty(id) || !wanted.Add(id) || !cells.Add(cellKey)
					|| Z.GetCell(spec.X, spec.Y) == null)
					return Fail("The paid hosted-floor fixture manifest is malformed.", out Failure);
				if (spec.Blueprint == Definition.PhysicalProducerBlueprint) producers++;
			}
			if (producers != Definition.PhysicalProducerCount)
				return Fail("The paid hosted-floor physical producer manifest has drifted.", out Failure);
			foreach (GameObject item in Z.GetObjects())
			{
				string id = item.IDIfAssigned;
				if (string.IsNullOrEmpty(id) || !wanted.Contains(id)) continue;
				if (exact.ContainsKey(id))
					return Fail("A hosted-floor fixture ID is duplicated, mistyped, or displaced.",
						out Failure);
				exact.Add(id, item);
			}
			for (int i = 0; i < Fixtures.Length; i++)
			{
				KingdomArcologyFixtureSpec spec = Fixtures[i];
				string id = FixtureId(RootId, Anchor, spec);
				Cell cell = Z.GetCell(spec.X, spec.Y);
				GameObject found;
				if (exact.TryGetValue(id, out found))
				{
					if (found.Blueprint != spec.Blueprint || found.CurrentCell != cell)
						return Fail("A hosted-floor fixture ID is duplicated, mistyped, or displaced.",
							out Failure);
					continue;
				}
				if (Anchor.FixturesRealized)
					return Fail("A realized hosted-floor fixture is missing; it was not respawned.",
						out Failure);
				if (!cell.IsPassable() || !cell.IsEmptyOfSolid() || cell.HasOpenLiquidVolume())
					return Fail("A paid hosted-floor fixture cell is obstructed.", out Failure);
				Prepared.Add(new PreparedFixture { Spec = spec, Id = id, Cell = cell });
			}
			for (int i = 0; i < Prepared.Count; i++)
			{
				try
				{
					Prepared[i].Output = GameObject.Create(Prepared[i].Spec.Blueprint);
					if (!GameObject.Validate(Prepared[i].Output)
						|| Prepared[i].Output.Blueprint != Prepared[i].Spec.Blueprint
						|| Prepared[i].Output.CurrentCell != null) throw new InvalidOperationException();
					Prepared[i].Output.ID = Prepared[i].Id;
					if (Prepared[i].Output.IDIfAssigned != Prepared[i].Id)
						throw new InvalidOperationException();
				}
				catch
				{
					DiscardPrepared(Prepared);
					return Fail("A paid hosted-floor fixture output failed preflight.", out Failure);
				}
			}
			return true;
		}

		private static bool TryPlacePrepared(Zone Z, string RootId,
			r_KingdomArcologyZoneAnchor Anchor, KingdomArcologyFixtureSpec[] Fixtures,
			List<PreparedFixture> Prepared, out string Failure)
		{
			Failure = null;
			try
			{
				for (int i = 0; i < Prepared.Count; i++)
				{
					PreparedFixture row = Prepared[i];
					GameObject accepted = row.Cell.AddObject(row.Output, Forced: true, System: true,
						NoStack: true, Silent: true);
					if (!ReferenceEquals(row.Output, accepted) || row.Output.CurrentCell != row.Cell)
						throw new InvalidOperationException();
				}
				if (ProvesExactFixtures(Z, RootId, Anchor, Fixtures)) return true;
			}
			catch { }
			bool clean = DiscardPrepared(Prepared);
			return Fail(clean
				? "A paid hosted-floor fixture could not be realized exactly."
				: "A paid hosted-floor fixture failed with ambiguous partial custody.", out Failure);
		}

		internal static bool ProvesExactFixtures(Zone Z, string RootId,
			r_KingdomArcologyZoneAnchor Anchor, KingdomArcologyFixtureSpec[] Fixtures)
		{
			for (int i = 0; i < Fixtures.Length; i++)
			{
				KingdomArcologyFixtureSpec spec = Fixtures[i];
				string id = FixtureId(RootId, Anchor, spec);
				GameObject found = null; int count = 0;
				foreach (GameObject item in Z.GetObjects())
					if (item.IDIfAssigned == id) { found = item; count++; }
				if (count != 1 || found.Blueprint != spec.Blueprint
					|| found.CurrentCell != Z.GetCell(spec.X, spec.Y)) return false;
			}
			return true;
		}

		private static bool DiscardPrepared(List<PreparedFixture> Prepared)
		{
			bool clean = true;
			for (int i = 0; i < Prepared.Count; i++)
			{
				GameObject output = Prepared[i].Output;
				if (!GameObject.Validate(output)) continue;
				try { output.Obliterate(null, Silent: true); }
				catch { clean = false; }
				if (GameObject.Validate(output)) clean = false;
			}
			return clean;
		}

		internal static string FixtureId(string RootId,
			r_KingdomArcologyZoneAnchor Anchor, KingdomArcologyFixtureSpec Spec)
		{
			return KingdomHostedArcologyRules.StableChildId(RootId,
				KingdomHostedArcologyTopology.StableRole(Anchor.ZoneX, Anchor.ZoneY,
					Anchor.ZoneZ, Anchor.LotKey + ":fixture:" + Spec.Role));
		}

		private static bool Quarantine(r_KingdomArcology Root, string Reason)
		{
			KingdomHostedArcology.Quarantine(Root, Reason);
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
