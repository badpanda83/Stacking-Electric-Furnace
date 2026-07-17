using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackingElectricFurnace", "badpanda83", "0.3.0")]
    [Description("Allows players with permission to stack electric furnaces by right clicking while placing.")]
    public class StackingElectricFurnace : RustPlugin
    {
        private const string ElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab";
        private const string ElectricFurnaceShortPrefab = "electricfurnace.deployed";
        private const string UsePermission = "stackingelectricfurnace.use";
        private const float VerticalTolerance = 0.35f;
        private const float HorizontalTolerance = 0.75f;
        private const float MaxPlaceDistance = 6f;

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        private object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction, Construction.Target target)
        {
            if (!CanUseStacking(player, planner, construction))
            {
                return null;
            }

            BaseEntity supportEntity = target.entity;
            if (supportEntity == null || !IsElectricFurnace(supportEntity))
            {
                return null;
            }

            Bounds supportBounds = GetWorldBounds(supportEntity);
            if (!IsStackPlacement(target.position, supportEntity, supportBounds))
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

            if (!HasStackPermission(player) || !IsElectricFurnace(gameObject) || !IsRightClickOnly(player))
            {
                return null;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, MaxPlaceDistance))
            {
                return null;
            }

            BaseEntity supportEntity = hit.GetEntity();
            if (supportEntity == null || !IsElectricFurnace(supportEntity))
            {
                return null;
            }

            Bounds supportBounds = GetWorldBounds(supportEntity);
            if (!IsStackPlacement(hit.point, supportEntity, supportBounds))
            {
                return null;
            }

            NextTick(() => TrySnapToTop(gameObject, supportEntity, supportBounds));
            return true;
        }

        private bool CanUseStacking(BasePlayer player, Planner planner, Construction construction)
        {
            if (player == null || planner == null || construction == null)
            {
                return false;
            }

            return HasStackPermission(player)
                && IsElectricFurnace(construction)
                && IsRightClickOnly(player);
        }

        private bool HasStackPermission(BasePlayer player)
        {
            return player != null && permission.UserHasPermission(player.UserIDString, UsePermission);
        }

        private bool IsRightClickOnly(BasePlayer player)
        {
            if (player?.serverInput == null)
            {
                return false;
            }

            bool rightClick = player.serverInput.IsDown(BUTTON.FIRE_SECONDARY);
            bool leftClick = player.serverInput.IsDown(BUTTON.FIRE_PRIMARY);
            return rightClick && !leftClick;
        }

        private bool IsStackPlacement(Vector3 desiredPosition, BaseEntity supportEntity, Bounds supportBounds)
        {
            bool isAboveSupport = desiredPosition.y >= supportBounds.max.y - VerticalTolerance;
            bool isCentered = Mathf.Abs(desiredPosition.x - supportEntity.transform.position.x) <= HorizontalTolerance
                && Mathf.Abs(desiredPosition.z - supportEntity.transform.position.z) <= HorizontalTolerance;

            return isAboveSupport && isCentered;
        }

        private void TrySnapToTop(GameObject gameObject, BaseEntity supportEntity, Bounds supportBounds)
        {
            if (gameObject == null || supportEntity == null)
            {
                return;
            }

            Collider newCollider = gameObject.GetComponentInChildren<Collider>();
            if (newCollider == null)
            {
                return;
            }

            float newHalfHeight = newCollider.bounds.extents.y;
            gameObject.transform.position = new Vector3(
                supportEntity.transform.position.x,
                supportBounds.max.y + newHalfHeight,
                supportEntity.transform.position.z
            );
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
