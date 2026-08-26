using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool AllGrowthRowsSettled(GrowthPlan Plan)
		{
			if (Plan?.Rows == null) return false;
			for (int i = 0; i < Plan.Rows.Count; i++)
				if (Plan.Rows[i] == null || Plan.Rows[i].State != 2) return false;
			return true;
		}

		private static bool ExactGrowthEndpoints(GameObject Predecessor,
			GameObject Successor, Cell ExpectedCell, GrowthPlan Plan)
		{
			Zone zone = ExpectedCell?.ParentZone;
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			if (zone == null || !GameObject.Validate(Predecessor)
				|| !GameObject.Validate(Successor) || Predecessor.CurrentCell != ExpectedCell
				|| Successor.CurrentCell != ExpectedCell || Predecessor.CurrentZone != zone
				|| Successor.CurrentZone != zone
				|| Predecessor.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
				|| Successor.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
				|| KingdomConstruction.FindExactId(zone, Predecessor.ID, out exactPredecessor)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactPredecessor, Predecessor)
				|| KingdomConstruction.FindExactId(zone, Successor.ID, out exactSuccessor)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactSuccessor, Successor)) return false;
			if (Plan != null)
			{
				string encoded = EncodeGrowthPlan(Plan);
				if (encoded == null || Predecessor.ID != Plan.PredecessorId
					|| Successor.ID != Plan.SuccessorId
					|| Successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
						!= Plan.SuccessorKey
					|| Predecessor.GetStringProperty(GrowthReceiptProperty) != encoded) return false;
			}
			string receipt = Predecessor.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)) return false;
			KingdomConstructionJob job;
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			return KingdomConstruction.TryFind(receipt, out job)
				&& job.Route == KingdomConstructionRoute.Improvement
				&& job.SubjectId == Predecessor.ID && job.SourceId == Predecessor.ID
				&& job.OutputId == Successor.ID && job.TargetKey == Plan.SuccessorKey
				&& job.X == ExpectedCell.X && job.Y == ExpectedCell.Y
				&& KingdomConstruction.Owns(system, zone, job)
				&& KingdomConstruction.IsCurrent(job)
				&& KingdomConstruction.HasReceipt(Predecessor, job)
				&& KingdomConstruction.HasReceipt(Successor, job);
		}

		private static bool ExactGrowthRemoval(GameObject Item, Zone Z, GrowthRow Row,
			string PlotId)
		{
			return GameObject.Validate(Item) && Item.Physics != null
				&& Item.Physics.InInventory == null && Item.CurrentZone == Z
				&& Item.CurrentCell == Z.GetCell(Row.X, Row.Y) && Item.ID == Row.Id
				&& Item.Blueprint == Row.Blueprint && Item.GetIntProperty(PlotPartProperty) == 1
				&& Item.GetStringProperty(PlotIdProperty) == PlotId
				&& Item.GetIntProperty(KingdomYards.YardWorkProperty) != 1
				&& (Item.IsWall() || Item.IsDoor() || Item.Blueprint == FrameBlueprint)
				&& ReferenceCountInCell(Item.CurrentCell, Item) == 1;
		}

		private static bool ExactGrowthOutput(GameObject Item, Zone Z, GrowthRow Row,
			string PlotId)
		{
			GameObject global;
			if (!GameObject.Validate(Item) || Item.Physics == null
				|| Item.Physics.InInventory != null || Item.CurrentZone != Z
				|| Item.CurrentCell != Z.GetCell(Row.X, Row.Y) || Item.ID != Row.Id
				|| Item.Blueprint != Row.Blueprint || Item.GetIntProperty(PlotPartProperty) != 1
				|| Item.GetStringProperty(PlotIdProperty) != PlotId
				|| ReferenceCountInCell(Item.CurrentCell, Item) != 1
				|| KingdomConstruction.FindExactId(Z, Row.Id, out global)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(global, Item)) return false;
			int sameBlueprint = 0;
			foreach (GameObject candidate in Item.CurrentCell.GetObjects())
				if (GameObject.Validate(candidate) && candidate.Blueprint == Row.Blueprint)
					sameBlueprint++;
			return sameBlueprint == 1;
		}

		private static int ReferenceCountInCell(Cell Cell, GameObject Item)
		{
			if (Cell == null || Item == null) return 0;
			int count = 0;
			foreach (GameObject candidate in Cell.GetObjects())
				if (ReferenceEquals(candidate, Item)) count++;
			return count;
		}

		private static bool GrowthTargetEmpty(Zone Z, GrowthRow Row)
		{
			Cell cell = Z?.GetCell(Row.X, Row.Y);
			if (cell == null) return false;
			foreach (GameObject item in cell.GetObjects())
				if (GameObject.Validate(item) && item.Blueprint == Row.Blueprint) return false;
			return KingdomConstruction.FindExactId(Z, Row.Id, out _)
				== KingdomPhysicalLookupState.Absent;
		}

		private static string GrowthRootKey(GameObject Predecessor, GrowthRow Row)
		{
			if (!BoundedGrowthIdentity(Predecessor?.ID) || !BoundedGrowthIdentity(Row?.Id))
				return null;
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Predecessor.ID + "\n" + Row.Id);
			byte[] digest;
			using (System.Security.Cryptography.SHA256 hash =
				System.Security.Cryptography.SHA256.Create()) digest = hash.ComputeHash(bytes);
			System.Text.StringBuilder key = new System.Text.StringBuilder(GrowthEscrowPrefix, 96);
			for (int i = 0; i < digest.Length; i++) key.Append(digest[i].ToString("x2",
				System.Globalization.CultureInfo.InvariantCulture));
			return key.ToString();
		}

		private static bool RootGrowthOutput(GameObject Predecessor, GrowthRow Row,
			GameObject Output)
		{
			string key = GrowthRootKey(Predecessor, Row);
			object rooted;
			if (The.Game == null || string.IsNullOrEmpty(key) || !GameObject.Validate(Output)
				|| (The.Game.ObjectGameState.TryGetValue(key, out rooted)
					&& !ReferenceEquals(rooted, Output))) return false;
			The.Game.SetObjectGameState(key, Output);
			return The.Game.ObjectGameState.TryGetValue(key, out rooted)
				&& ReferenceEquals(rooted, Output) && Output.ID == Row.Id
				&& Output.Blueprint == Row.Blueprint;
		}

		private static bool TryGrowthRoot(GameObject Predecessor, GrowthRow Row,
			out GameObject Output)
		{
			Output = null;
			string key = GrowthRootKey(Predecessor, Row);
			object rooted;
			if (The.Game == null || string.IsNullOrEmpty(key)
				|| !The.Game.ObjectGameState.TryGetValue(key, out rooted)) return false;
			Output = rooted as GameObject;
			return GameObject.Validate(Output) && Output.ID == Row.Id
				&& Output.Blueprint == Row.Blueprint
				&& Output.GetIntProperty(PlotPartProperty) == 1;
		}

		private static bool RetireSettledGrowthRoot(Zone Z, GameObject Predecessor,
			GrowthRow Row, string PlotId, GameObject Expected)
		{
			string key = GrowthRootKey(Predecessor, Row);
			object rooted;
			if (The.Game == null || string.IsNullOrEmpty(key)) return false;
			if (!The.Game.ObjectGameState.TryGetValue(key, out rooted)) return true;
			GameObject output = rooted as GameObject;
			if ((Expected != null && !ReferenceEquals(Expected, output))
				|| !ExactGrowthOutput(output, Z, Row, PlotId)) return false;
			The.Game.ObjectGameState.Remove(key);
			return !The.Game.ObjectGameState.ContainsKey(key);
		}

		private static bool PublishGrowthPlan(GameObject Predecessor, GrowthPlan Plan)
		{
			string encoded = EncodeGrowthPlan(Plan);
			if (!GameObject.Validate(Predecessor) || encoded == null) return false;
			Predecessor.SetStringProperty(GrowthReceiptProperty, encoded);
			return Predecessor.GetStringProperty(GrowthReceiptProperty) == encoded;
		}

		private static bool GrowthPlanMatches(GrowthPlan Plan, GameObject Predecessor,
			GameObject Successor, string SuccessorKey, string PlotId,
			KingdomPlotRules.PlotRect Old, KingdomPlotRules.PlotRect Grown,
			KingdomPlotRules.RoofState Roof, int HeartX, int HeartY, bool KeepInner,
			string Wall)
		{
			return Plan != null && Plan.PredecessorId == Predecessor.ID
				&& Plan.SuccessorId == Successor.ID && Plan.SuccessorKey == SuccessorKey
				&& Plan.PlotId == PlotId && SameGrowthRect(Plan.Old, Old)
				&& SameGrowthRect(Plan.Grown, Grown) && Plan.Roof == Roof
				&& Plan.HeartX == HeartX && Plan.HeartY == HeartY
				&& Plan.KeepInner == KeepInner && Plan.Wall == (Wall ?? "");
		}

		private static bool SameGrowthRect(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		private static int CompareGrowthRows(GrowthRow A, GrowthRow B)
		{
			int compared = A.Kind.CompareTo(B.Kind);
			if (compared != 0) return compared;
			compared = A.Y.CompareTo(B.Y);
			if (compared != 0) return compared;
			compared = A.X.CompareTo(B.X);
			if (compared != 0) return compared;
			compared = string.CompareOrdinal(A.Blueprint, B.Blueprint);
			return compared != 0 ? compared : string.CompareOrdinal(A.Id, B.Id);
		}

		private static bool BoundedGrowthIdentity(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 128;
		}

		private static bool BoundedGrowthText(string Value, int Maximum)
		{
			return Value == null || Value.Length <= Maximum;
		}

		private static string GrowthText(string Value)
		{
			return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value ?? ""));
		}

	}
}
