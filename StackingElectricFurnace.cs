using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackingElectricFurnace", "badpanda83", "0.7.0")]
    [Description("Allows players with permission to stack electric and industrial electric furnaces by right clicking an existing furnace with the desired furnace selected.")]
    public class StackingElectricFurnace : RustPlugin
    {
        private const string ElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab";
        private const string IndustrialElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/skins/industrial_electric_furnace/industrial_electric_furnace.deployed.prefab";

        private const string ElectricFurnaceShortPrefab = "electricfurnace.deployed";
        private const string IndustrialElectricFurnaceShortPrefab = "industrial_electric_furnace.deployed";

        private const string ElectricFurnaceItemShortName = "electric.furnace";
        private const string IndustrialElectricFurnaceItemShortName = "industrial.electric.furnace";

        private const string UsePermission = "stackingelectricfurnace.use";
        private const float MaxUseDistance = 6f;
        private const float VerticalGap = 0.0f;

        private const string DeniedMessage = "You don't have permission to stack furnaces.";
        private const string WrongItemMessage = "Hold an electric furnace or industrial electric furnace to stack it.";
        private const string TooFarMessage = "You are too far away from that furnace.";
        private const string FailedMessage = "Failed to stack the furnace there.";

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

            HeldEntity heldEntity = player.GetHeldEntity();
            Item heldItem = heldEntity?.GetItem();
            FurnaceDefinition selectedFurnace = GetFurnaceDefinition(heldItem?.info?.shortname);
            if (selectedFurnace == null)
            {
                SendReply(player, WrongItemMessage);
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, MaxUseDistance))
            {
                return;
            }

            BaseEntity targetEntity = hit.GetEntity();
            if (targetEntity == null || !IsSupportedTargetFurnace(targetEntity))
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
                supportBounds.max.y + selectedFurnace.HeightOffset + VerticalGap,
                targetEntity.transform.position.z
            );

            Quaternion spawnRotation = targetEntity.transform.rotation;

            Puts($"Attempting stack spawn for {selectedFurnace.ItemShortName} at {spawnPosition} on top of {targetEntity.PrefabName} for player {player.displayName}");

            BaseEntity newFurnace = GameManager.server.CreateEntity(selectedFurnace.PrefabPath, spawnPosition, spawnRotation, true);
            if (newFurnace == null)
            {
                SendReply(player, FailedMessage);
                Puts($"CreateEntity returned null while stacking {selectedFurnace.ItemShortName} using prefab {selectedFurnace.PrefabPath}");
                return;
            }

            newFurnace.OwnerID = player.userID;
            if (heldItem != null)
            {
                newFurnace.skinID = heldItem.skin;
            }

            newFurnace.Spawn();

            if (newFurnace.IsDestroyed)
            {
                SendReply(player, FailedMessage);
                Puts($"Spawned {selectedFurnace.ItemShortName} was immediately destroyed after spawn.");
                return;
            }

            CopyGrounding(targetEntity, newFurnace);
            UpdateBuildingPrivilege(targetEntity, newFurnace);
            newFurnace.SendNetworkUpdateImmediate();

            heldItem.UseItem(1);
            Puts($"Successfully stacked {selectedFurnace.ItemShortName} for {player.displayName} at {spawnPosition}");
        }

        private FurnaceDefinition GetFurnaceDefinition(string itemShortName)
        {
            if (string.IsNullOrEmpty(itemShortName))
            {
                return null;
            }

            if (string.Equals(itemShortName, ElectricFurnaceItemShortName, System.StringComparison.OrdinalIgnoreCase))
            {
                return new FurnaceDefinition
                {
                    PrefabPath = ElectricFurnacePrefab,
                    ItemShortName = ElectricFurnaceItemShortName,
                    HeightOffset = 0.0f
                };
            }

            if (string.Equals(itemShortName, IndustrialElectricFurnaceItemShortName, System.StringComparison.OrdinalIgnoreCase))
            {
                return new FurnaceDefinition
                {
                    PrefabPath = IndustrialElectricFurnacePrefab,
                    ItemShortName = IndustrialElectricFurnaceItemShortName,
                    HeightOffset = 0.0f
                };
            }

            return null;
        }

        private bool IsSupportedTargetFurnace(BaseEntity entity)
        {
            string prefab = entity?.PrefabName;
            if (string.IsNullOrEmpty(prefab))
            {
                return false;
            }

            return prefab.IndexOf(ElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0
                || prefab.IndexOf(IndustrialElectricFurnaceShortPrefab, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Bounds GetWorldBounds(BaseEntity entity)
        {
            Collider collider = entity.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds : new Bounds(entity.transform.position, Vector3.zero);
        }

        private void CopyGrounding(BaseEntity source, BaseEntity target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.SetParent(source.GetParentEntity(), true, true);
            target.groundEntity = source.groundEntity;
            target.transform.hasChanged = true;
        }

        private void UpdateBuildingPrivilege(BaseEntity source, BaseEntity target)
        {
            if (source == null || target == null)
            {
                return;
            }

            BuildingManager.Building sourceBuilding = source.GetBuilding();
            if (sourceBuilding != null)
            {
                target.AttachToBuilding(sourceBuilding);
            }
            else
            {
                target.UpdateNetworkGroup();
            }
        }

        private class FurnaceDefinition
        {
            public string PrefabPath;
            public string ItemShortName;
            public float HeightOffset;
        }
    }
}
