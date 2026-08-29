#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source contracts for the engine-touching halves of the realized capture and the durable-key
	/// presence law.
	/// <para>
	/// These cannot execute: both read a live zone or a live <c>XRLGame</c>. What they can do is
	/// hold the two facts a pure test cannot see - that the runtime asks the engine the RIGHT
	/// question, and that it hands the answer to the pure classifier instead of deciding for itself.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomRealizedCaptureSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		/// <summary>
		/// One named region of a file. A whole-file search is the defect these contracts keep
		/// catching: the name appears somewhere and the code that runs omits it.
		/// </summary>
		private static string Section(string source, string start, string end)
		{
			int begin = source.IndexOf(start, StringComparison.Ordinal);
			Assert.Greater(begin, -1, start);
			int stop = source.IndexOf(end, begin + start.Length, StringComparison.Ordinal);
			if (stop < 0) stop = source.Length;
			Assert.Greater(stop, begin, end);
			return source.Substring(begin, stop - begin);
		}

		private static string Statement(string source, string start)
		{
			int begin = source.IndexOf(start, StringComparison.Ordinal);
			Assert.Greater(begin, -1, start);
			int end = source.IndexOf(';', begin);
			Assert.Greater(end, begin, start);
			return source.Substring(begin, end - begin).Replace("\t", "").Replace("\r", "")
				.Replace("\n", "").Replace(" ", "");
		}

		// ----- RED 6: architecture facts, never aggregate cell state -----------------------------

		/// <summary>
		/// The engine's own cell predicates scan every object standing in the cell, so a resident or
		/// a puddle would move a digest that claims to measure architecture - and would contradict
		/// this same reader's exclusion of that resident.
		/// </summary>
		[Test]
		public void TheRealizedCaptureNeverConsultsAggregateCellState()
		{
			foreach (string shard in new string[]
				{ "Core/KingdomRealizedArchitectureCapture.cs",
					"Core/KingdomRealizedArchitectureCapture.Objects.cs",
					"Core/KingdomRealizedArchitectureCapture.Authority.cs",
					"Core/KingdomRealizedArchitectureCapture.Facts.cs" })
			{
				string source = Read(shard);
				foreach (string forbidden in new string[]
					{ "IsPassable()", "HasOpenLiquidVolume()", "HasObjectWithPart",
						"Cell.GetObjects()" })
					StringAssert.DoesNotContain(forbidden, source, shard);
			}
		}

		/// <summary>Cell rows are derived from the measured object facts, not from the zone.</summary>
		[Test]
		public void CellFactsAreDerivedFromTheMeasuredObjects()
		{
			string capture = Read("Core/KingdomRealizedArchitectureCapture.cs");
			StringAssert.Contains(
				"private static List<KingdomRealizedCellFact> Cells(int Width, int Height,", capture);
			StringAssert.Contains("IList<KingdomRealizedObjectFact> Objects)", capture);
		}

		/// <summary>The realized capture is read-only: it must never write or create.</summary>
		[Test]
		public void RealizedCaptureIsReadOnly()
		{
			foreach (string shard in new string[]
				{ "Core/KingdomRealizedArchitectureCapture.cs",
					"Core/KingdomRealizedArchitectureCapture.Objects.cs",
					"Core/KingdomRealizedArchitectureCapture.Authority.cs",
					"Core/KingdomRealizedArchitectureCapture.Facts.cs" })
			{
				string source = Read(shard);
				foreach (string forbidden in new string[]
					{ "SetStringProperty", "SetIntProperty", "RemoveIntProperty",
						"RemoveStringProperty", "GameObject.Create", "AddObject", "Destroy(" })
					StringAssert.DoesNotContain(forbidden, source, shard);
			}
		}

		// ----- RED 6/8: refusal rather than narrowing --------------------------------------------

		/// <summary>
		/// Every damaged authority state is a refusal by name. Skipping one would narrow the measured
		/// world until a damaged lot quietly matched an intact one.
		/// </summary>
		[Test]
		public void DamagedComponentAuthorityRefusesRatherThanNarrowing()
		{
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("has moved off its exact rotated coordinate", objects);
			StringAssert.Contains("two authored slots name the same object id", objects);
			StringAssert.Contains("has not finished staging", objects);
			StringAssert.Contains("is absent, ambiguous, ", objects);
			StringAssert.Contains("carries a partial component marking", authority);
			StringAssert.Contains("upgrade-retention marker in a ", authority);
			StringAssert.Contains("under the string table", authority);
			StringAssert.Contains("under the int table", authority);
			StringAssert.Contains("carries foreign component authority", authority);
			// The unreceipted verdicts are the pure census's; their own reasons live with it, and
			// their mutants execute in KingdomRealizedAuthorityShapeTests.
			string shape = Read("Core/KingdomRealizedAuthorityShape.cs");
			StringAssert.Contains("a second architecture owner carries this lot id", shape);
			StringAssert.Contains("carries this lot's snapshot authority", shape);
			StringAssert.Contains("is named by no owner receipt", shape);
			StringAssert.Contains("under the wrong durable type table", shape);
		}

		/// <summary>
		/// The census is driven by the owner's frozen receipts. A hash and a token are values on an
		/// object; whatever can write a property can write both, so only the owner's own output id,
		/// recomputed token, and rotated coordinate bind a component to the layout claiming it.
		/// </summary>
		[Test]
		public void ComponentAuthorityIsReprovedFromTheOwnerReceipts()
		{
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			foreach (string required in new string[]
				{ "Snapshot.Placements", "OutputStateProperty(Placement)",
					"OutputIdProperty(Placement)", "KingdomConstruction.FindExactId",
					"KingdomPhysicalLookupState.Exact",
					"KingdomArchitectureRuntime.TryWorldPlacement", "TryExactAuthority" })
				StringAssert.Contains(required, objects);
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("ComponentToken(Lot, Intent.SnapshotHash, Placement)", authority);
			StringAssert.Contains("does not recompute", authority);
		}

		/// <summary>
		/// Every stamper marker is in the exactness audit, checked inside the helper that runs it
		/// rather than anywhere in the file. A whole-file search passes while the operative predicate
		/// omits a marker, which is how plot-part custody went missing from the census once already.
		/// </summary>
		[Test]
		public void EveryComponentMarkerIsInTheOperativeExactnessAudit()
		{
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			string audit = Section(authority, "private static readonly string[] AuditedIntKeys",
				"/// <summary>");
			foreach (string marker in new string[]
				{ "ComponentSchemaProperty", "ComponentLayerProperty", "ComponentExistingProperty",
					"ComponentCarriedProperty", "PlotPartProperty", "ComponentSlotProperty",
					"ComponentAnchorProperty", "ComponentHashProperty", "ComponentTokenProperty",
					"PlotIdProperty" })
				StringAssert.Contains(marker, audit, marker);
		}

		/// <summary>
		/// The upgrade-retention marker is proved by the pure classifier and then omitted from the
		/// digest. It is provenance, not identity: the shipped same-lot upgrade path writes it on
		/// every retained placement and never removes it, so a build that reached its final shape by
		/// upgrade must compare alike with one realized fresh.
		/// </summary>
		[Test]
		public void TheCarriedMarkerIsProvedAndThenLeftOutOfTheDigest()
		{
			string authority = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			StringAssert.Contains("KingdomRealizedAuthorityShape.Carried(", authority);
			StringAssert.Contains("ComponentCarriedProperty", authority);
			string facts = Read("Core/KingdomRealizedCaptureFacts.cs");
			StringAssert.DoesNotContain("Carried", facts);
			string capture = Read("Core/KingdomRealizedArchitectureCapture.Facts.cs");
			StringAssert.DoesNotContain("Carried", capture);
			// It stays in the unreceipted census: stray retention authority is still a claim.
			StringAssert.Contains("ComponentCarriedProperty", authority);
		}

		/// <summary>
		/// The unreceipted census is the PURE predicate, whose own mutants execute in
		/// KingdomRealizedAuthorityShapeTests. A runtime that judged for itself could omit a marker
		/// while every source string still read correctly.
		/// </summary>
		[Test]
		public void TheUnreceiptedCensusDelegatesToThePureJudge()
		{
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			StringAssert.Contains("KingdomRealizedAuthorityShape.Judge(", objects);
			StringAssert.Contains("KingdomRealizedAuthorityShape.Describe(verdict)", objects);
			string observe = Section(objects,
				"private static KingdomRealizedMarkerObservation Observe(", "private static bool Marked");
			foreach (string required in new string[]
				{ "PlotPart = Item.HasIntProperty(KingdomPlots.PlotPartProperty)",
					"PlotIdString = Item.HasStringProperty(KingdomPlots.PlotIdProperty)",
					"PlotIdInt = Item.HasIntProperty(KingdomPlots.PlotIdProperty)",
					"InsideRect = " })
				StringAssert.Contains(required, observe, required);
		}

		/// <summary>The owner's own top-level layout authority is proved by exact type presence.</summary>
		[Test]
		public void TopLevelLayoutAuthorityIsProvedByExactTypePresenceAndValue()
		{
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			string proof = Section(objects, "private static bool TryProveOwnerAuthority(",
				"private static bool ExactInt");
			foreach (string required in new string[]
				{ "ExactInt(Owner, KingdomArchitectureStamper.SchemaProperty)",
					"ExactText(Owner, KingdomArchitectureStamper.LotIdProperty)",
					"ExactText(Owner, KingdomArchitectureStamper.HashProperty)",
					"ExactText(Owner, KingdomPlots.PlotIdProperty)",
					"ExactInt(Owner, KingdomArchitectureStamper.NextLayerProperty)",
					"!= CompleteStage" })
				StringAssert.Contains(required, proof, required);
			// Type is not custody: the owner's plot id must also EQUAL the lot every component
			// receipt below is keyed to, or another lot's ground enters this digest.
			StringAssert.Contains("Owner.GetStringProperty(KingdomPlots.PlotIdProperty), Lot",
				proof);
			StringAssert.Contains("names a different lot than its layout", proof);
			StringAssert.Contains("TryProveOwnerAuthority(Owner, Lot, out Failure)", objects);
			StringAssert.Contains(
				"return Item.HasIntProperty(Property) && !Item.HasStringProperty(Property);", objects);
			StringAssert.Contains(
				"return Item.HasStringProperty(Property) && !Item.HasIntProperty(Property);", objects);
		}

		/// <summary>
		/// Solidity is read off the LIVE part. A blueprint-only read digests a component whose
		/// Physics was stripped exactly like the intact one.
		/// </summary>
		[Test]
		public void SolidityIsReadFromTheLiveComponent()
		{
			string facts = Read("Core/KingdomRealizedArchitectureCapture.Facts.cs");
			StringAssert.Contains("Physics physics = Item.GetPart<Physics>();", facts);
			StringAssert.Contains("PhysicsPresent = physics != null,", facts);
			StringAssert.Contains("Solid = physics != null && physics.Solid,", facts);
			StringAssert.Contains("BlueprintSolid = blueprint != null", facts);
			string rules = Read("Core/KingdomRealizedCaptureRules.cs");
			StringAssert.Contains("Flag(Item.PhysicsPresent)", rules);
			StringAssert.Contains("Flag(Item.BlueprintSolid)", rules);
		}

		/// <summary>
		/// The recomputed token preimage and the per-slot property spelling mirror the stamper's own
		/// private helpers. Pinned here so a change on either side fails loudly rather than silently
		/// accepting every stored token or auditing the wrong properties.
		/// </summary>
		[Test]
		public void TheRecomputedTokenMirrorsTheProductionPreimage()
		{
			string production = Read("Growth/KingdomArchitectureStamper.Recovery.cs");
			string mirror = Read("Core/KingdomRealizedArchitectureCapture.Authority.cs");
			Assert.AreEqual(Statement(production, "string preimage ="),
				Statement(mirror, "string preimage ="),
				"the component-token preimage drifted from the stamper's own");
			string objects = Read("Core/KingdomRealizedArchitectureCapture.Objects.cs");
			Assert.AreEqual(Statement(production, "return Slot == null ?"),
				Statement(objects, "return Slot == null ?"),
				"the per-slot property spelling drifted from the stamper's own");
			StringAssert.Contains("KingdomArchitectureStamper.OutputIdPrefix", objects);
			StringAssert.Contains("KingdomArchitectureStamper.OutputStatePrefix", objects);
		}

	}
}
#endif
