using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
#if !TAF_TESTS
		private sealed partial class ZoneHost
		{
			private static string LotFor(KingdomInheritBuildSpec Spec)
			{
				return "inherit-" + Spec.Index.ToString(CultureInfo.InvariantCulture) + "-"
					+ Spec.ArchitectureHash.Substring(0, 16);
			}

			private static bool CanStampArchitecture(KingdomInheritBuildSpec Spec)
			{
				if (Spec == null || !Spec.IsArchitecture
					|| !SafeInheritedBlueprint(Spec.Blueprint)) return false;
				ArchitectureLayoutSnapshot snapshot;
				if (!KingdomArchitectureRules.TryDecodeSnapshot(Spec.ArchitectureSnapshot,
					out snapshot, out _)) return false;
				for (int i = 0; i < snapshot.Placements.Count; i++)
					if (!SafeInheritedBlueprint(snapshot.Placements[i].Blueprint))
						return false;
				return true;
			}

			private static bool SafeInheritedBlueprint(string Blueprint)
			{
				GameObjectBlueprint blueprint = GameObjectFactory.Factory.GetBlueprintIfExists(
					Blueprint);
				return blueprint != null && !blueprint.HasPart("Brain")
					&& (!blueprint.HasPart("Physics")
						|| !blueprint.GetPartParameter("Physics", "Takeable", false));
			}

			private static bool TryArchitectureIntent(KingdomInheritBuildSpec Spec,
				out KingdomArchitectureIntent Intent, out string Failure)
			{
				Intent = null;
				Failure = "";
				ArchitectureLayoutSnapshot snapshot;
				KingdomInheritanceSpatialRules.Rect rect;
				if (Spec == null || !Spec.IsArchitecture
					|| !KingdomArchitectureRules.TryDecodeSnapshot(Spec.ArchitectureSnapshot,
						out snapshot, out Failure)
					|| !KingdomInheritanceSpatialRules.TrySnapshotRect(snapshot, Spec.X, Spec.Y,
						out rect)) return false;
				Intent = KingdomArchitectureIntent.CreateRaw(KingdomArchitectureRuntime.ReceiptSchema,
					snapshot.BuildKey, snapshot.PlanKey, snapshot.BindingKey, snapshot.TierKey,
					snapshot.VariantKey, snapshot.PaletteKey, snapshot.LotType, snapshot.LotSize,
					snapshot.Facing, Spec.ArchitectureSnapshot, Spec.ArchitectureHash,
					new KingdomPlotRules.PlotRect(rect.X1, rect.Y1, rect.X2, rect.Y2),
					Spec.X, Spec.Y);
				return KingdomArchitectureRuntime.TryValidate(Intent, out Failure);
			}

			private bool ExactArchitecture(GameObject Root, KingdomInheritBuildSpec Spec)
			{
				KingdomArchitectureIntent intent;
				ArchitectureLayoutSnapshot snapshot;
				string lot;
				return KingdomArchitectureStamper.TryReadOwner(Root, out intent, out snapshot,
					out lot, out _) && intent.EncodedSnapshot == Spec.ArchitectureSnapshot
					&& intent.SnapshotHash == Spec.ArchitectureHash && lot == LotFor(Spec)
					&& KingdomArchitectureStamper.TryVerifyComplete(Root, Zone, out _);
			}

				private bool ScrubArchitecture(GameObject Root, out string Failure)
			{
				Failure = "";
				KingdomArchitectureIntent intent;
				ArchitectureLayoutSnapshot snapshot;
				string lot;
				if (!KingdomArchitectureStamper.TryReadOwner(Root, out intent, out snapshot,
					out lot, out Failure)) return false;
					KingdomInheritWorkState state = (KingdomInheritWorkState)Root.GetIntProperty(
						ObjectStateProperty, -1);
					int condition = Root.GetIntProperty(ObjectConditionProperty, -1);
					int count = 0;
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item)
						|| item.GetStringProperty(KingdomPlots.PlotIdProperty) != lot
						|| item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
							!= KingdomArchitectureStamper.ComponentSchema) continue;
						Scrub(item);
						item.SetIntProperty(ObjectStateProperty, (int)state);
						item.SetIntProperty(ObjectConditionProperty, condition);
						item.SetIntProperty(ObjectFreshEmptyProperty, 1);
						if (item.baseHitpoints > 0)
							item.hitpoints = Math.Max(1, item.baseHitpoints * condition / 100);
						ArchitectureLayer layer = (ArchitectureLayer)item.GetIntProperty(
							KingdomArchitectureStamper.ComponentLayerProperty, -1);
						if (KingdomInheritanceFabricRules.MarksComponent(state, condition, layer,
							intent.SnapshotHash, item.GetStringProperty(
								KingdomArchitectureStamper.ComponentSlotProperty)))
							item.RequirePart<r_KingdomInheritedFabric>();
						count++;
				}
				if (count != snapshot.Placements.Count)
				{
					Failure = "the same stamper did not publish every frozen inherited component";
					return false;
				}
				return true;
			}

			private bool EmptyArchitecture(GameObject Root)
			{
				KingdomArchitectureIntent intent;
				ArchitectureLayoutSnapshot snapshot;
				string lot;
				if (!KingdomArchitectureStamper.TryReadOwner(Root, out intent, out snapshot,
					out lot, out _) || !KingdomArchitectureStamper.TryVerifyComplete(Root, Zone,
						out _)) return false;
				int count = 0;
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item)
						|| item.GetStringProperty(KingdomPlots.PlotIdProperty) != lot
						|| item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
							!= KingdomArchitectureStamper.ComponentSchema) continue;
					if (item.GetIntProperty(ObjectFreshEmptyProperty) != 1
						|| !IsEmptyObject(item)) return false;
					count++;
				}
				return count == snapshot.Placements.Count;
			}

			private bool DiscardArchitecture(GameObject Root)
			{
				if (Root == null || !Root.HasIntProperty(KingdomArchitectureStamper.SchemaProperty))
					return true;
				// TryStageLayer quarantines a failed owner, so its strict reader deliberately
				// refuses it. Rollback still uses the schema-first raw lot/hash pair written while
				// detached; both are unique to this inherited spec and every component repeats them.
				string lot = Root.GetStringProperty(KingdomArchitectureStamper.LotIdProperty);
				string hash = Root.GetStringProperty(KingdomArchitectureStamper.HashProperty);
				if (string.IsNullOrEmpty(lot) || string.IsNullOrEmpty(hash)) return false;
				List<GameObject> owned = new List<GameObject>();
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
					if (GameObject.Validate(objects[i])
						&& objects[i].GetStringProperty(KingdomPlots.PlotIdProperty) == lot
						&& objects[i].GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
							== KingdomArchitectureStamper.ComponentSchema
						&& objects[i].GetStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
							== hash) owned.Add(objects[i]);
				for (int i = owned.Count - 1; i >= 0; i--)
					owned[i].Obliterate(null, Silent: true);
				objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
					if (GameObject.Validate(objects[i])
						&& objects[i].GetStringProperty(KingdomPlots.PlotIdProperty) == lot
						&& objects[i].GetIntProperty(
							KingdomArchitectureStamper.ComponentSchemaProperty)
							== KingdomArchitectureStamper.ComponentSchema
						&& objects[i].GetStringProperty(
							KingdomArchitectureStamper.ComponentHashProperty) == hash) return false;
				return true;
			}
		}
#endif

	}
}
