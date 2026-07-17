using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackingElectricFurnace", "badpanda83", "0.1.0")]
    [Description("Allows electric furnaces to be stacked vertically by relaxing placement checks when aiming at another electric furnace.")]
    public class StackingElectricFurnace : RustPlugin
    {
        private const string ElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab";
        private const string ElectricFurnaceShortPrefab = "electricfurnace.deployed";
        private const float VerticalTolerance = 0.15f;
        private const float HorizontalTolerance = 0.2f;

        private object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction, Construction.Target target)
        {
            if (player == null || planner == null || construction == null)
            {
                return null;
            }

            if (!IsElectricFurnace(construction))
            {
                return null;
            }

            BaseEntity supportEntity = target.entity;
            if (supportEntity == null || !IsElectricFurnace(supportEntity))
            {
                return null;
            }

            Vector3 desiredPosition = target.position;
            Bounds supportBounds = GetWorldBounds(supportEntity);
            float topY = supportBounds.max.y;
            bool isAboveSupport = desiredPosition.y >= topY - VerticalTolerance;
            bool isCentered = Mathf.Abs(desiredPosition.x - supportEntity.transform.position.x) <= HorizontalTolerance
                && Mathf.Abs(desiredPosition.z - supportEntity.transform.position.z) <= HorizontalTolerance;

            if (!isAboveSupport || !isCentered)
            {
                return null;
            }

            return true;
        }

        private object CanPlaceEntity(BasePlayer player, Planner planner, GameObject gameObject)
        {
            if (player == null || planner == null || gameObject == null)
            {
                return null;
            }

            if (!IsElectricFurnace(gameObject))
            {
                return null;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 6f))
            {
                return null;
            }

            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity == null || !IsElectricFurnace(hitEntity))
            {
                return null;
            }

            Bounds supportBounds = GetWorldBounds(hitEntity);
            bool isAboveSupport = hit.point.y >= supportBounds.max.y - VerticalTolerance;
            bool isCentered = Mathf.Abs(hit.point.x - hitEntity.transform.position.x) <= HorizontalTolerance
                && Mathf.Abs(hit.point.z - hitEntity.transform.position.z) <= HorizontalTolerance;

            if (!isAboveSupport || !isCentered)
            {
                return null;
            }

            NextTick(() => TrySnapToTop(player, gameObject, hitEntity, supportBounds));
            return true;
        }

        private void TrySnapToTop(BasePlayer player, GameObject gameObject, BaseEntity supportEntity, Bounds supportBounds)
        {
            if (player == null || gameObject == null || supportEntity == null)
            {
                return;
            }

            Collider newCollider = gameObject.GetComponentInChildren<Collider>();
            if (newCollider == null)
            {
                return;
            }

            Bounds newBounds = newCollider.bounds;
            Vector3 currentPosition = gameObject.transform.position;
            float newHalfHeight = newBounds.extents.y;

            Vector3 snappedPosition = new Vector3(
                supportEntity.transform.position.x,
                supportBounds.max.y + newHalfHeight,
                supportEntity.transform.position.z
            );

            gameObject.transform.position = snappedPosition;
            gameObject.transform.rotation = Quaternion.identity;
        }

        private bool IsElectricFurnace(Construction construction)
        {
            string prefab = construction?.fullName;
            return !string.IsNullOrEmpty(prefab)
                && (prefab.IndexOf(ElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || prefab.Equals(ElectricFurnacePrefab, System.StringComparison.OrdinalIgnoreCase));
        }

        private bool IsElectricFurnace(BaseEntity entity)
        {
            string prefab = entity?.PrefabName;
            return !string.IsNullOrEmpty(prefab)
                && prefab.IndexOf(ElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsElectricFurnace(GameObject gameObject)
        {
            string name = gameObject?.name;
            return !string.IsNullOrEmpty(name)
                && name.IndexOf(ElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Bounds GetWorldBounds(BaseEntity entity)
        {
            Collider collider = entity.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds : new Bounds(entity.transform.position, Vector3.zero);
        }
    }
}
