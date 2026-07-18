using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StackingElectricFurnace", "badpanda83", "1.2.1")]
    [Description("Allows players with permission to stack electric furnaces and industrial electric furnaces up to two total.")]
    public class StackingElectricFurnace : RustPlugin
    {
        private const string ElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab";
        private const string IndustrialElectricFurnacePrefab = "assets/prefabs/deployable/playerioents/electricfurnace/skins/industrial_electric_furnace/industrial_electric_furnace.deployed.prefab";

        private const string ElectricFurnaceShortPrefab = "electricfurnace.deployed";
        private const string IndustrialElectricFurnaceShortPrefab = "industrial_electric_furnace.deployed";

        private const string ElectricFurnaceItemShortName = "electric.furnace";
        private const string IndustrialElectricFurnaceItemShortName = "industrial.electric.furnace";

        private const string UsePermission = "stackingelectricfurnace.use";
        private const BaseEntity.Flags StackedFlag = BaseEntity.Flags.Reserved1;
        private const string DataFileName = "StackingElectricFurnace";

        private readonly HashSet<ulong> _runtimeStackedEntityIds = new HashSet<ulong>();
        private readonly Dictionary<ulong, ulong> _topToBottom = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, ulong> _bottomToTop = new Dictionary<ulong, ulong>();

        private Configuration _config;
        private StoredData _data;

        private class Configuration
        {
            [JsonProperty("Building privilege required")]
            public bool BuildingPrivilegeRequired = false;

            [JsonProperty("Max use distance")]
            public float MaxUseDistance = 6f;

            [JsonProperty("Horizontal tolerance")]
            public float HorizontalTolerance = 0.15f;

            [JsonProperty("Max vertical support distance")]
            public float MaxVerticalSupportDistance = 2.0f;
        }

        private class StoredData
        {
            [JsonProperty("Stack pairs")]
            public List<StackPair> StackPairs = new List<StackPair>();
        }

        private class StackPair
        {
            [JsonProperty("Top entity ID")]
            public ulong TopEntityId;

            [JsonProperty("Bottom entity ID")]
            public ulong BottomEntityId;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>() ?? new Configuration();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            LoadData();
        }

        private void OnServerInitialized()
        {
            RebuildMappingsFromData();
        }

        private void Unload()
        {
            foreach (ulong entityId in _runtimeStackedEntityIds)
            {
                BaseEntity entity = FindEntity(entityId);
                if (entity == null || entity.IsDestroyed)
                {
                    continue;
                }

                entity.SetFlag(StackedFlag, false);
                entity.SendNetworkUpdateImmediate();
            }

            SaveData();
            _runtimeStackedEntityIds.Clear();
            _topToBottom.Clear();
            _bottomToTop.Clear();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to stack furnaces.",
                ["BuildingBlocked"] = "You must be building authorized to stack furnaces.",
                ["WrongItem"] = "Hold an electric furnace or industrial electric furnace to stack it.",
                ["TooFar"] = "You are too far away from that furnace.",
                ["MaxStack"] = "You can only stack furnaces two high.",
                ["Failed"] = "Failed to stack the furnace there."
            }, this);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !input.WasJustPressed(BUTTON.FIRE_SECONDARY))
            {
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, _config.MaxUseDistance))
            {
                return;
            }

            BaseEntity targetEntity = hit.GetEntity();
            if (targetEntity == null || !IsSupportedTargetFurnace(targetEntity))
            {
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                ReplyToPlayer(player, "NoPermission");
                return;
            }

            if (_config.BuildingPrivilegeRequired && !player.IsBuildingAuthed())
            {
                ReplyToPlayer(player, "BuildingBlocked");
                return;
            }

            Item activeItem = player.GetActiveItem();
            FurnaceDefinition selectedFurnace = GetFurnaceDefinition(activeItem?.info?.shortname);
            if (selectedFurnace == null)
            {
                ReplyToPlayer(player, "WrongItem");
                return;
            }

            if (Vector3.Distance(player.transform.position, targetEntity.transform.position) > _config.MaxUseDistance + 1f)
            {
                ReplyToPlayer(player, "TooFar");
                return;
            }

            if (targetEntity.net == null)
            {
                ReplyToPlayer(player, "Failed");
                return;
            }

            ulong bottomId = targetEntity.net.ID.Value;

            if (targetEntity.HasFlag(StackedFlag) || _bottomToTop.ContainsKey(bottomId))
            {
                ReplyToPlayer(player, "MaxStack");
                return;
            }

            Bounds targetBounds = GetWorldBounds(targetEntity);
            Vector3 spawnPosition = new Vector3(
                targetEntity.transform.position.x,
                targetBounds.max.y,
                targetEntity.transform.position.z
            );

            Quaternion spawnRotation = targetEntity.transform.rotation;

            BaseEntity newFurnace = GameManager.server.CreateEntity(selectedFurnace.PrefabPath, spawnPosition, spawnRotation, true);
            if (newFurnace == null)
            {
                ReplyToPlayer(player, "Failed");
                return;
            }

            newFurnace.OwnerID = player.userID;
            newFurnace.skinID = activeItem.skin;
            newFurnace.SetFlag(StackedFlag, true);
            newFurnace.Spawn();

            if (newFurnace.IsDestroyed || newFurnace.net == null)
            {
                ReplyToPlayer(player, "Failed");
                return;
            }

            ulong topId = newFurnace.net.ID.Value;

            _runtimeStackedEntityIds.Add(topId);
            _topToBottom[topId] = bottomId;
            _bottomToTop[bottomId] = topId;
            UpsertStackPair(topId, bottomId);
            SaveData();

            newFurnace.SendNetworkUpdateImmediate();
            activeItem.UseItem(1);
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            BaseEntity entity = networkable as BaseEntity;
            if (entity == null || entity.net == null || !IsSupportedTargetFurnace(entity))
            {
                return;
            }

            ulong entityId = entity.net.ID.Value;

            if (_topToBottom.TryGetValue(entityId, out ulong bottomId))
            {
                _topToBottom.Remove(entityId);
                _bottomToTop.Remove(bottomId);
                _runtimeStackedEntityIds.Remove(entityId);
                RemoveStackPair(entityId, bottomId);
                SaveData();
                return;
            }

            if (_bottomToTop.TryGetValue(entityId, out ulong topId))
            {
                _bottomToTop.Remove(entityId);
                _topToBottom.Remove(topId);
                _runtimeStackedEntityIds.Remove(topId);
                RemoveStackPair(topId, entityId);
                SaveData();

                BaseEntity topEntity = FindEntity(topId);
                if (topEntity != null && !topEntity.IsDestroyed)
                {
                    topEntity.Kill();
                }
            }
        }

        private object OnEntityGroundMissing(BaseEntity entity)
        {
            if (entity == null || entity.net == null || !IsSupportedTargetFurnace(entity) || !entity.HasFlag(StackedFlag))
            {
                return null;
            }

            ulong topId = entity.net.ID.Value;
            if (!_topToBottom.TryGetValue(topId, out ulong bottomId))
            {
                entity.Kill();
                return true;
            }

            BaseEntity bottomEntity = FindEntity(bottomId);
            if (bottomEntity == null || bottomEntity.IsDestroyed || !IsSupportedTargetFurnace(bottomEntity))
            {
                entity.Kill();
                return true;
            }

            if (IsSupportedByTrackedBottom(entity, bottomEntity))
            {
                return true;
            }

            entity.Kill();
            return true;
        }

        private bool IsSupportedByTrackedBottom(BaseEntity topEntity, BaseEntity bottomEntity)
        {
            Vector3 topPos = topEntity.transform.position;
            Vector3 bottomPos = bottomEntity.transform.position;

            Vector2 topXZ = new Vector2(topPos.x, topPos.z);
            Vector2 bottomXZ = new Vector2(bottomPos.x, bottomPos.z);

            if (Vector2.Distance(topXZ, bottomXZ) > _config.HorizontalTolerance)
            {
                return false;
            }

            float verticalDifference = topPos.y - bottomPos.y;
            return verticalDifference > 0f && verticalDifference <= _config.MaxVerticalSupportDistance;
        }

        private void RebuildMappingsFromData()
        {
            _runtimeStackedEntityIds.Clear();
            _topToBottom.Clear();
            _bottomToTop.Clear();

            if (_data == null || _data.StackPairs == null)
            {
                _data = new StoredData();
                return;
            }

            List<StackPair> validPairs = new List<StackPair>();

            for (int i = 0; i < _data.StackPairs.Count; i++)
            {
                StackPair pair = _data.StackPairs[i];
                BaseEntity topEntity = FindEntity(pair.TopEntityId);
                BaseEntity bottomEntity = FindEntity(pair.BottomEntityId);

                if (topEntity == null || topEntity.IsDestroyed || !IsSupportedTargetFurnace(topEntity))
                {
                    continue;
                }

                if (bottomEntity == null || bottomEntity.IsDestroyed || !IsSupportedTargetFurnace(bottomEntity))
                {
                    topEntity.SetFlag(StackedFlag, false);
                    topEntity.SendNetworkUpdateImmediate();
                    continue;
                }

                topEntity.SetFlag(StackedFlag, true);
                topEntity.SendNetworkUpdateImmediate();

                _runtimeStackedEntityIds.Add(pair.TopEntityId);
                _topToBottom[pair.TopEntityId] = pair.BottomEntityId;
                _bottomToTop[pair.BottomEntityId] = pair.TopEntityId;
                validPairs.Add(pair);
            }

            _data.StackPairs = validPairs;
            SaveData();
        }

        private void UpsertStackPair(ulong topId, ulong bottomId)
        {
            if (_data == null)
            {
                _data = new StoredData();
            }

            for (int i = 0; i < _data.StackPairs.Count; i++)
            {
                StackPair pair = _data.StackPairs[i];
                if (pair.TopEntityId == topId || pair.BottomEntityId == bottomId)
                {
                    pair.TopEntityId = topId;
                    pair.BottomEntityId = bottomId;
                    return;
                }
            }

            _data.StackPairs.Add(new StackPair
            {
                TopEntityId = topId,
                BottomEntityId = bottomId
            });
        }

        private void RemoveStackPair(ulong topId, ulong bottomId)
        {
            if (_data == null || _data.StackPairs == null)
            {
                return;
            }

            _data.StackPairs.RemoveAll(pair => pair.TopEntityId == topId || pair.BottomEntityId == bottomId);
        }

        private void LoadData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(DataFileName) ?? new StoredData();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, _data);
        }

        private BaseEntity FindEntity(ulong entityId)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(entityId)) as BaseEntity;
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
                    PrefabPath = ElectricFurnacePrefab
                };
            }

            if (string.Equals(itemShortName, IndustrialElectricFurnaceItemShortName, System.StringComparison.OrdinalIgnoreCase))
            {
                return new FurnaceDefinition
                {
                    PrefabPath = IndustrialElectricFurnacePrefab
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

        private void ReplyToPlayer(BasePlayer player, string key)
        {
            SendReply(player, lang.GetMessage(key, this, player.UserIDString));
        }

        private class FurnaceDefinition
        {
            public string PrefabPath;
        }
    }
}
