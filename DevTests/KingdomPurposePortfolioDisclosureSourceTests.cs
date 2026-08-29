#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Two structural contracts on the portfolio's own consent and status surfaces:
	/// the three portfolio-only <c>PurposeEffect</c> declarations stay reachable there, and the
	/// pair freeze and cargo credit entries — the ones that never pass the operation preflight —
	/// refuse new work before any durable publish.</summary>
	[TestFixture]
	public class KingdomPurposePortfolioDisclosureSourceTests
	{
		private const string OpenPath = "Growth/KingdomPurposePortfolio.Open.cs";
		private const string InteractionPath = "Growth/KingdomPurposePortfolio.Interaction.cs";
		private const string PairingPath = "Growth/KingdomPurposePortfolio.Pairing.cs";
		private const string ControlPath = "Growth/KingdomPurposePortfolio.OperationControl.cs";
		private const string DrivePath = "Growth/KingdomPurposePortfolio.OperationDrive.cs";
		private const string RegistryPath = "Growth/KingdomPurposePortfolio.RuntimeRegistry.cs";
		private const string TransitionPath = "Growth/KingdomPurposePortfolioRules.Transitions.cs";
		private const string EscrowPath = "Growth/KingdomPurpose.03.CargoIdentityAndEscrow.cs";
		private const string CargoRootPath = "Growth/KingdomPurposePortfolio.CargoRoot.cs";
		private const string BuildingsPath = "RuntimeData/KingdomBuildings.xml";

		/// <summary>The shipped root-key prefix, kept honest by
		/// <see cref="ReleasingACreditRootIsIdempotentAndTotal"/>.</summary>
		private const string RootPrefix = "r_TAF_PurposePairCargo:";

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Squash(string text)
		{
			StringBuilder squashed = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
				if (!char.IsWhiteSpace(text[i])) squashed.Append(text[i]);
			return squashed.ToString();
		}

		private static int At(string source, string term)
		{
			int found = source.IndexOf(term, StringComparison.Ordinal);
			Assert.Greater(found, -1, term);
			return found;
		}

		private static int Count(string haystack, string needle)
		{
			int found = 0;
			for (int at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
				at = haystack.IndexOf(needle, at + 1, StringComparison.Ordinal)) found++;
			return found;
		}

		/// <summary>The declared effect prose of one building row, read straight from the
		/// shipped catalogue.</summary>
		private static string DeclaredEffect(string BuildingsXml, string BuildKey)
		{
			int row = At(BuildingsXml, "Key=\"" + BuildKey + "\"");
			int attribute = BuildingsXml.IndexOf("PurposeEffect=\"", row, StringComparison.Ordinal);
			Assert.Greater(attribute, row, BuildKey + " declares no PurposeEffect");
			int start = attribute + "PurposeEffect=\"".Length;
			int end = BuildingsXml.IndexOf('"', start);
			Assert.Greater(end, start, BuildKey);
			return BuildingsXml.Substring(start, end - start);
		}

		[Test]
		public void PortfolioOnlyEffectProseIsReachableOnConsentAndStatus()
		{
			string open = Squash(Read(OpenPath));
			StringAssert.Contains(
				"TryGetDefinition(KingdomPurposePortfolioRules.BuildKey(Kind),outKingdomPurposeDefinitiondefinition)",
				open, "the surfaces reach the declaration through the public definition accessor");
			StringAssert.Contains("+definition.Effect+\".\"", open,
				"the declared prose itself must be rendered, not merely looked up");

			StringAssert.Contains("DeclaredEffect(Recipe.Source)", Squash(Read(InteractionPath)),
				"operation consent must name what the acting work is declared to do");
			string pairing = Squash(Read(PairingPath));
			StringAssert.Contains("DeclaredEffect(FirstKind)", pairing);
			StringAssert.Contains("DeclaredEffect(secondKind)", pairing);
			StringAssert.Contains(
				"+ProvisionState(Pair)+PurposeEffectState(Pair)+DeclaredEffect(acting)", open,
				"portfolio status must name the acting work's declared operation");
		}

		[Test]
		public void ThreeNonBodyDeclarationsCarryProseTheDispatchCatalogueStillFilters()
		{
			StringAssert.Contains("if (!pair.Value.PortfolioOnly) values.Add", Read(EscrowPath),
				"the dispatch catalogue still filters portfolio-only declarations, which is why the portfolio surfaces are the only place their prose can be read");

			string buildings = Read(BuildingsPath);
			KingdomPurposeKind[] kinds =
			{
				KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, KingdomPurposeKind.Harvest
			};
			string[] keys = { "deepbore", "greatfoundry", "realmgranary" };
			for (int i = 0; i < kinds.Length; i++)
			{
				Assert.AreEqual(keys[i], KingdomPurposePortfolioRules.BuildKey(kinds[i]));
				KingdomPurposeKind resolved;
				Assert.IsTrue(KingdomPurposePortfolioRules.TryBuildKind(keys[i], out resolved));
				Assert.AreEqual(kinds[i], resolved);
				string effect = DeclaredEffect(buildings, keys[i]);
				Assert.GreaterOrEqual(effect.Length, 1, keys[i]);
				Assert.LessOrEqual(effect.Length, 360, keys[i]);
				StringAssert.StartsWith("performs", effect, keys[i]);
			}
		}

		[Test]
		public void EveryPurposeKindResolvesToADeclarationTheSurfacesCanRender()
		{
			string buildings = Read(BuildingsPath);
			for (int i = 1; i <= (int)KingdomPurposeKind.Harvest; i++)
			{
				KingdomPurposeKind kind = (KingdomPurposeKind)i;
				string key = KingdomPurposePortfolioRules.BuildKey(kind);
				Assert.IsFalse(string.IsNullOrEmpty(key), kind.ToString());
				Assert.IsFalse(string.IsNullOrEmpty(DeclaredEffect(buildings, key)),
					kind.ToString());
			}
			Assert.IsNull(KingdomPurposePortfolioRules.BuildKey(KingdomPurposeKind.None));
		}

		[Test]
		public void PairFreezeRefusesNewWorkBeforeAnyDurableMutation()
		{
			string pairing = Read(PairingPath);
			int offer = At(pairing, "private static void OfferPair(");
			int gate = At(pairing, "if (!KingdomMaster.NewWorkAllowed(System))");
			Assert.Greater(gate, offer, "the freeze refusal belongs inside OfferPair");
			string[] mutations =
			{
				"Popup.PickOption(", "Popup.ShowYesNo(",
				"KingdomPurposePortfolioRules.TryCreatePair(",
				"TryPublishPortfolioPair(", "TryReplaceDormantPair("
			};
			for (int i = 0; i < mutations.Length; i++)
				Assert.Less(gate, At(pairing, mutations[i]),
					"the pause refusal must precede " + mutations[i]);
			Assert.AreEqual(1, Count(pairing, "KingdomMaster.NewWorkAllowed"),
				"one refusal governs the freeze path");
		}

		[Test]
		public void CounterHeadroomPrecedesOperationCleanupAndEpochArithmetic()
		{
			string control = Read(ControlPath);
			Assert.Less(At(control, "CanStartOperationAtRevision("),
				At(control, "TryRetireCreditedPurposeCargo(Pair.Operation)"));
			string drive = Read(DrivePath);
			Assert.Less(At(drive, "CanStartOperationAtRevision("),
				At(drive, "TryRetireCreditedPurposeCargo(Pair.Operation)"));
			string transitions = Read(TransitionPath);
			StringAssert.Contains("Before.NextOperationOrdinal != int.MaxValue",
				transitions);

			string pairing = Read(PairingPath);
			Assert.Less(At(pairing, "Dormant.Epoch == long.MaxValue"),
				At(pairing, "Dormant.Epoch + 1L"));
			string registry = Read(RegistryPath);
			Assert.Less(At(registry, "Dormant.Epoch == long.MaxValue"),
				At(registry, "Dormant.Epoch + 1L"));
		}

		/// <summary>Replica of one root-table value: the object identity the entry roots, the cargo
		/// receipt that identity reproves, and whether the object is still alive. A value that is
		/// none of these stands for the entry production also refuses to touch.</summary>
		private sealed class RootedValue
		{
			internal string ObjectId;
			internal string Receipt;
			internal bool Alive;
		}

		/// <summary>Replica of the consumed cargo receipt a dissolution releases: the tuple that
		/// names both root keys, the canonical encoded body, and the exact identity plus receipt
		/// encoding a rooted value has to reprove before its key may go.</summary>
		private sealed class CargoStub
		{
			internal string PairId;
			internal long PairEpoch;
			internal string OperationId;
			internal string Body;
			internal string ObjectId;
			internal string Receipt;
		}

		/// <summary>Replica of the two root keys a dissolution offers up: the canonical encoded
		/// form and the pre-correction delimiter join, its epoch in invariant digits so the key one
		/// machine wrote is the key another reads back. Neither is deleted blind — the legacy form
		/// shares one namespace with every other pair/epoch/operation tuple. Production's own
		/// wiring of this is pinned once, by the landing suite's
		/// <c>ARootIsRemovedOnlyWhenItsValueReprovesThisExactConsumedCargo</c>.</summary>
		private static void ReleaseRoots(Dictionary<string, object> Roots, CargoStub Cargo)
		{
			if (Cargo == null) return;
			ReleaseRoot(Roots, RootPrefix + Cargo.Body, Cargo);
			ReleaseRoot(Roots, RootPrefix + Cargo.PairId + ":"
				+ Cargo.PairEpoch.ToString(CultureInfo.InvariantCulture) + ":"
				+ Cargo.OperationId, Cargo);
		}

		/// <summary>One checked release: the key goes only when the value under it is the object
		/// this receipt names — alive and reproving its whole receipt, or the dead remains of that
		/// same identity, which leave nothing but a stale key. The decision itself is the shipped
		/// rule, so this replica cannot drift from the law production applies.</summary>
		private static void ReleaseRoot(Dictionary<string, object> Roots, string Key,
			CargoStub Cargo)
		{
			if (!Roots.TryGetValue(Key, out object value)) return;
			RootedValue rooted = value as RootedValue;
			if (!KingdomPurposePortfolioRules.RootEntryIsRetirable(rooted != null,
				rooted != null && rooted.ObjectId == Cargo.ObjectId,
				rooted != null && rooted.Alive,
				rooted != null && rooted.Receipt == Cargo.Receipt)) return;
			Roots.Remove(Key);
		}

		private static CargoStub Credit()
		{
			return new CargoStub
			{
				PairId = "pair", PairEpoch = 3L, OperationId = "op", Body = "body",
				ObjectId = "cargo-1", Receipt = "receipt-1"
			};
		}

		private static Dictionary<string, object> NewRoots()
		{
			return new Dictionary<string, object>(StringComparer.Ordinal);
		}

		private static RootedValue Rooted(string ObjectId, string Receipt)
		{
			return new RootedValue { ObjectId = ObjectId, Receipt = Receipt, Alive = true };
		}

		[Test]
		public void DissolutionReleasesTheCreditRootBeforeItForgetsWhatToRelease()
		{
			string interaction = Read(InteractionPath);
			int dissolve = At(interaction, "private static void DissolvePair(");
			int release = At(interaction, "RemovePurposeCargoRoots(credit)");
			int guard = At(interaction, "Pair.Revision == int.MaxValue");
			Assert.Greater(release, dissolve);
			Assert.Less(guard, release,
				"counter exhaustion refuses before any physical root disposition");
			Assert.Less(release, At(interaction, "dormant.CreditCargoId = null;"),
				"the receipt still has to name the cargo when the root is released");
			Assert.Less(release, At(interaction, "TryPublishPortfolioPair(Pair, dormant"),
				"releasing after a successful publish would strand the entry beyond recovery: the dormant receipt no longer names the cargo");
			Assert.Less(release, At(interaction,
				"KingdomGovernanceScope.Commit(\"dissolve purpose pair\")"));
			Assert.Greater(release, At(interaction, "Popup.ShowYesNo(\"Dissolve this pair"),
				"a declined confirmation releases nothing");
			StringAssert.Contains("TryDecodeCargo(Pair.CreditCargoReceipt", interaction,
				"a pair holding no credit cargo decodes nothing and releases nothing");
		}

		[Test]
		public void DissolutionRetiresRootsThroughTheOneSharedCheckedApi()
		{
			// The removal law itself — value-checked deletion of the canonical and the legacy key —
			// is the landing lane's, pinned once by KingdomPurposeFoodLandingSourceTests
			// .ARootIsRemovedOnlyWhenItsValueReprovesThisExactConsumedCargo. What belongs here is
			// only that dissolution reaches that seam instead of formatting or deleting a key of
			// its own.
			StringAssert.Contains(
				"internal static void RemovePurposeCargoRoots(KingdomPurposeCargoReceipt Cargo)",
				Read(CargoRootPath), "the shared retirement API dissolution calls must exist");
			string interaction = Read(InteractionPath);
			StringAssert.DoesNotContain("ObjectGameState", interaction,
				"dissolution retires through the shared API rather than reaching into the root table");
			StringAssert.DoesNotContain("PortfolioCargoRootPrefix", interaction,
				"and never rebuilds a root key format of its own, which could only drift");
			Assert.AreEqual(1, Count(interaction, "RemovePurposeCargoRoots("),
				"one disposition, at the one dissolution");
			StringAssert.Contains("old cargo remains a physical but inert token", interaction,
				"without-refund dissolution discloses the inert physical token");
			StringAssert.DoesNotContain("RemoveIntProperty(CargoSchemaProperty)", interaction,
				"dissolution must not silently book old cargo into ordinary civic stock");
			StringAssert.DoesNotContain("RemoveIntProperty(PortfolioCargoSchemaProperty)",
				interaction, "a later epoch never reinterprets a reciprocal token as material");
		}

		[Test]
		public void ReleasingACreditRootIsIdempotentAndTotal()
		{
			string cargoRoot = Read(CargoRootPath);
			int release = At(cargoRoot, "internal static void RemovePurposeCargoRoots(");
			string body = cargoRoot.Substring(release,
				cargoRoot.IndexOf("\n\t\t}", release, StringComparison.Ordinal) - release);
			StringAssert.Contains("Cargo == null", body,
				"a pair with no credit cargo releases nothing");
			StringAssert.DoesNotContain("ObjectGameState.Remove", body,
				"the fan-out deletes nothing itself; both keys are offered to the one checked seam");
			StringAssert.DoesNotContain("Popup", body, "disposition reports no fault of its own");
			StringAssert.Contains(
				"private const string PortfolioCargoRootPrefix = \"" + RootPrefix + "\";",
				Read(ControlPath), "the replica below builds its keys from the shipped prefix");

			// Mutant: double dissolve. Removing twice must leave the same state and not throw.
			CargoStub credit = Credit();
			Dictionary<string, object> roots = NewRoots();
			roots[RootPrefix + "body"] = Rooted("cargo-1", "receipt-1");
			roots[RootPrefix + "pair:3:op"] = Rooted("cargo-1", "receipt-1");
			roots[RootPrefix + "someone-else"] = Rooted("cargo-2", "receipt-2");
			ReleaseRoots(roots, credit);
			Assert.AreEqual(1, roots.Count);
			ReleaseRoots(roots, credit);
			Assert.AreEqual(1, roots.Count, "a second dissolution is a no-op");
			Assert.IsTrue(roots.ContainsKey(RootPrefix + "someone-else"),
				"disposition is exact: no other pair's root is touched");

			// Mutant: crash between release and CAS. Releasing again re-converges.
			Dictionary<string, object> crashed = NewRoots();
			crashed[RootPrefix + "body"] = Rooted("cargo-1", "receipt-1");
			ReleaseRoots(crashed, credit);
			ReleaseRoots(crashed, credit);
			Assert.AreEqual(0, crashed.Count, "no orphaned root key survives dissolution");

			// Mutant: the rooted object died before the dissolution. Its dead remains reprove no
			// receipt at all, and leave nothing behind but a stale key, so the key still goes.
			Dictionary<string, object> dead = NewRoots();
			dead[RootPrefix + "body"] = new RootedValue
			{
				ObjectId = "cargo-1", Receipt = null, Alive = false
			};
			ReleaseRoots(dead, credit);
			Assert.AreEqual(0, dead.Count,
				"the dead remains of this same cargo leave no root behind");
		}

		[Test]
		public void DissolutionPreservesForeignValueAtCollidingLegacyKey()
		{
			// An assigned id admits ':', so the pre-correction delimiter join can be named by a
			// second pair/epoch/operation tuple. The canonical key is this cargo's alone; the
			// legacy key may be somebody else's, and a dissolution deleting it blind would drop a
			// live root another operation still needs to find its own cargo.
			CargoStub credit = Credit();
			Dictionary<string, object> roots = NewRoots();
			roots[RootPrefix + "body"] = Rooted("cargo-1", "receipt-1");
			roots[RootPrefix + "pair:3:op"] = Rooted("cargo-9", "receipt-9");
			ReleaseRoots(roots, credit);
			Assert.AreEqual(1, roots.Count, "the canonical root is still released");
			Assert.IsFalse(roots.ContainsKey(RootPrefix + "body"));
			RootedValue survivor = roots[RootPrefix + "pair:3:op"] as RootedValue;
			Assert.IsNotNull(survivor, "the colliding foreign entry survives as itself");
			Assert.AreEqual("cargo-9", survivor.ObjectId,
				"another operation's live root is not this dissolution's to delete");

			// Same identity, different receipt: a half-bound or re-encoded value is interference,
			// not this cargo, and survives to be quarantined rather than silently dropped.
			Dictionary<string, object> torn = NewRoots();
			torn[RootPrefix + "body"] = Rooted("cargo-1", "receipt-torn");
			ReleaseRoots(torn, credit);
			Assert.AreEqual(1, torn.Count,
				"a live value that does not reprove the whole receipt is not retired");

			// A value that is no rooted object at all belongs to whoever wrote it.
			Dictionary<string, object> foreign = NewRoots();
			foreign[RootPrefix + "body"] = "not a rooted object";
			ReleaseRoots(foreign, credit);
			Assert.AreEqual(1, foreign.Count,
				"a non-object entry is another owner's, and survives");
		}

		[Test]
		public void PairFreezeCommitsExactlyOnceAndOnlyAfterPublication()
		{
			string pairing = Read(PairingPath);
			Assert.AreEqual(1, Count(pairing, "KingdomGovernanceScope.Commit("),
				"one freeze, one commit — the family's leaf convention (r_KingdomPurposeWork.cs:30 opens the scope)");
			int commit = At(pairing, "KingdomGovernanceScope.Commit(\"freeze purpose pair\")");
			int publish = At(pairing, "bool published = Dormant == null");
			Assert.Greater(commit, publish, "nothing is committed before publication is attempted");
			StringAssert.Contains(
				"if(!published)Popup.Show(failure);elseKingdomGovernanceScope.Commit(\"freezepurposepair\");",
				Squash(pairing), "a refused CAS publication reports and commits nothing");

			// Every abandonment before publication must leave the scope uncommitted.
			foreach (string exit in new[] { "if (!KingdomMaster.NewWorkAllowed(System))",
				"if (picked < 0) return;",
				"if (Popup.ShowYesNo(prompt) != DialogResult.Yes) return;" })
				Assert.Less(At(pairing, exit), commit, exit + " precedes the only commit");
		}

		[Test]
		public void PairFreezeRefusalConsumesNoBootstrapOrReturnBit()
		{
			string pairing = Read(PairingPath);
			StringAssert.DoesNotContain("BootstrapUsed", pairing,
				"a refused freeze may not spend the bootstrap bit");
			StringAssert.DoesNotContain("ReturnUsed", pairing,
				"a refused freeze may not spend the return bit");
			int gate = At(pairing, "if (!KingdomMaster.NewWorkAllowed(System))");
			int returned = pairing.IndexOf("return;", gate, StringComparison.Ordinal);
			Assert.Greater(returned, gate, "the refusal returns rather than falling through");
			Assert.Less(returned, At(pairing, "Popup.PickOption("));
		}

		[Test]
		public void CommittedCargoCreditStaysAvailableWhileTheRealmIsPaused()
		{
			string interaction = Read(InteractionPath);
			Assert.AreEqual(0, Count(interaction, "KingdomMaster.NewWorkAllowed"),
				"the delivered cargo has already arrived; crediting it completes committed work and must survive a pause");
			int offer = At(interaction, "private static void OfferCredit(");
			int credit = At(interaction, "AcceptPortfolioCredit(");
			Assert.AreEqual(-1, interaction.IndexOf("NewWorkAllowed", offer,
				StringComparison.Ordinal), "no refusal may stand between the menu and the credit");
			Assert.Greater(credit, offer);
		}

		[Test]
		public void NeitherCreditPathBillsADisabledSpanAsWork()
		{
			string[] clocks =
			{
				"TimeTicks", "ElapsedDays", "AdvanceCheckpoint", "MasterOptionTick", "LastDrawTick"
			};
			foreach (string path in new[] { InteractionPath, DrivePath, OpenPath, PairingPath })
			{
				string source = Read(path);
				for (int i = 0; i < clocks.Length; i++)
					StringAssert.DoesNotContain(clocks[i], source,
						path + " acquired a clock; an ungated credit could then turn paused time into work");
			}
		}

		[Test]
		public void BrandNewWorkIsStillRefusedOnTheCreditSurface()
		{
			string drive = Read(DrivePath);
			int activation = At(drive, "KingdomPurposePortfolioRules.TryCreateOperation(");
			int preflight = drive.IndexOf("TryPortfolioOperationPreflight(", activation,
				StringComparison.Ordinal);
			Assert.Greater(preflight, activation,
				"a brand-new activating operation is preflighted");
			Assert.Less(preflight, drive.IndexOf("TryPublishPortfolioPair(Pair, activating",
				StringComparison.Ordinal), "and preflighted before it publishes");
			StringAssert.Contains(
				"!KingdomPurposePortfolioRules.OperationPhaseIsCommitted(operation.Phase)&&!KingdomMaster.NewWorkAllowed(System)",
				Squash(drive),
				"the drive still refuses to advance work that is not yet committed while paused");
		}

		[Test]
		public void OperationStartAndActivationStillFlowThroughPreflight()
		{
			string control = Read(ControlPath);
			Assert.Less(At(control, "TryPortfolioOperationPreflight("),
				At(control, "TryPublishPortfolioPair("),
				"a started operation is preflighted before it is published");
			string drive = Read(DrivePath);
			Assert.Less(At(drive, "TryPortfolioOperationPreflight("),
				At(drive, "TryPublishPortfolioPair(Pair, activating"),
				"an activating credit is preflighted before it is published");
			StringAssert.Contains("KingdomMaster.NewWorkAllowed(System)", drive,
				"the drive keeps a refusal of its own; without it nothing would gate an uncommitted operation");
			StringAssert.Contains("OperationPhaseIsCommitted(", drive,
				"and that refusal exempts committed phases, so a committed landing stays resumable while paused");
		}
	}
}
#endif
