using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the seed chain: what commits a field, what physically stands in
	/// one, what a gathering does with what it brings in, and how a harvest reaches a larder in a
	/// zone nobody is standing in. The rules it asks are all in
	/// <see cref="KingdomCropRules"/>; the state one field carries is on
	/// <see cref="XRL.World.Parts.r_KingdomPlot"/>; the per-pass walk is
	/// <see cref="KingdomPlot.OnSettlementPass"/>.
	/// <para>
	/// <b>The protection law, in the one place it binds hardest.</b> A committed seed is the
	/// founder's designation, exactly as a dedicated cask is. Nothing here sows a field the
	/// founder did not sow, nothing takes a seed the founder did not commit, and the only path
	/// out of a committed field is <see cref="Withdraw"/> &mdash; which is the founder's own
	/// action and hands the seed back. The rows this file lays are objects it created and marked
	/// (<see cref="RowProperty"/>), which is the only class of object a kingdom system may
	/// destroy.
	/// </para>
	/// </summary>
	public static class KingdomCrops
	{
		/// <summary>Blueprint tag declaring how many rows a design stands when it is sown. Read
		/// from the blueprint exactly the way a pantry's capacity is
		/// (<c>KingdomRules.LarderCapacityTag</c>), and for the same reason: what a design adds to
		/// the settlement's LEVEL is a catalogue fact, and how much actually stands in the ground
		/// is a fact about the object. <c>_notes/balance-sim.py</c> re-derives every food design's
		/// <c>Carries</c> from this tag.</summary>
		public const string RowsTag = "r_KingdomCropRows";

		/// <summary>Marks a plant this file laid, so a later withdrawal or striking can find its
		/// own rows and nothing else's. The protection law's whole warrant for removing them.</summary>
		public const string RowProperty = "KingdomCropRow";

		/// <summary>Ties a row to the field that sowed it.</summary>
		public const string RowFieldProperty = "KingdomCropField";

		/// <summary>Tick the founder committed seed to this field. A DATE and not a clock: it is
		/// never re-anchored, and nothing reads it to decide what is owed &mdash; the cycle runs
		/// off <c>r_KingdomPlot.NextStageTick</c>. It exists so the chronicle and the report can
		/// say when this field was sown.</summary>
		public const string SownTickProperty = "KingdomCropSownTick";

		/// <summary>Rows this field was sown with. Stamped from <see cref="RowsTag"/> at sowing so
		/// a retune of the catalogue never silently changes what a field already in the ground is
		/// worth.</summary>
		public const string RowsProperty = "KingdomCropRows";

		/// <summary>Gatherings this field has already resolved. The kernel ordinal the seed-return
		/// draw is keyed on, so no cycle is ever asked twice and a reload cannot re-roll one.</summary>
		public const string CyclesProperty = "KingdomCropCycles";

		/// <summary>The seed blueprint committed to this field, so a withdrawal hands back what
		/// was actually put in.</summary>
		public const string SeedProperty = "KingdomCropSeed";

		/// <summary>The last want this field announced (STANDARDS 7b), as a
		/// <c>KingdomCropRules.FieldWant</c>. Zero means nothing is being said, so the next real
		/// block speaks.</summary>
		public const string SaidProperty = "KingdomCropSaid";

		// ==================================================================================
		// Reading a field
		// ==================================================================================

		/// <summary>The field part of a finished work, or null for anything that is not a field.</summary>
		public static r_KingdomPlot FieldOf(GameObject Work)
		{
			if (!GameObject.Validate(Work) || Work.GetIntProperty("KingdomBuilt") != 1)
			{
				return null;
			}
			return Work.GetPart<r_KingdomPlot>();
		}

		/// <summary>Rows this design stands when it is sown, off its own blueprint. Zero for a
		/// blueprint that declares none, which is a field that grows nothing and says so at the
		/// first attempt to sow it.</summary>
		public static int DeclaredRows(GameObject Work)
		{
			if (Work == null)
			{
				return 0;
			}
			int rows;
			if (!int.TryParse(Work.GetTag(RowsTag, ""), out rows) || rows < 0)
			{
				return 0;
			}
			return rows;
		}

		/// <summary>Whether the founder has committed seed to this field. The whole of the
		/// Addendum 11(b) gate, read in one place so every consumer agrees.</summary>
		public static bool IsSown(GameObject Work)
		{
			r_KingdomPlot field = FieldOf(Work);
			return field != null && field.Stage != KingdomCropRules.PlotStage.Dormant;
		}

		/// <summary>Whether this work is worn past the point where anything comes out of it.</summary>
		public static bool IsCondemned(GameObject Work)
		{
			return KingdomLodgingRules.IsCondemned(KingdomWear.WearOf(Work));
		}

		/// <summary>
		/// The same parsed <c>Carries</c> list with the <c>food</c> entry dropped when this work
		/// is a field nobody has sown. Addendum 11(b): a farm starts producing only once seeds
		/// are committed, so uncommitted ground carries no food to the settlement's level and
		/// makes none in a day.
		/// <para>
		/// Everything else the design carries is left exactly where it was. A home farm's mill and
		/// its yard are built, standing and real whether or not a row is in the ground; only the
		/// dinner is conditional. The list is copied rather than edited, because the caller's is
		/// the catalogue's own parse and is reused for every work of the same design.
		/// </para>
		/// </summary>
		/// <param name="Work">The finished work being folded into the level.</param>
		/// <param name="Carries">Its design's parsed carries. Null passes straight through.</param>
		public static List<KindAmount> WithoutUnsownFood(GameObject Work, List<KindAmount> Carries)
		{
			if (Carries == null || Carries.Count == 0)
			{
				return Carries;
			}
			r_KingdomPlot field = FieldOf(Work);
			if (field == null || field.Stage != KingdomCropRules.PlotStage.Dormant)
			{
				return Carries;
			}
			// TryParseTally already folds every kind to its lower-case token, so the comparison
			// is against the constant directly rather than through a second normaliser that
			// could disagree with the first.
			List<KindAmount> kept = null;
			for (int i = 0; i < Carries.Count; i++)
			{
				if (Carries[i].Kind != KingdomCatalogueRules.SupportFood)
				{
					continue;
				}
				kept = new List<KindAmount>(Carries.Count - 1);
				for (int j = 0; j < Carries.Count; j++)
				{
					if (Carries[j].Kind != KingdomCatalogueRules.SupportFood)
					{
						kept.Add(Carries[j]);
					}
				}
				break;
			}
			return kept ?? Carries;
		}

		/// <summary>
		/// The daily food this zone's SOWN fields are already counted for, which the growth pass
		/// subtracts from its clocked make. The cycle delivers that food physically, on the crop's
		/// own six days, so counting it a second time per day would feed the settlement twice out
		/// of one field.
		/// <para>
		/// Folded at exactly the effectiveness <c>KingdomSubsidence.Supports</c> folds it at, and
		/// through exactly the same <c>KingdomCatalogueRules.Carried</c>, so the subtraction
		/// cancels the addition to the unit rather than approximately.
		/// </para>
		/// <para>
		/// <b>And it carries no method factor, deliberately.</b> What this subtracts is what the
		/// book CREDITED the field with, not what the field GREW: the credit is
		/// <c>Supports</c>'s own baseline carry, so the subtraction has to be that same baseline or
		/// it stops cancelling. The keepers' method lands on the physical gathering instead
		/// (<c>KingdomCropRules.HarvestYield</c>), which is why a researched realm eats better
		/// &mdash; the book removes one field's worth and the field delivers rather more than one field's
		/// worth, and the difference is exactly the bonus. Methoding this line as well would hand
		/// that difference straight back and, on a settlement whose granaries are counted here
		/// too, would charge the granaries for the fields' learning.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null carries nothing.</param>
		public static int CycledFoodPerDay(KingdomSurvey Survey)
		{
			if (Survey == null)
			{
				return 0;
			}
			int cycled = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				r_KingdomPlot field = FieldOf(work);
				if (field == null || field.Stage == KingdomCropRules.PlotStage.Dormant)
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				if (carries == null)
				{
					continue;
				}
				int effectiveness = KingdomWear.EffectivenessOf(work);
				cycled += KingdomCatalogueRules.Carried(
					KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportFood), effectiveness);
			}
			return cycled;
		}

		// ==================================================================================
		// The next link: what industry does with a harvest
		// ==================================================================================

		/// <summary>
		/// Whether this finished work is a mill &mdash; asked of the OBJECT, off vanilla's own
		/// <c>Mill</c> part, exactly as <see cref="FieldOf"/> asks a field off
		/// <c>r_KingdomPlot</c>.
		/// <para>
		/// Vanilla's <c>Mill</c> (<c>D/XRL/World/Parts/Mill.cs:9</c>) is one of only four parts in
		/// the whole game that transform matter, and its blank-target path runs
		/// <c>Campfire.PerformPreserve</c> (<c>:82-101</c>) &mdash; which is precisely what
		/// vanilla's shipped <c>Millstone</c> does with a vinewafer. Testing for the part rather
		/// than for a catalogue key means a third party's own millstone counts the moment it
		/// declares one, the same way a third party's cistern counts the moment it declares a
		/// <c>LiquidVolume</c>.
		/// </para>
		/// </summary>
		public static bool IsMill(GameObject Work)
		{
			return GameObject.Validate(Work) && Work.GetIntProperty("KingdomBuilt") == 1 && Work.HasPart("Mill");
		}

		/// <summary>
		/// Servings a day the settlement's mills are counted for by the LEVEL, at exactly the
		/// effectiveness the level counts them at. The mill's mirror of
		/// <see cref="CycledFoodPerDay"/>, and it exists for the same reason: a mill delivers its
		/// food PHYSICALLY, by taking real crops off the larder shelves and putting real preserves
		/// back, so its <c>Carries</c> must be subtracted from the clocked daily make or the
		/// settlement would be fed twice out of one millstone.
		/// <para>
		/// Baseline, and no method factor, for <see cref="CycledFoodPerDay"/>'s reason exactly: a
		/// subtraction that removes a CREDIT has to be the size of the credit.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null makes nothing.</param>
		public static int MilledFoodPerDay(KingdomSurvey Survey)
		{
			if (Survey == null)
			{
				return 0;
			}
			int milled = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!IsMill(work))
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				if (carries == null)
				{
					continue;
				}
				int effectiveness = KingdomWear.EffectivenessOf(work);
				milled += KingdomCatalogueRules.Carried(
					KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportFood), effectiveness);
			}
			return milled;
		}

		/// <summary>
		/// What one crop of this settlement's becomes when it is bound to keep: the mod's stated
		/// staple where it has one (<c>KingdomRules.PreservedStapleFor</c>), otherwise the crop's
		/// OWN vanilla <c>PreservableItem Result</c>, read off a sample of the thing itself.
		/// <para>
		/// The fallback is what makes a third party's crop mill without this file knowing about
		/// it: <c>PreservableItem</c> is a two-field pure-data marker
		/// (<c>D/XRL/World/Parts/PreservableItem.cs:6-10</c>) and reading it is free of side
		/// effects, unlike <c>Campfire</c>'s own ingredient listing, which mutates counts
		/// (VANILLA-PRODUCTION-TRUTH 2.3's static-cache hazard note).
		/// </para>
		/// </summary>
		/// <param name="Crop">A crop blueprint. Null or unknown yields null.</param>
		/// <returns>The preserved blueprint, or null for a crop nothing can bind.</returns>
		public static string StapleFor(string Crop)
		{
			string stated = KingdomRules.PreservedStapleFor(Crop);
			if (!string.IsNullOrEmpty(stated))
			{
				return stated;
			}
			if (string.IsNullOrEmpty(Crop) || !GameObjectFactory.Factory.HasBlueprint(Crop))
			{
				return null;
			}
			GameObject sample = GameObject.Create(Crop);
			if (sample == null)
			{
				return null;
			}
			PreservableItem preservable = sample.GetPart<PreservableItem>();
			string result = (preservable == null) ? null : preservable.Result;
			sample.Obliterate();
			return string.IsNullOrEmpty(result) ? null : result;
		}

		// ==================================================================================
		// Sowing, and taking it back
		// ==================================================================================

		/// <summary>
		/// Commits one seed to the field the founder is standing in. Everything the seed item's
		/// own part does is call this, the same split <c>r_FounderBasin</c> and
		/// <c>r_KingdomCarrySign</c> keep.
		/// </summary>
		/// <param name="Actor">The founder. Non-players never reach here.</param>
		/// <param name="Seed">The seed item, which is decremented by exactly one on success.</param>
		public static void AttemptSow(GameObject Actor, GameObject Seed)
		{
			if (Actor == null || Seed == null)
			{
				return;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Cell cell = Actor.CurrentCell;
			Zone zone = Actor.CurrentZone;
			string crop = KingdomData.CropForSeed(Seed.Blueprint);
			string row = KingdomData.RowForCrop(crop);
			GameObject work = FieldUnder(zone, cell);
			r_KingdomPlot field = FieldOf(work);
			KingdomSurvey survey = (zone == null) ? null : KingdomSurvey.Take(zone, system);
			KingdomCropRules.SowVerdict verdict = KingdomCropRules.AssessSow(
				HasField: field != null,
				Claimed: system.Founded && zone != null && system.ClaimedZones.Contains(zone.ZoneID),
				AlreadySown: field != null && field.Stage != KingdomCropRules.PlotStage.Dormant,
				Condemned: IsCondemned(work),
				HasRow: !string.IsNullOrEmpty(row),
				StoredWater: (survey == null) ? 0 : survey.StoredWater,
				Population: system.Population);
			if (verdict != KingdomCropRules.SowVerdict.Sown)
			{
				Popup.Show(KingdomCropRules.SowRefusal(verdict));
				return;
			}
			int rows = DeclaredRows(work);
			if (rows <= 0)
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoCrop));
				return;
			}
			string fieldName = work.ShortDisplayName;
			// Consent before cost, exactly as the carry-sign asks it: the founder sees the crop,
			// the rows, the wait and the water before one dram or one seed is spent.
			if (Popup.ShowYesNo(KingdomCropRules.SowConfirm(CropName(crop), fieldName, rows, KingdomCropRules.PlantWaterCostDrams)) != DialogResult.Yes)
			{
				return;
			}
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(KingdomCropRules.PlantWaterCostDrams, out debit))
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoWater));
				return;
			}

			// Snapshot field and its pre-existing rows before touching either. Water can therefore
			// return to the exact casks, and a refused seed leaves no half-sown field behind.
			KingdomCropRules.PlotStage oldStage = field.Stage;
			string oldCrop = field.CropBlueprint;
			long oldNext = field.NextStageTick;
			bool oldNoLarder = field.NoLarderAnnounced;
			string oldSeed = work.GetStringProperty(SeedProperty);
			bool hadRows = work.HasIntProperty(RowsProperty);
			int oldRows = work.GetIntProperty(RowsProperty);
			bool hadSownTick = work.HasIntProperty(SownTickProperty);
			int oldSownTick = work.GetIntProperty(SownTickProperty);
			bool hadCycles = work.HasIntProperty(CyclesProperty);
			int oldCycles = work.GetIntProperty(CyclesProperty);
			bool hadSaid = work.HasIntProperty(SaidProperty);
			int oldSaid = work.GetIntProperty(SaidProperty);
			List<GameObject> rowsBefore = RowsOf(zone, work);
			// Snapshot first, then cross the exact physical debit boundary. Nothing below may
			// discover that it did not know how to compensate only after water has moved.
			if (!debit.Commit())
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoWater));
				return;
			}
			long now = The.Game.TimeTicks;
			int laid = 0;
			int seedCount = Seed.Count;
			try
			{
				field.CropBlueprint = crop;
				work.SetStringProperty(SeedProperty, Seed.Blueprint);
				work.SetIntProperty(RowsProperty, rows);
				work.SetIntProperty(SownTickProperty, StampOf(now));
				work.SetIntProperty(CyclesProperty, 0);
				work.SetIntProperty(SaidProperty, 0);
				field.NoLarderAnnounced = false;
				field.NextStageTick = KingdomCropRules.RipenTick(now);
				field.ApplyStage(KingdomCropRules.PlotStage.Growing);
				laid = LayRows(zone, work, row, rows);
				if (laid <= 0)
				{
					throw new InvalidOperationException("No crop row could be laid in the field footprint.");
				}
				bool destroyed = Seed.Destroy(null, Silent: true);
				bool seedSpent = (seedCount > 1 && GameObject.Validate(Seed) && Seed.Count == seedCount - 1)
					|| (seedCount == 1 && destroyed && !GameObject.Validate(Seed));
				if (!seedSpent)
				{
					throw new InvalidOperationException("The seed refused to leave its stack.");
				}
			}
			catch (Exception ex)
			{
				// Restore the receipt first. Even a hostile callback in field or row compensation
				// cannot strand the physical debit behind it.
				bool waterRestored = debit.Rollback();
				bool fieldRestored = true;
				try
				{
					field.CropBlueprint = oldCrop;
					field.NextStageTick = oldNext;
					field.NoLarderAnnounced = oldNoLarder;
					field.ApplyStage(oldStage);
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: field snapshot restore failed (" + restoreError.Message + ")");
				}
				try
				{
					work.SetStringProperty(SeedProperty, oldSeed);
					RestoreInt(work, RowsProperty, hadRows, oldRows);
					RestoreInt(work, SownTickProperty, hadSownTick, oldSownTick);
					RestoreInt(work, CyclesProperty, hadCycles, oldCycles);
					RestoreInt(work, SaidProperty, hadSaid, oldSaid);
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: work snapshot restore failed (" + restoreError.Message + ")");
				}
				try
				{
					List<GameObject> rowsAfter = RowsOf(zone, work);
					for (int i = 0; i < rowsAfter.Count; i++)
					{
						if (rowsBefore.Contains(rowsAfter[i])) continue;
						bool removed = false;
						try { removed = rowsAfter[i].Obliterate(null, Silent: true); }
						finally
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(zone, rowsAfter[i]);
						}
						if (!removed)
						{
							fieldRestored = false;
						}
					}
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: row cleanup failed (" + restoreError.Message + ")");
				}
				try
				{
					if (GameObject.Validate(Seed) && Seed.Count != seedCount)
					{
						Seed.Count = seedCount;
					}
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: seed stack restore failed (" + restoreError.Message + ")");
				}
				KingdomLog.Log("crop: sow transaction refused (" + ex.Message + "; water restored="
					+ waterRestored + "; field restored=" + fieldRestored + ")");
				Popup.Show(waterRestored && fieldRestored
					? "The sowing would not hold. The field is unchanged and the water was returned to its casks."
					: "The sowing would not hold, and one rollback could not be proved exact. Inspect the field, seed, and stores.");
				return;
			}
			// Crewed straight away, off a survey retaken now that the field asks for hands at all:
			// an unsown field is deliberately not in KingdomSurvey.Works, so until this runs the
			// new field carries no crew stamp and would read as "sown and nobody working it" to a
			// founder who sowed and then stood still for a week without a zone activation.
			KingdomGrowth.AssignWork(system, KingdomSurvey.Take(zone, system));
			string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
			system.RecordDeed("the " + fieldName + " you sowed at " + realm);
			KingdomChronicle.Record(system, KingdomCropRules.SownChronicle(CropName(crop), fieldName, realm));
			system.Ledger.Note("{{G|The " + fieldName + " is sown with " + CropName(crop) + ": " + laid
				+ ((laid == 1) ? " row" : " rows") + " in the ground, ripe in " + KingdomCropRules.CropDays + " days.}}");
			if (laid < rows)
			{
				// Said plainly rather than swallowed: the ground took fewer rows than the design
				// promises, and the harvest is what STANDS, so the founder is owed the difference.
				MessageQueue.AddPlayerMessage("{{r|The ground took only " + laid + " of the " + rows
					+ " rows the " + fieldName + " wants. Clear what is standing in it, and sow again for the rest.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("crop: sown " + fieldName + " crop=" + crop + " rows=" + laid + "/" + rows);
		}

		private static void RestoreInt(GameObject Object, string Property, bool Had, int Value)
		{
			if (Had)
			{
				Object.SetIntProperty(Property, Value);
			}
			else
			{
				Object.RemoveIntProperty(Property);
			}
		}

		/// <summary>
		/// Takes the founder's own seed back out of a field: the rows come up, the cycle stops,
		/// and one seed is handed back. The protection law's other half &mdash; a designation the
		/// founder made is a designation the founder can unmake, and nothing else can.
		/// </summary>
		/// <param name="Actor">The founder.</param>
		/// <param name="Work">The field.</param>
		public static void Withdraw(GameObject Actor, GameObject Work)
		{
			r_KingdomPlot field = FieldOf(Work);
			if (Actor == null || field == null)
			{
				return;
			}
			if (field.Stage == KingdomCropRules.PlotStage.Dormant)
			{
				Popup.Show("There is nothing sown here to take back.");
				return;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			string crop = field.CropBlueprint;
			string fieldName = Work.ShortDisplayName;
			string seed = Work.GetStringProperty(SeedProperty);
			if (string.IsNullOrEmpty(seed))
			{
				seed = KingdomData.SeedForCrop(crop);
			}
			if (Popup.ShowYesNo("Take the seed back out of the " + fieldName + "?\n\nThe rows come up, and it grows nothing until you sow it again.") != DialogResult.Yes)
			{
				return;
			}
			ClearRows(Work.CurrentZone, Work);
			field.CropBlueprint = null;
			field.NoLarderAnnounced = false;
			field.NextStageTick = 0L;
			field.ApplyStage(KingdomCropRules.PlotStage.Dormant);
			Work.SetIntProperty(RowsProperty, 0);
			Work.SetIntProperty(CyclesProperty, 0);
			Work.SetStringProperty(SeedProperty, null);
			Work.SetIntProperty(SaidProperty, 0);
			if (!string.IsNullOrEmpty(seed))
			{
				GameObject returned = GameObject.Create(seed);
				if (returned != null)
				{
					Actor.ReceiveObject(returned);
				}
			}
			string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
			system.Ledger.Note("{{K|" + KingdomCropRules.WithdrawnNote(CropName(crop), fieldName, realm) + "}}");
			MessageQueue.AddPlayerMessage("{{K|" + KingdomCropRules.WithdrawnNote(CropName(crop), fieldName, realm) + "}}");
		}

		/// <summary>
		/// Strips one wild plant of its seed, once and once only. The third honest source, and
		/// the narrowest: only a plant of the species the seed grows carries this, only a plant
		/// nobody owns gives it up, and a plant that has been stripped has nothing left to give.
		/// </summary>
		/// <param name="Actor">Whoever is gathering.</param>
		/// <param name="Plant">The wild plant.</param>
		/// <param name="SeedBlueprint">What it carries.</param>
		public static void TakeWildSeed(GameObject Actor, GameObject Plant, string SeedBlueprint)
		{
			if (Actor == null || Plant == null || string.IsNullOrEmpty(SeedBlueprint))
			{
				return;
			}
			if (Plant.GetIntProperty(WildSeedTakenProperty) == 1)
			{
				Popup.Show("This one has already been stripped of its seed.");
				return;
			}
			// Somebody else's crop is somebody else's. The protection law read the other way
			// round: the mod does not help the founder rob a farmer.
			Physics physics = Plant.GetPart<Physics>();
			if (physics != null && !string.IsNullOrEmpty(physics.Owner))
			{
				Popup.Show("These are somebody's, and they are watching them.");
				return;
			}
			GameObject seed = GameObject.Create(SeedBlueprint);
			if (seed == null)
			{
				return;
			}
			Plant.SetIntProperty(WildSeedTakenProperty, 1);
			Actor.ReceiveObject(seed);
			MessageQueue.AddPlayerMessage("You strip the seed from " + Plant.the + Plant.ShortDisplayName + ".");
		}

		/// <summary>Set once on a wild plant whose seed has been taken, so one plant is one
		/// seed forever.</summary>
		public const string WildSeedTakenProperty = "KingdomWildSeedTaken";

		// ==================================================================================
		// The rows themselves
		// ==================================================================================

		/// <summary>
		/// Lays up to <paramref name="Rows"/> standing plants across the field's footprint, on a
		/// stride so they read as rows rather than as a heap, and never over anything (STANDARDS
		/// 7: automatic placement targets empty cells only). Returns how many actually stood,
		/// which is what the field is worth from here on.
		/// </summary>
		public static int LayRows(Zone Z, GameObject Work, string RowBlueprint, int Rows)
		{
			if (Z == null || Work == null || string.IsNullOrEmpty(RowBlueprint) || Rows <= 0)
			{
				return 0;
			}
			KingdomPlotRules.PlotRect rect;
			if (!KingdomPlots.TryReadFootprint(Work, out rect))
			{
				return 0;
			}
			string id = Work.GetStringProperty(KingdomPlots.PlotIdProperty);
			List<Cell> open = new List<Cell>();
			for (int y = rect.Y1; y <= rect.Y2; y++)
			{
				for (int x = rect.X1; x <= rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable())
					{
						open.Add(cell);
					}
				}
			}
			if (open.Count == 0)
			{
				return 0;
			}
			// Strided rather than sequential, so a sparse kitchen garden reads as a garden spread
			// over its patch instead of a solid block in one corner. Vanilla's own village farm
			// builder lays a crop on a stride for the same reason.
			//
			// Which cells are spoken for is tracked here rather than re-asked of the cell, because
			// Cell.IsEmpty() is true of a cell already holding a plant (a RenderLayer-3 non-combat
			// object does not make a cell non-empty), so a second sweep would stack two rows on one
			// square and the field would look half its size while counting full.
			bool[] used = new bool[open.Count];
			int stride = open.Count / Rows;
			if (stride < 1)
			{
				stride = 1;
			}
			int laid = 0;
			for (int i = 0; i < open.Count && laid < Rows; i += stride)
			{
				if (StandRow(open[i], RowBlueprint, Work, id))
				{
					used[i] = true;
					laid++;
				}
			}
			// A dense design wants more rows than one strided sweep reaches; fill the gaps left
			// behind rather than shorting the field for an arithmetic reason.
			for (int i = 0; i < open.Count && laid < Rows; i++)
			{
				if (used[i])
				{
					continue;
				}
				if (StandRow(open[i], RowBlueprint, Work, id))
				{
					used[i] = true;
					laid++;
				}
			}
			return laid;
		}

		/// <summary>Stands one row in one cell, marked as this field's own. False when the
		/// blueprint does not resolve, which stops the sweep rather than spinning it.</summary>
		private static bool StandRow(Cell C, string RowBlueprint, GameObject Work, string PlotId)
		{
			GameObject plant = GameObject.Create(RowBlueprint);
			if (plant == null)
			{
				return false;
			}
			plant.SetIntProperty(RowProperty, 1);
			plant.SetIntProperty(KingdomPlots.PlotPartProperty, 1);
			plant.SetStringProperty(RowFieldProperty, Work.ID);
			if (!string.IsNullOrEmpty(PlotId))
			{
				plant.SetStringProperty(KingdomPlots.PlotIdProperty, PlotId);
			}
			GameObject accepted = null;
			try { accepted = C.AddObject(plant); }
			finally { KingdomSurvey.ObserveAddResultInActive(C.ParentZone, plant, accepted); }
			return ReferenceEquals(accepted, plant) && ReferenceEquals(plant.CurrentCell, C);
		}

		/// <summary>Rows standing ripe right now &mdash; what a gathering is actually owed, and
		/// what a founder who walked the rows with a basket has already reduced.</summary>
		public static int CountRipe(List<GameObject> Rows)
		{
			int ripe = 0;
			for (int i = 0; (Rows != null) && i < Rows.Count; i++)
			{
				Harvestable harvestable = Rows[i].GetPart<Harvestable>();
				if (harvestable != null && harvestable.Ripe)
				{
					ripe++;
				}
			}
			return ripe;
		}

		/// <summary>Every row this field laid that is still standing.</summary>
		public static List<GameObject> RowsOf(Zone Z, GameObject Work)
		{
			List<GameObject> rows = new List<GameObject>();
			if (Z == null || Work == null)
			{
				return rows;
			}
			string id = Work.ID;
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.CropRows.Count; i++)
			{
				GameObject item = survey.CropRows[i];
				if (item.GetIntProperty(RowProperty) == 1 && item.GetStringProperty(RowFieldProperty) == id)
				{
					rows.Add(item);
				}
			}
			return rows;
		}

		/// <summary>Takes this field's own rows up. Only objects this file created and marked are
		/// touched, which is the protection law's whole warrant.</summary>
		public static void ClearRows(Zone Z, GameObject Work)
		{
			List<GameObject> rows = RowsOf(Z, Work);
			for (int i = 0; i < rows.Count; i++)
			{
				try { rows[i].Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(Z, rows[i]); }
			}
		}

		/// <summary>Turns every standing row ripe or unripe, through vanilla's own
		/// <c>Harvestable.UpdateRipeStatus</c> so the tile and the colours swap exactly the way
		/// every other plant in the game swaps them.</summary>
		/// <returns>Rows that were standing ripe BEFORE the change, which is what a gathering
		/// counts.</returns>
		public static int SetRipe(List<GameObject> Rows, bool Ripe)
		{
			int wereRipe = 0;
			for (int i = 0; (Rows != null) && i < Rows.Count; i++)
			{
				Harvestable harvestable = Rows[i].GetPart<Harvestable>();
				if (harvestable == null)
				{
					continue;
				}
				if (harvestable.Ripe)
				{
					wereRipe++;
				}
				if (harvestable.Ripe != Ripe)
				{
					harvestable.UpdateRipeStatus(Ripe);
				}
			}
			return wereRipe;
		}

		// ==================================================================================
		// Delivery, including across zones
		// ==================================================================================

		/// <summary>
		/// Writes down what this zone's dedicated pantries hold and can hold, on the pass that
		/// stood in it. Rewritten from the ground every time, including down to zero &mdash; a
		/// larder that was struck stops being somewhere a harvest can be sent on the pass the
		/// founder sees the empty plot, and never before.
		/// <para>
		/// The <c>r_TAF_Larders_&lt;zoneID&gt;_*</c> game-state pair this replaced held ROOM, one
		/// int; the zone row holds the level and the capacity it is the difference of, which is the
		/// same answer and one the drain can also read (LIVING-CITY-ARCHITECTURE &sect;1.2(b)).
		/// </para>
		/// </summary>
		public static void RecordLarders(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (Survey == null)
			{
				return;
			}
			Simulation.City.KingdomCity.RecordLarder(System, Z, Survey.FoodStored, Survey.FoodCapacity, TimeTicks);
		}

		/// <summary>
		/// Room the city's OTHER claimed zones were last seen holding for a harvest. The exclusion
		/// is the whole point: this zone has just been offered the load from the ground.
		/// <para>
		/// Knowledge, not truth, exactly as <c>KingdomSubsidence.OtherZones</c> is: a zone nobody
		/// has ever stood in contributes nothing, and a sighting stays exactly as old as it is.
		/// When the belief turns out wrong the load arrives at a full larder and is lost there,
		/// which is a story rather than a bug &mdash; the same contract the manifest keeps.
		/// </para>
		/// </summary>
		public static int LarderRoomElsewhere(KingdomSystem System, Zone Z)
		{
			return Simulation.City.KingdomCity.LarderRoomElsewhere(System, Z);
		}

		/// <summary>
		/// Materialises whatever of the city's harvest is still on the road into this zone's
		/// pantries. Called at the top of every settlement pass, before the day's rations are
		/// drawn, so a load that arrived is a load the settlement can eat.
		/// <para>
		/// This is the crystallise-at-awareness idiom the rest of the mod runs on: the CITY's
		/// stores were credited the moment the harvest came due, wherever that was; the physical
		/// crop appears when somebody is standing where it was sent. Nothing is touched in an
		/// unloaded zone, because nothing in an unloaded zone can be touched.
		/// </para>
		/// </summary>
		public static void DeliverPending(KingdomSystem System, KingdomSurvey Survey)
		{
			DeliverPending(System, null, Survey);
		}

		/// <summary>
		/// As above, and with the ground in hand it can arrive <b>embodied</b>.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7, Addendum 12(c)'s canonical image: <i>"walking around
		/// in my house in 1 zone, a farm finishes harvesting in another zone, a porter should come
		/// and put the harvested goods in the storage that is in the zone i am walking around."</i>
		/// The load was already the city's the moment the harvest came due; what the porter changes
		/// is the RENDERING and never the effect, which is invariant I2. A load that walks in on a
		/// back is not delivered twice by the plain path below, because it left
		/// <see cref="KingdomSystem.PendingCrop"/> when it went onto that back.
		/// </para>
		/// </summary>
		public static void DeliverPending(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || Survey == null || System.PendingCrop <= 0 || Survey.Larders.Count == 0)
			{
				return;
			}
			string blueprint = System.PendingCropBlueprint;
			if (string.IsNullOrEmpty(blueprint))
			{
				blueprint = KingdomData.CropForStyle(System.Style);
			}
			if (Z != null)
			{
				string from = System.PendingCropZoneId;
				if (!string.IsNullOrEmpty(from) && !System.ClaimedZones.Contains(from))
				{
					// Ground this city does not hold cannot be walked out of. The carrier still
					// comes in by a wall, it is simply no longer a wall that faces anything.
					from = null;
				}
				int carried = Simulation.City.KingdomPorters.Embody(System, Z, Survey, from, blueprint,
					System.PendingCrop, (The.Game != null) ? The.Game.TimeTicks : 0L);
				if (carried > 0)
				{
					System.PendingCrop -= carried;
					if (System.PendingCrop <= 0)
					{
						System.PendingCrop = 0;
						System.PendingCropBlueprint = null;
						System.PendingCropZoneId = null;
					}
					if (KingdomLog.Enabled) KingdomLog.Log("crop: " + carried + " went onto a porter's back, pending=" + System.PendingCrop);
					return;
				}
			}
			int delivered = Survey.StoreFood(System.PendingCrop, blueprint);
			if (delivered <= 0)
			{
				return;
			}
			System.PendingCrop -= delivered;
			if (System.PendingCrop <= 0)
			{
				System.PendingCrop = 0;
				System.PendingCropBlueprint = null;
				System.PendingCropZoneId = null;
			}
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			System.Ledger.Note("{{G|" + KingdomCropRules.DeliveryNote(delivered, realm) + "}}");
			MessageQueue.AddPlayerMessage("{{G|" + KingdomCropRules.DeliveryNote(delivered, realm) + "}}");
			if (KingdomLog.Enabled) KingdomLog.Log("crop: delivered " + delivered + " pending=" + System.PendingCrop);
		}

		/// <summary>
		/// Puts a gathering where it can go: this zone's pantries first, the city's other pantries
		/// second (as a load in flight), and the ground last.
		/// </summary>
		/// <returns>What was lost for want of room anywhere.</returns>
		public static int Deposit(KingdomSystem System, Zone Z, KingdomSurvey Survey, string CropBlueprint, int Amount, out int Delivered, out int Pending)
		{
			Delivered = 0;
			Pending = 0;
			if (System == null || Survey == null || Amount <= 0 || string.IsNullOrEmpty(CropBlueprint))
			{
				return 0;
			}
			Delivered = Survey.StoreFood(Amount, CropBlueprint);
			int left = Amount - Delivered;
			if (left <= 0)
			{
				return 0;
			}
			int elsewhere = LarderRoomElsewhere(System, Z) - System.PendingCrop;
			if (elsewhere > 0)
			{
				Pending = (left < elsewhere) ? left : elsewhere;
				// One crop at a time on the road. A second harvest of a different crop arriving
				// while the first is still in flight travels as the first: the load is servings,
				// and what it physically is was decided when it left.
				if (System.PendingCrop <= 0 || string.IsNullOrEmpty(System.PendingCropBlueprint))
				{
					System.PendingCropBlueprint = CropBlueprint;
					// Where it left from, so the carrier who renders it walks in by the edge that
					// faces the field rather than by whichever wall is nearest the code (§3.7).
					System.PendingCropZoneId = (Z != null) ? Z.ZoneID : null;
				}
				System.PendingCrop += Pending;
				left -= Pending;
			}
			return left;
		}

		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		/// <summary>The finished field under this cell, or null. A field is a rect, so the
		/// founder may be standing anywhere in its footprint rather than on the one cell the
		/// building object occupies.</summary>
		public static GameObject FieldUnder(Zone Z, Cell C)
		{
			if (Z == null || C == null)
			{
				return null;
			}
			GameObject best = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (FieldOf(item) == null)
				{
					continue;
				}
				Cell at = item.CurrentCell;
				if (at != null && at.X == C.X && at.Y == C.Y)
				{
					return item;
				}
				KingdomPlotRules.PlotRect rect;
				if (best == null && KingdomPlots.TryReadFootprint(item, out rect) && rect.Contains(C.X, C.Y))
				{
					best = item;
				}
			}
			return best;
		}

		/// <summary>What the founder calls a crop, off its own blueprint rather than out of a
		/// second table that could disagree with it.</summary>
		public static string CropName(string CropBlueprint)
		{
			if (string.IsNullOrEmpty(CropBlueprint))
			{
				return "the crop";
			}
			GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(CropBlueprint);
			string name = (blueprint == null) ? null : blueprint.DisplayName();
			return string.IsNullOrEmpty(name) ? CropBlueprint : name;
		}

		/// <summary>A tick as an int property can hold it. Clamped rather than wrapped, for the
		/// reason <c>KingdomSubsidence.SeenStamp</c> clamps: a game that somehow outruns the slot
		/// stops dating rather than reading as the future.</summary>
		public static int StampOf(long TimeTicks)
		{
			if (TimeTicks <= 0L)
			{
				return 0;
			}
			return (TimeTicks >= int.MaxValue) ? int.MaxValue : (int)TimeTicks;
		}

		/// <summary>
		/// Says a field's want once and unsays it when the block lifts (STANDARDS 7b). The flag is
		/// the want itself rather than a bare bool, so a field that stops wanting hands and starts
		/// wanting a larder says the new thing instead of staying silent.
		/// </summary>
		public static void Announce(KingdomSystem System, GameObject Work, KingdomCropRules.FieldWant Want)
		{
			if (System == null || Work == null)
			{
				return;
			}
			if (Want == KingdomCropRules.FieldWant.None)
			{
				Work.SetIntProperty(SaidProperty, 0);
				return;
			}
			if (Work.GetIntProperty(SaidProperty) == (int)Want)
			{
				return;
			}
			Work.SetIntProperty(SaidProperty, (int)Want);
			string line = KingdomCropRules.WantNote(Want, Work.ShortDisplayName, KingdomPresentation.Rich(System.KingdomDisplayName));
			System.Ledger.Note("{{r|" + line + "}}");
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
		}
	}
}

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; everything
// they do lives in ThousandAndFirst.KingdomCrops above.
namespace XRL.World.Parts
{
	/// <summary>
	/// Carried by every seed item. Offers the one thing a seed is for.
	/// </summary>
	[Serializable]
	public class r_KingdomSeed : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Sow", "sow in a field", "r_SowSeed", null, 's', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_SowSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomCrops.AttemptSow(E.Actor, E.Item ?? ParentObject);
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// Merged onto the vanilla wild plants whose species the settlement grows, so a founder who
	/// walks past a watervine can start a farm with what the marsh already offered. One plant is
	/// one seed, forever; a plant somebody owns gives nothing.
	/// </summary>
	[Serializable]
	public class r_KingdomWildSeed : IPart
	{
		/// <summary>The seed blueprint this species carries. Declared in XML beside the part, so
		/// the map from plant to seed is data rather than a switch nobody can extend.</summary>
		public string Seed;

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (!string.IsNullOrEmpty(Seed) && ParentObject.GetIntProperty(ThousandAndFirst.KingdomCrops.WildSeedTakenProperty) != 1)
			{
				E.AddAction("Gather Seed", "gather seed", "r_GatherWildSeed", null, 'g', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_GatherWildSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomCrops.TakeWildSeed(E.Actor, ParentObject, Seed);
			}
			return base.HandleEvent(E);
		}
	}
}
