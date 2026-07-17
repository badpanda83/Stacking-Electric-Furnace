using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackingElectricFurnace", "badpanda83", "0.4.0")]
    [Description("Allows players with permission to stack electric furnaces by right clicking an existing electric furnace.")]
    public class StackingElectricFurnace : RustPlugin
    {
        private const string ElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab";
        private const string ElectricFurnaceShortPrefab = "electricfurnace.deployed";
        private const string ElectricFurnaceItemShortName = "electric.furnace";
        private const string UsePermission = "stackingelectricfurnace.use";
        private const float MaxUseDistance = 6f;
        private const float VerticalGap = 0.02f;
        private const string DeniedMessage = "You don't have permission to stack electric furnaces.";
        private const string NoItemMessage = "You need an electric furnace in your inventory to stack one.";
        private const string TooFarMessage = "You are too far away from that electric furnace.";
        private const string BlockedMessage = "There is not enough room to stack another electric furnace there.";

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !input.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                SendReply(player, DeniedMessage);
                return;
            }

            Item furnaceItem = player.inventory?.containerMain?.FindItemByItemName(ElectricFurnaceItemShortName)
                ?? player.inventory?.containerBelt?.FindItemByItemName(ElectricFurnaceItemShortName)
                ?? player.inventory?.containerWear?.FindItemByItemName(ElectricFurnaceItemShortName);

            if (furnaceItem == null)
            {
                SendReply(player, NoItemMessage);
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, MaxUseDistance))
            {
                return;
            }

            BaseEntity targetEntity = hit.GetEntity();
            if (targetEntity == null || !IsElectricFurnace(targetEntity))
            {
                return;
            }

            if (Vector3.Distance(player.transform.position, targetEntity.transform.position) > MaxUseDistance + 1f)
            {
                SendReply(player, TooFarMessage);
                return;
            }

            Bounds supportBounds = GetWorldBounds(targetEntity);
            Vector3 spawnPosition = new Vector3(
                targetEntity.transform.position.x,
                supportBounds.max.y + GetElectricFurnaceHalfHeight() + VerticalGap,
                targetEntity.transform.position.z
            );

            Quaternion spawnRotation = targetEntity.transform.rotation;

            if (Physics.CheckBox(spawnPosition, GetElectricFurnaceExtents(), spawnRotation, ~0, QueryTriggerInteraction.Ignore))
            {
                SendReply(player, BlockedMessage);
                return;
            }

            BaseEntity newFurnace = GameManager.server.CreateEntity(ElectricFurnacePrefab, spawnPosition, spawnRotation, true);
            if (newFurnace == null)
            {
                return;
            }

            newFurnace.OwnerID = player.userID;
            newFurnace.skinID = furnaceItem.skin;
            newFurnace.Spawn();

            furnaceItem.UseItem(1);
        }

        private bool IsElectricFurnace(BaseEntity entity)
        {
            string prefab = entity?.PrefabName;
            return !string.IsNullOrEmpty(prefab)
                && prefab.IndexOf(ElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Bounds GetWorldBounds(BaseEntity entity)
        {
            Collider collider = entity.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds : new Bounds(entity.transform.position, Vector3.zero);
        }

        private Vector3 GetElectricFurnaceExtents()
        {
            return new Vector3(0.55f, 0.7f, 0.55f);
        }

        private float GetElectricFurnaceHalfHeight()
        {
            return GetElectricFurnaceExtents().y;
        }
    }
}
