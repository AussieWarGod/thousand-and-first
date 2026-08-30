using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		internal static List<GalleryCase> Cases()
		{
			List<GalleryCase> result = new List<GalleryCase>();
			IList<KingdomArchitectureMapping> mappings = KingdomArchitecture.InspectMappings();
			for (int m = 0; m < mappings.Count; m++)
			{
				KingdomArchitectureMapping mapping = mappings[m];
				IList<string> variants = mapping.VariantKeys;
				for (int v = 0; v < variants.Count; v++)
					for (int facing = 0; facing < 4; facing++)
						result.Add(new GalleryCase { Number = result.Count + 1, Mapping = mapping,
							Variant = variants[v], Facing = (ArchitectureFacing)facing });
			}
			return result;
		}

		internal static bool TryStage(Zone Zone, GalleryCase Case, int Total,
			out GameObject Owner, out string Receipt, out string Failure)
		{
			Owner = null;
			Receipt = null;
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitecture.Healthy)
				return Fail("The authored architecture catalogue is not healthy.", out Failure);
			if (!KingdomArchitecture.TryResolveVariant(Case.Mapping.BuildKey,
				Case.Mapping.TypeKey, Case.Mapping.LotSize, Case.Variant, Case.Facing,
				out snapshot, out Failure)) return false;
			int width;
			int height;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out width, out height))
				return Fail("The selected pose has impossible world dimensions.", out Failure);
			KingdomPlotRules.PlotRect rect;
			if (!TryFindCanvas(Zone, width, height, out rect, out Failure)) return false;
			string encoded;
			string hash;
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(snapshot, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(snapshot, out hash, out Failure)
				|| !KingdomArchitectureRules.TryToWorld(rect.X1, rect.Y1, snapshot.Width,
					snapshot.Height, snapshot.Facing, snapshot.MainX, snapshot.MainY,
					out mainX, out mainY)) return false;
			KingdomArchitectureIntent intent = KingdomArchitectureIntent.Create(snapshot, encoded,
				hash, rect, mainX, mainY);
			if (!KingdomArchitectureRuntime.TryValidate(intent, out Failure)) return false;
			Receipt = ReceiptFor(Case, Total, hash);
			string lot = "taf-gallery-" + Receipt + "-" + Guid.NewGuid().ToString("N");
			GameObject synthetic = null;
			GameObject works = null;
			GameObject final = null;
			try
			{
				if (!TryCreateSyntheticAuthority(Zone, snapshot, intent, Receipt,
					out synthetic, out Failure)) return false;
				works = GameObject.Create(WorksBlueprint);
				if (!GameObject.Validate(works))
					return Fail("The production plot-works blueprint created no gallery owner.", out Failure);
				StampGallery(works, Receipt, Case.Key);
				if (!KingdomArchitectureRuntime.TryFreeze(works, intent, out Failure)
					|| !KingdomArchitectureStamper.TryInitializeOwner(works, intent, lot, out Failure))
					return false;
				Cell main = Zone.GetCell(mainX, mainY);
				GameObject accepted = main == null ? null : main.AddObject(works, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, works)
					|| !ReferenceEquals(works.CurrentCell, main) || works.InInventory != null)
					return Fail("The engine refused, replaced, or displaced the exact gallery plot-works owner.", out Failure);
				if (!KingdomArchitectureStamper.TryStageLayer(works, Zone,
					ArchitectureLayer.Ground, out Failure)
					|| !KingdomArchitectureStamper.TryStageLayer(works, Zone,
						ArchitectureLayer.Structure, out Failure)
					|| !KingdomArchitectureStamper.TryStageLayer(works, Zone,
						ArchitectureLayer.Object, out Failure)
					|| !KingdomArchitectureStamper.TryVerifyComplete(works, Zone, out Failure)) return false;

				final = GameObject.Create(Case.Mapping.BuildingBlueprint);
				if (!GameObject.Validate(final) || final.Blueprint != Case.Mapping.BuildingBlueprint)
					return Fail("The production behavior-root blueprint created no exact object.", out Failure);
				// The engine assigns ids lazily and observation paths (realized capture) are
				// forbidden to mint identity, so the STAGING side - the one lawful writer -
				// assigns the owner's durable id at creation. Proven live 2026-08-30: capture
				// refused the first staged owner for carrying no assigned identity.
				_ = final.ID;
				StampGallery(final, Receipt, Case.Key);
				final.DisplayName = "gallery: " + Case.Mapping.BuildKey;
				if (!KingdomArchitectureStamper.TryCopyFrozenOwner(works, final, out Failure)) return false;
				accepted = main.AddObject(final, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, final)
					|| !ReferenceEquals(final.CurrentCell, main) || final.InInventory != null)
					return Fail("The engine refused, replaced, or displaced the exact gallery behavior root.", out Failure);
				final.MakeActive();
				if (!KingdomArchitectureStamper.TryVerifyComplete(final, Zone, out Failure)) return false;
				if (!works.Destroy(null, Silent: true) || GameObject.Validate(works))
					return Fail("The temporary production plot-works owner would not retire.", out Failure);
				works = null;
				if (!KingdomArchitectureStamper.TryVerifyComplete(final, Zone, out Failure)
					|| !StampExactGallerySet(final, Zone, snapshot, lot, Receipt, out Failure)) return false;
				final.SetIntProperty(GalleryNumberProperty, Case.Number);
				final.SetStringProperty(GalleryDigestProperty, hash);
				final.SetStringProperty(GalleryExpectedScreenshotProperty,
					ArchitectureScreenshot(Case.Number, Total));
				Owner = final;
				KingdomLog.Log("[TAF architecture-gallery] receipt=" + Receipt + " case=" + Case.Key
					+ " mod=" + ModVersion + " qud=" + XRLGame.CoreVersion + " snapshot=" + hash
					+ " zone=" + Zone.ZoneID + " rect=" + rect.X1 + "," + rect.Y1 + ","
					+ rect.X2 + "," + rect.Y2
					+ " economy=bypassed eligibility=not-asserted stage=complete");
				return true;
			}
			catch (Exception exception)
			{
				Failure = "Gallery staging threw: " + Bounded(exception.Message, MaxNoteChars);
				return false;
			}
			finally
			{
				if (Owner == null) RollBackCreated(Zone, lot, works, final, synthetic);
			}
		}
	}
}
