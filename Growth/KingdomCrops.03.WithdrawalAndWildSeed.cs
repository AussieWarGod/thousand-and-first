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
	public static partial class KingdomCrops
	{
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

	}
}
