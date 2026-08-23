#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Residents as rows: the dead/abroad vocabulary, the brink windows the property bag used to
	/// hold, and the equation between the roll and the binding registry.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §8.3. <b>W2 ships the rows and the binding but not the
	/// placement</b>, so every test here is about what a row SAYS; none of them moves a body.
	/// </para>
	/// </summary>
	// Internal, not public: the parameterised cases are keyed on the model's own vocabulary
	// (KingdomBodyWitness, KingdomResidentStanding, KingdomStandingCause), which is internal to the
	// simulation slice. Widening those enums to satisfy a test would be the test choosing the
	// production API, so the test narrows instead. TestMain reflects over every type it finds.
	internal class KingdomResidentRulesTests
	{
		private const string Here = "taf:zone:here";

		[TestCase(true, false, true, false, KingdomAccessionCarrierState.Original)]
		[TestCase(false, true, false, true, KingdomAccessionCarrierState.Committed)]
		[TestCase(false, true, true, false, KingdomAccessionCarrierState.CityAdvanced)]
		[TestCase(true, false, false, true, KingdomAccessionCarrierState.BindingAdvanced)]
		[TestCase(true, true, true, true, KingdomAccessionCarrierState.Unknown)]
		[TestCase(false, false, false, false, KingdomAccessionCarrierState.Unknown)]
		public void AccessionCarrierClassifierNamesOnlyExactStates(bool cityOriginal,
			bool cityAdvanced, bool bindingOriginal, bool bindingAdvanced,
			KingdomAccessionCarrierState expected)
		{
			Assert.AreEqual(expected, KingdomResidentRules.AccessionCarriers(cityOriginal,
				cityAdvanced, bindingOriginal, bindingAdvanced));
		}

		[Test]
		public void TornNonResidentCityColumnsCannotProveAccessionOriginalOrCommitted()
		{
			KingdomResidentRow resident = Settler(7, KingdomResidentStanding.Resident,
				KingdomStandingCause.None);
			KingdomWorkRow work = new KingdomWorkRow(11, Here, 4, 9, "r_KingdomTent",
				62, 2, 700L, new KingdomWorkRunState(KingdomWorkKind.Other, 0, 0, 0L));
			KingdomCityState original = City(new[] { work }, new[] { resident });
			KingdomCityState advanced;
			KingdomCityFault fault;
			Assert.IsTrue(original.TryWithResidents(new KingdomResidentRow[0], out advanced,
				out fault), fault.ToString());
			KingdomCityState torn = City(new KingdomWorkRow[0], new[] { resident });

			Assert.IsFalse(KingdomResidentRules.SameCity(torn, original),
				"matching resident rows cannot hide a torn work column");
			Assert.IsFalse(KingdomResidentRules.SameCity(torn, advanced));
			Assert.AreEqual(KingdomAccessionCarrierState.Unknown,
				KingdomResidentRules.AccessionCarriers(
					KingdomResidentRules.SameCity(torn, original),
					KingdomResidentRules.SameCity(torn, advanced), true, false));
		}

		private static KingdomResidentRow Settler(int id, KingdomResidentStanding standing, KingdomStandingCause cause)
		{
			return new KingdomResidentRow(id, "Ptoh-" + id, 2, 3, 400L, 0, 0, 0, KingdomDayShape.Hearth,
				standing, cause, Here, KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}

		private static KingdomCityState Book(params KingdomResidentRow[] rows)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, default(KingdomStocks), null, null, rows, null, out state, out fault), fault.ToString());
			return state;
		}

		private static KingdomCityState City(KingdomWorkRow[] works,
			KingdomResidentRow[] residents)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, "taf:city:kavvat", 900L,
				default(KingdomStocks), null, works, residents, null, out state, out fault),
				fault.ToString());
			return state;
		}

		private static KingdomBindingTable BoundTo(params int[] ids)
		{
			KingdomBindingTable table = KingdomBindingTable.Empty;
			KingdomCityFault fault;
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomBindingTable next;
				Assert.IsTrue(table.TryBind(ids[i], KingdomBindingKind.Resident, Here, "obj-" + ids[i], 700L, out next, out fault), fault.ToString());
				table = next;
			}
			return table;
		}

		// ---- The vocabulary ------------------------------------------------------------------

		/// <summary>
		/// The four witnesses, and what each does to a row. A body the founder is leading reads
		/// Abroad even though it is standing right there — that IS the case §8.3 was written for.
		/// </summary>
		[TestCase(KingdomBodyWitness.Present, KingdomResidentStanding.Resident, KingdomStandingCause.None)]
		[TestCase(KingdomBodyWitness.Led, KingdomResidentStanding.Abroad, KingdomStandingCause.Followed)]
		[TestCase(KingdomBodyWitness.Missing, KingdomResidentStanding.Abroad, KingdomStandingCause.Astray)]
		[TestCase(KingdomBodyWitness.Killed, KingdomResidentStanding.Dead, KingdomStandingCause.Raid)]
		public void EachWitnessMovesTheRowWhereTheDesignSaysItDoes(KingdomBodyWitness witness, KingdomResidentStanding expected, KingdomStandingCause cause)
		{
			KingdomResidentRow next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomResidentRules.TryTransition(Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				witness, cause, out next, out fault), fault.ToString());
			Assert.AreEqual(expected, next.Standing);
			Assert.AreEqual(cause, next.Cause);
		}

		/// <summary>A person who was away and is standing here again is home, and not partly away:
		/// the cause is cleared with the standing.</summary>
		[Test]
		public void ComingHomeClearsTheReasonForHavingBeenAway()
		{
			KingdomResidentRow next;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomResidentRules.TryTransition(Settler(7, KingdomResidentStanding.Abroad, KingdomStandingCause.Followed),
				KingdomBodyWitness.Present, KingdomStandingCause.None, out next, out fault));
			Assert.AreEqual(KingdomResidentStanding.Resident, next.Standing);
			Assert.AreEqual(KingdomStandingCause.None, next.Cause);
		}

		/// <summary>
		/// Dead is terminal. A dead row never transitions again, whatever the ground says next: the
		/// id is spent, and a model that silently resurrected somebody would be answering a witness
		/// nobody could have produced honestly.
		/// </summary>
		[TestCase(KingdomBodyWitness.Present)]
		[TestCase(KingdomBodyWitness.Led)]
		[TestCase(KingdomBodyWitness.Missing)]
		[TestCase(KingdomBodyWitness.Killed)]
		public void ADeadRowNeverTransitionsAgain(KingdomBodyWitness witness)
		{
			KingdomResidentRow dead = Settler(7, KingdomResidentStanding.Dead, KingdomStandingCause.Founder);
			KingdomResidentRow next;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomResidentRules.TryTransition(dead, witness, KingdomStandingCause.Raid, out next, out fault));
			Assert.AreEqual(KingdomCityFault.TerminalStanding, fault);
			Assert.AreEqual(KingdomResidentStanding.Dead, next.Standing);
			Assert.AreEqual(KingdomStandingCause.Founder, next.Cause, "a refused transition leaves the row byte-identical");
		}

		/// <summary>A death with no cause is refused rather than stored as an absence or as
		/// nothing. KingdomOfficeRules already classifies every death the engine reports, so a
		/// caller with no cause has not looked.</summary>
		[TestCase(KingdomStandingCause.None)]
		[TestCase(KingdomStandingCause.Followed)]
		[TestCase(KingdomStandingCause.Astray)]
		public void ADeathWithoutADeathCauseIsRefused(KingdomStandingCause cause)
		{
			KingdomResidentRow next;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomResidentRules.TryTransition(Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				KingdomBodyWitness.Killed, cause, out next, out fault));
			Assert.AreEqual(KingdomCityFault.CauseRequired, fault);
			Assert.AreEqual(KingdomResidentStanding.Resident, next.Standing);
		}

		/// <summary>Every cause belongs to exactly one standing's family. A row that said a living
		/// settler died defending the stores would be worse than a row that said nothing.</summary>
		[TestCase(KingdomResidentStanding.Resident, KingdomStandingCause.None, true)]
		[TestCase(KingdomResidentStanding.Resident, KingdomStandingCause.Raid, false)]
		[TestCase(KingdomResidentStanding.Resident, KingdomStandingCause.Followed, false)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Unwitnessed, true)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Founder, true)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.None, false)]
		[TestCase(KingdomResidentStanding.Dead, KingdomStandingCause.Taken, false)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomStandingCause.Followed, true)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomStandingCause.Astray, true)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomStandingCause.Violence, false)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomStandingCause.None, false)]
		public void ACauseOnlyFitsItsOwnStanding(KingdomResidentStanding standing, KingdomStandingCause cause, bool fits)
		{
			Assert.AreEqual(fits, KingdomResidentRules.CauseFits(standing, cause));
		}

		/// <summary>
		/// The one bridge to the funeral the city already tells: the four death causes are
		/// KingdomOfficeRules.DeathCause's own, in its own order, so CauseClause keeps being the ONE
		/// telling and no second cause vocabulary is written.
		/// </summary>
		[TestCase(KingdomStandingCause.Unwitnessed, KingdomOfficeRules.DeathCause.Unknown)]
		[TestCase(KingdomStandingCause.Violence, KingdomOfficeRules.DeathCause.Violence)]
		[TestCase(KingdomStandingCause.Raid, KingdomOfficeRules.DeathCause.Raid)]
		[TestCase(KingdomStandingCause.Founder, KingdomOfficeRules.DeathCause.Player)]
		public void ADeathCauseTellsItselfThroughTheSurfaceTheCityAlreadyHas(KingdomStandingCause cause, KingdomOfficeRules.DeathCause expected)
		{
			int ordinal;
			Assert.IsTrue(KingdomResidentRules.TryDeathCauseOrdinal(cause, out ordinal));
			Assert.AreEqual((int)expected, ordinal);
			Assert.AreEqual(KingdomOfficeRules.CauseClause(expected), KingdomOfficeRules.CauseClause((KingdomOfficeRules.DeathCause)ordinal));
		}

		/// <summary>An absence has no clause on a cairn, and inventing one would put a living person
		/// on a memorial.</summary>
		[TestCase(KingdomStandingCause.None)]
		[TestCase(KingdomStandingCause.Followed)]
		[TestCase(KingdomStandingCause.Taken)]
		[TestCase(KingdomStandingCause.Astray)]
		public void AnAbsenceHasNoDeathClause(KingdomStandingCause cause)
		{
			int ordinal;
			Assert.IsFalse(KingdomResidentRules.TryDeathCauseOrdinal(cause, out ordinal));
			Assert.AreEqual(0, ordinal);
		}

		/// <summary>§8.3: a body the player took away is still on the roll and contributes no
		/// labour. The dead are off the roll entirely.</summary>
		[TestCase(KingdomResidentStanding.Resident, true, true)]
		[TestCase(KingdomResidentStanding.Abroad, false, true)]
		[TestCase(KingdomResidentStanding.Dead, false, false)]
		public void AbroadContributesNoLabourAndStaysOnTheRoll(KingdomResidentStanding standing, bool labours, bool onTheRoll)
		{
			KingdomStandingCause cause = (standing == KingdomResidentStanding.Dead)
				? KingdomStandingCause.Unwitnessed
				: ((standing == KingdomResidentStanding.Abroad) ? KingdomStandingCause.Followed : KingdomStandingCause.None);
			KingdomResidentRow row = Settler(7, standing, cause);
			Assert.AreEqual(labours, KingdomResidentRules.Labours(row));
			Assert.AreEqual(onTheRoll, KingdomResidentRules.OnTheRoll(row));
			Assert.AreEqual(labours, KingdomResidentRules.Bindable(standing), "only a resident has a body bound in this city's ground");
		}

		/// <summary>One place decides which of the registry's causes a standing means, so the row
		/// and the registry can never disagree about why a body stopped being bound.</summary>
		[TestCase(KingdomResidentStanding.Dead, KingdomUnbindCause.Death)]
		[TestCase(KingdomResidentStanding.Abroad, KingdomUnbindCause.Abroad)]
		[TestCase(KingdomResidentStanding.Resident, KingdomUnbindCause.None)]
		public void AStandingNamesItsOwnUnbinding(KingdomResidentStanding standing, KingdomUnbindCause expected)
		{
			Assert.AreEqual(expected, KingdomResidentRules.UnbindFor(standing));
		}

		// ---- The roll and the registry -------------------------------------------------------

		[Test]
		public void TheTallyCountsEachStandingOnce()
		{
			KingdomCityState state = Book(
				Settler(1, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				Settler(2, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				Settler(3, KingdomResidentStanding.Abroad, KingdomStandingCause.Followed),
				Settler(4, KingdomResidentStanding.Dead, KingdomStandingCause.Raid));
			KingdomResidentTally tally;
			Assert.IsTrue(KingdomResidentRules.TryTally(state, out tally));
			Assert.AreEqual(2, tally.Resident);
			Assert.AreEqual(1, tally.Abroad);
			Assert.AreEqual(1, tally.Dead);
			Assert.AreEqual(3, tally.OnTheRoll, "the dead are off the roll and the abroad are on it");
		}

		/// <summary>
		/// §8.3 invariant 3: the roll == live bindings + Abroad. Every Resident row has exactly one
		/// live binding; no Abroad or Dead row has one.
		/// </summary>
		[Test]
		public void TheRollReconcilesWithTheRegistry()
		{
			KingdomCityState state = Book(
				Settler(1, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				Settler(2, KingdomResidentStanding.Abroad, KingdomStandingCause.Followed),
				Settler(3, KingdomResidentStanding.Dead, KingdomStandingCause.Founder));
			KingdomResidentTally tally;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomResidentRules.TryReconcile(state, BoundTo(1), out tally, out fault), fault.ToString());
			Assert.AreEqual(3, tally.Resident + tally.Abroad + tally.Dead);
		}

		/// <summary>A resident the registry has no body for is a person the city thinks is working
		/// and has nothing to show for. Named rather than tolerated.</summary>
		[Test]
		public void AResidentWithNoBoundBodyFailsTheReconciliation()
		{
			KingdomCityState state = Book(Settler(1, KingdomResidentStanding.Resident, KingdomStandingCause.None));
			KingdomResidentTally tally;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomResidentRules.TryReconcile(state, KingdomBindingTable.Empty, out tally, out fault));
			Assert.AreEqual(KingdomCityFault.UnknownBinding, fault);
		}

		/// <summary>A dead row the registry still holds a body for is a corpse the registry will go
		/// on handing out as somewhere to move a settler to.</summary>
		[Test]
		public void ADeadRowWithALiveBindingFailsTheReconciliation()
		{
			KingdomCityState state = Book(Settler(1, KingdomResidentStanding.Dead, KingdomStandingCause.Violence));
			KingdomResidentTally tally;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomResidentRules.TryReconcile(state, BoundTo(1), out tally, out fault));
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
		}

		/// <summary>The registry is realm-scope and the roll is one city's, so a key the OTHER city
		/// minted is not this city's to reconcile. Without this, the audit would fail every time a
		/// realm held two cities.</summary>
		[Test]
		public void TheOtherCitysBindingsAreNotThisCitysToReconcile()
		{
			KingdomCityState state = Book(Settler(1, KingdomResidentStanding.Resident, KingdomStandingCause.None));
			KingdomResidentTally tally;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomResidentRules.TryReconcile(state, BoundTo(1, 55, 56), out tally, out fault), fault.ToString());
		}

		/// <summary>The roster is written in one copy-on-write publish, and a roster that would seat
		/// one id twice is refused at the door rather than found by a reader later.</summary>
		[Test]
		public void ARosterCannotSeatOneIdTwice()
		{
			KingdomCityState state = Book();
			KingdomCityState next;
			KingdomCityFault fault;
			Assert.IsFalse(state.TryWithResidents(new KingdomResidentRow[2]
			{
				Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				Settler(7, KingdomResidentStanding.Abroad, KingdomStandingCause.Followed)
			}, out next, out fault));
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
			Assert.IsNull(next);
		}

		[Test]
		public void ARowIsFoundByItsIdAndAnUnknownIdIsNot()
		{
			KingdomCityState state = Book(
				Settler(4, KingdomResidentStanding.Resident, KingdomStandingCause.None),
				Settler(9, KingdomResidentStanding.Resident, KingdomStandingCause.None));
			int index;
			Assert.IsTrue(state.TryResidentIndex(9, out index));
			Assert.AreEqual(1, index);
			Assert.IsFalse(state.TryResidentIndex(5, out index));
			Assert.AreEqual(-1, index);
		}

		// ---- The brink windows the property bag used to hold ---------------------------------

		/// <summary>
		/// The three states the storage swap had to keep representable: no brink; a brink recorded
		/// with the word not yet gone out; and a brink whose window is anchored on the tick the
		/// founder was told. W1's row could only carry two of them, which is why the columns moved.
		/// </summary>
		[Test]
		public void ARowHoldsAllThreeBrinkStatesApart()
		{
			KingdomResidentRow row = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None);
			Assert.IsFalse(row.BrinkOf(BrinkKind.Roof).Stands);

			KingdomResidentRow recorded = row.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true, 900L, KingdomBrinkRules.Unwarned), null, 0);
			Assert.IsTrue(recorded.BrinkOf(BrinkKind.Roof).Stands);
			Assert.AreEqual(900L, recorded.BrinkOf(BrinkKind.Roof).ReachedTick);
			Assert.IsFalse(KingdomBrinkRules.Warned(recorded.BrinkOf(BrinkKind.Roof).WarnedTick),
				"a recorded brink nobody has been told about has no deadline");

			KingdomResidentRow warned = recorded.WithBrink(BrinkKind.Roof, recorded.BrinkOf(BrinkKind.Roof).WithWarned(1000L), null, 0);
			Assert.IsTrue(KingdomBrinkRules.Warned(warned.BrinkOf(BrinkKind.Roof).WarnedTick));
			Assert.AreEqual(900L, warned.BrinkOf(BrinkKind.Roof).ReachedTick, "warning somebody must not redate their loss");
		}

		/// <summary>The window the row carries is the one KingdomBrinkRules runs on, unchanged: the
		/// storage swap moved where the anchor is kept and nothing about what it means.</summary>
		[Test]
		public void TheRowsWarnedTickIsTheWindowTheRulesRunOn()
		{
			long warnedAt = 1000L;
			KingdomResidentRow row = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None)
				.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true, 900L, warnedAt), null, 0);
			long expiry = KingdomBrinkRules.ExpiryTick(BrinkKind.Roof, row.BrinkOf(BrinkKind.Roof).WarnedTick);
			Assert.IsFalse(KingdomBrinkRules.WindowSpent(BrinkKind.Roof, row.BrinkOf(BrinkKind.Roof).WarnedTick, expiry - 1L));
			Assert.IsTrue(KingdomBrinkRules.WindowSpent(BrinkKind.Roof, row.BrinkOf(BrinkKind.Roof).WarnedTick, expiry));
		}

		/// <summary>The two brinks are separate windows on one row: recording a creed brink cannot
		/// disturb a roof brink, which is the property-bag behaviour the swap had to preserve.</summary>
		[Test]
		public void TheTwoBrinksDoNotDisturbEachOther()
		{
			KingdomResidentRow row = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None)
				.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true, 900L, 1000L), null, 0)
				.WithBrink(BrinkKind.Creed, new KingdomBrinkWindow(true, 950L, 1100L), "Mechanimists", 2);
			Assert.AreEqual(1000L, row.BrinkOf(BrinkKind.Roof).WarnedTick);
			Assert.AreEqual(1100L, row.BrinkOf(BrinkKind.Creed).WarnedTick);
			Assert.AreEqual("Mechanimists", row.CreedToward);
			Assert.AreEqual(2, row.CreedChannel);

			KingdomResidentRow lifted = row.WithBrink(BrinkKind.Creed, KingdomBrinkWindow.None, null, 0);
			Assert.IsTrue(lifted.BrinkOf(BrinkKind.Roof).Stands, "lifting a creed brink must not lift a roof brink");
			Assert.IsFalse(lifted.BrinkOf(BrinkKind.Creed).Stands);
		}

		/// <summary>A lifted brink is forgotten rather than banked, creed and all: if the cause
		/// returns the founder gets the whole window again, because they are being asked to act on
		/// THIS one.</summary>
		[Test]
		public void ALiftedBrinkForgetsItsCreedAndItsTicks()
		{
			KingdomResidentRow lifted = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None)
				.WithBrink(BrinkKind.Creed, new KingdomBrinkWindow(true, 950L, 1100L), "Mechanimists", 2)
				.WithBrink(BrinkKind.Creed, KingdomBrinkWindow.None, "Mechanimists", 2);
			Assert.IsNull(lifted.CreedToward);
			Assert.AreEqual(0, lifted.CreedChannel);
			Assert.AreEqual(0L, lifted.BrinkOf(BrinkKind.Creed).ReachedTick);
			Assert.AreEqual(KingdomBrinkRules.Unwarned, lifted.BrinkOf(BrinkKind.Creed).WarnedTick);
		}

		/// <summary>A roof brink can never acquire a creed, however a caller asks: the creed travels
		/// with the creed window and is ignored for any other kind.</summary>
		[Test]
		public void ARoofBrinkNeverAcquiresACreed()
		{
			KingdomResidentRow row = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None)
				.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true, 900L, 1000L), "Mechanimists", 2);
			Assert.IsNull(row.CreedToward);
			Assert.AreEqual(0, row.CreedChannel);
		}

		/// <summary>The realm's own brink is not a settler's, and a row is asked about it honestly
		/// rather than answering with the roof's.</summary>
		[Test]
		public void TheRealmsBrinkIsNotSomethingARowAnswersFor()
		{
			KingdomResidentRow row = Settler(7, KingdomResidentStanding.Resident, KingdomStandingCause.None)
				.WithBrink(BrinkKind.Roof, new KingdomBrinkWindow(true, 900L, 1000L), null, 0);
			Assert.IsFalse(row.BrinkOf(BrinkKind.City).Stands);
			Assert.AreSame(row.Name, row.WithBrink(BrinkKind.City, new KingdomBrinkWindow(true, 1L, 2L), null, 0).Name);
			Assert.IsTrue(row.WithBrink(BrinkKind.City, KingdomBrinkWindow.None, null, 0).BrinkOf(BrinkKind.Roof).Stands,
				"a kind the row has no window for must leave the row alone");
		}

		// ---- The day shape -------------------------------------------------------------------

		/// <summary>§1.2(d): the day shape is DERIVED from the job, never authored per settler. A
		/// settler with no post keeps the hearth, which is what an unposted settler's day is.</summary>
		[TestCase(0, KingdomWorkKind.Growing, KingdomDayShape.Hearth)]
		[TestCase(11, KingdomWorkKind.Growing, KingdomDayShape.Field)]
		[TestCase(11, KingdomWorkKind.Store, KingdomDayShape.Market)]
		[TestCase(11, KingdomWorkKind.Producer, KingdomDayShape.Craft)]
		[TestCase(11, KingdomWorkKind.Refiner, KingdomDayShape.Craft)]
		[TestCase(11, KingdomWorkKind.Power, KingdomDayShape.Yard)]
		[TestCase(11, KingdomWorkKind.Other, KingdomDayShape.Hearth)]
		public void TheDayShapeIsDerivedFromThePostAndNeverAuthored(int jobWorkId, KingdomWorkKind kind, KingdomDayShape expected)
		{
			Assert.AreEqual(expected, KingdomResidentRules.DayShapeFor(jobWorkId, kind));
		}

		// ---- Origins -------------------------------------------------------------------------

		/// <summary>The district idiom, applied to where a settler walked in from: the row carries a
		/// code, the name stays in one place, and the two invert over every representable
		/// input.</summary>
		[Test]
		public void EveryOriginCodeInvertsBackToItsOwnName()
		{
			for (int i = 0; i < KingdomRules.Origins.Length; i++)
			{
				int code = KingdomResidentRules.OriginCode(KingdomRules.Origins[i]);
				Assert.AreNotEqual(KingdomResidentRules.NoOrigin, code, KingdomRules.Origins[i] + " has no code");
				Assert.AreEqual(KingdomRules.Origins[i], KingdomResidentRules.OriginKey(code));
			}
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("the moon")]
		public void AnUnknownOriginIsNoOriginRatherThanTheFirstOne(string origin)
		{
			Assert.AreEqual(KingdomResidentRules.NoOrigin, KingdomResidentRules.OriginCode(origin));
			Assert.IsNull(KingdomResidentRules.OriginKey(KingdomResidentRules.NoOrigin));
		}

		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(9999)]
		public void ACodeOutsideTheRegistryNamesNothing(int code)
		{
			Assert.IsNull(KingdomResidentRules.OriginKey(code));
		}

		[Test]
		public void ANullStateIsRefusedRatherThanCountedAsAnEmptyRoll()
		{
			KingdomResidentTally tally;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomResidentRules.TryTally(null, out tally));
			Assert.IsFalse(KingdomResidentRules.TryReconcile(null, KingdomBindingTable.Empty, out tally, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomResidentRules.TryReconcile(Book(), null, out tally, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}
	}
}
#endif
