using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace SiegeGame.Editor
{
    public static class UnitPrefabGenerator
    {
        private const string StandardAnimDir = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/animation";
        private const string CavalryAnimDir = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/animation_cavalry";
        private const string BasePrefabsDir = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/prefabs";
        private const string AnimControllerDir = "Assets/AnimationController";

        private const string OutputCharactersDir = "Assets/prefabs/characters";
        private const string OutputPrefabsDir = "Assets/prefabs";
        private const string OutputPowerupsDir = "Assets/Resources/Powerups";

        private const string CharacterAvatarPath = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/units/TT_RTS_Characters_customizable.FBX";
        private const string CavalryAvatarPath = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/units/TT_RTS_Cavalry_customizable.FBX";
        private const string CartAvatarPath = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/units/machines/TT_RTS_Cart.FBX";

        private const string HealthBarPrefabPath = "Assets/prefabs/healthbar.prefab";
        private const string ArrowPrefabPath = "Assets/prefabs/Arrow_B.prefab";
        private const string BlueTexturePath = "Assets/Resources/Units/TT_RTS_Units_blueB.tga";
        private const string RedTexturePath = "Assets/Resources/Units/TT_RTS_Units_red.tga";

        private const string SwordAudioPath = "Assets/audioMohamad/dragon-studio-sword-slice-393847.mp3";
        private const string HammerAudioPath = "Assets/audioMohamad/dragon-studio-hammer-smash-effect-382731.mp3";
        private const string ArrowAudioPath = "Assets/audioMohamad/arrowrelease.mp3";

        private static readonly string[] ScreamAudioPaths = new string[]
        {
            "Assets/audioMohamad/nhlk-revolution-108386.mp3",
            "Assets/audioMohamad/universfield-epic-war-combat-scream-352707.mp3",
            "Assets/audioMohamad/universfield-man-fear-scream-244948.mp3",
            "Assets/audioMohamad/virtual_vibes-angry-man-yell-sound-hd-379386.mp3"
        };

        public class ControllerClips
        {
            public string idleFbx;
            public string runFbx;
            public string attackFbx;
            public string hurtFbx;
            public string deathFbx;
        }

        public class UnitConfig
        {
            public string prefabName;
            public string unitDisplayName;
            public PeasantUnitController.UnitCombatType combatType;
            public string controllerName; // filename in Assets/AnimationController (e.g. "players.controller")
            public bool isCavalry;
            public bool isSettler;
            public float moveSpeed = 3.5f;
            public float colliderRadius = 0.5f;
            public float colliderHeight = 1f;
            public Vector3 colliderCenter = new Vector3(0f, 0.5f, 0f);
            public float attackRange = 1.6f;
            public float detectionRange = 8f;
            public float attackDamage = 15f;
            public float maxHealth = 100f;
            public string attackAudio = SwordAudioPath;
            public string weaponKeyword = null; // keyword to match active weapon transform
        }

        private static readonly Dictionary<string, ControllerClips> ControllerDefinitions = new Dictionary<string, ControllerClips>
        {
            {
                "crossbow.controller", new ControllerClips
                {
                    idleFbx = StandardAnimDir + "/Crossbow/crossbow_01_idle.FBX",
                    runFbx = StandardAnimDir + "/Crossbow/crossbow_03_run.FBX",
                    attackFbx = StandardAnimDir + "/Crossbow/crossbow_04_attack_A.FBX",
                    hurtFbx = StandardAnimDir + "/Crossbow/crossbow_05_damage.FBX",
                    deathFbx = StandardAnimDir + "/Crossbow/crossbow_06_death_A.FBX"
                }
            },
            {
                "twohanded.controller", new ControllerClips
                {
                    idleFbx = StandardAnimDir + "/TwoHanded/twohanded_01_idle.FBX",
                    runFbx = StandardAnimDir + "/TwoHanded/twohanded_03_run.FBX",
                    attackFbx = StandardAnimDir + "/TwoHanded/twohanded_04_attack_A.FBX",
                    hurtFbx = StandardAnimDir + "/TwoHanded/twohanded_05_damage.FBX",
                    deathFbx = StandardAnimDir + "/TwoHanded/twohanded_06_death_A.FBX"
                }
            },
            {
                "spearman.controller", new ControllerClips
                {
                    idleFbx = StandardAnimDir + "/Spear/spear_01_idle.FBX",
                    runFbx = StandardAnimDir + "/Spear/spear_03_run.FBX",
                    attackFbx = StandardAnimDir + "/Spear/spear_04_attack_A.FBX",
                    hurtFbx = StandardAnimDir + "/Spear/spear_05_damage.FBX",
                    deathFbx = StandardAnimDir + "/Spear/spear_06_death_A.FBX"
                }
            },
            {
                "staff_caster.controller", new ControllerClips
                {
                    idleFbx = StandardAnimDir + "/Staff/staff_01_idle.FBX",
                    runFbx = StandardAnimDir + "/Staff/staff_03_run.FBX",
                    attackFbx = StandardAnimDir + "/Staff/staff_07_cast_A.FBX",
                    hurtFbx = StandardAnimDir + "/Staff/staff_05_damage.FBX",
                    deathFbx = StandardAnimDir + "/Staff/staff_06_death_A.FBX"
                }
            },
            {
                "cavalry_shield.controller", new ControllerClips
                {
                    idleFbx = CavalryAnimDir + "/cavalry_shield/cav_shield_01_idle.FBX",
                    runFbx = CavalryAnimDir + "/cavalry_shield/cav_shield_03_run.FBX",
                    attackFbx = CavalryAnimDir + "/cavalry_shield/cav_shield_04_attack.FBX",
                    hurtFbx = CavalryAnimDir + "/cavalry_shield/cav_shield_05_damage.FBX",
                    deathFbx = CavalryAnimDir + "/cavalry_shield/cav_shield_06_death_A.FBX"
                }
            },
            {
                "cavalry_spear.controller", new ControllerClips
                {
                    idleFbx = CavalryAnimDir + "/cavalry_spear/cav_spear_01_idle_A.FBX",
                    runFbx = CavalryAnimDir + "/cavalry_spear/cav_spear_03_run_A.FBX",
                    attackFbx = CavalryAnimDir + "/cavalry_spear/cav_spear_04_attack_A.FBX",
                    hurtFbx = CavalryAnimDir + "/cavalry_spear/cav_spear_05_take_damage_A.FBX",
                    deathFbx = CavalryAnimDir + "/cavalry_spear/cav_spear_06_death_A.FBX"
                }
            },
            {
                "cavalry_twohanded.controller", new ControllerClips
                {
                    idleFbx = CavalryAnimDir + "/cavalry/cavalry_01_idle.FBX",
                    runFbx = CavalryAnimDir + "/cavalry/cavalry_03_run.FBX",
                    attackFbx = CavalryAnimDir + "/cavalry/cavalry_04_attack.FBX",
                    hurtFbx = CavalryAnimDir + "/cavalry/cavalry_05_damage.FBX",
                    deathFbx = CavalryAnimDir + "/cavalry/cavalry_06_death_A.FBX"
                }
            },
            {
                "cavalry_archer.controller", new ControllerClips
                {
                    idleFbx = CavalryAnimDir + "/cavalry_archer/cav_archer_01_idle.FBX",
                    runFbx = CavalryAnimDir + "/cavalry_archer/cav_archer_03_run.FBX",
                    attackFbx = CavalryAnimDir + "/cavalry_archer/cav_archer_04_attack.FBX",
                    hurtFbx = CavalryAnimDir + "/cavalry_archer/cav_archer_05_damage.FBX",
                    deathFbx = CavalryAnimDir + "/cavalry_archer/cav_archer_06_death_A.FBX"
                }
            },
            {
                "cavalry_staff.controller", new ControllerClips
                {
                    idleFbx = CavalryAnimDir + "/cavalry_staff/cav_staff_01_idle.FBX",
                    runFbx = CavalryAnimDir + "/cavalry_staff/cav_staff_03_run.FBX",
                    attackFbx = CavalryAnimDir + "/cavalry_staff/cav_staff_07_cast_A.FBX",
                    hurtFbx = CavalryAnimDir + "/cavalry_staff/cav_staff_05_damage.FBX",
                    deathFbx = CavalryAnimDir + "/cavalry_staff/cav_staff_06_death_A.FBX"
                }
            },
            {
                "settler.controller", new ControllerClips
                {
                    idleFbx = StandardAnimDir + "/animation_machines/Cart/cart_01_idle.FBX",
                    runFbx = StandardAnimDir + "/animation_machines/Cart/cart_02_move.FBX",
                    attackFbx = StandardAnimDir + "/animation_machines/Cart/cart_01_idle.FBX",
                    hurtFbx = StandardAnimDir + "/animation_machines/Cart/cart_01_idle.FBX",
                    deathFbx = StandardAnimDir + "/animation_machines/Cart/cart_03_death.FBX"
                }
            }
        };

        private static readonly UnitConfig[] RemainingUnits = new UnitConfig[]
        {
            // 1. Commander
            new UnitConfig
            {
                prefabName = "TT_Commander",
                unitDisplayName = "Commander",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "players.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.6f,
                detectionRange = 8f,
                attackDamage = 18f,
                maxHealth = 120f,
                attackAudio = HammerAudioPath,
                weaponKeyword = "warhammer"
            },
            // 2. Crossbowman
            new UnitConfig
            {
                prefabName = "TT_Crossbowman",
                unitDisplayName = "Crossbowman",
                combatType = PeasantUnitController.UnitCombatType.Archer,
                controllerName = "crossbow.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 15f,
                detectionRange = 25f,
                attackDamage = 16f,
                maxHealth = 100f,
                attackAudio = ArrowAudioPath,
                weaponKeyword = "crossbow"
            },
            // 3. HeavySwordman
            new UnitConfig
            {
                prefabName = "TT_HeavySwordman",
                unitDisplayName = "Heavy Swordman",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "twohanded.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.3f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.8f,
                detectionRange = 8f,
                attackDamage = 22f,
                maxHealth = 120f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "TH_sword"
            },
            // 4. Heavy_Cavalry
            new UnitConfig
            {
                prefabName = "TT_Heavy_Cavalry",
                unitDisplayName = "Heavy Cavalry",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_shield.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 4.8f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.0f,
                detectionRange = 9f,
                attackDamage = 20f,
                maxHealth = 160f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "broad_sword"
            },
            // 5. Heavy_Infantry
            new UnitConfig
            {
                prefabName = "TT_Heavy_Infantry",
                unitDisplayName = "Heavy Infantry",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "players.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.3f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.6f,
                detectionRange = 8f,
                attackDamage = 16f,
                maxHealth = 130f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "broad_sword_B"
            },
            // 6. HighPriest
            new UnitConfig
            {
                prefabName = "TT_HighPriest",
                unitDisplayName = "High Priest",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "staff_caster.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.8f,
                detectionRange = 8f,
                attackDamage = 16f,
                maxHealth = 110f,
                attackAudio = HammerAudioPath,
                weaponKeyword = "mace"
            },
            // 7. King
            new UnitConfig
            {
                prefabName = "TT_King",
                unitDisplayName = "King",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "players.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.6f,
                detectionRange = 8f,
                attackDamage = 20f,
                maxHealth = 150f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "sword_B"
            },
            // 8. Knight
            new UnitConfig
            {
                prefabName = "TT_Knight",
                unitDisplayName = "Knight",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_spear.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 5.0f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.4f,
                detectionRange = 9f,
                attackDamage = 22f,
                maxHealth = 150f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "cav_lance"
            },
            // 9. Light_Cavalry
            new UnitConfig
            {
                prefabName = "TT_Light_Cavalry",
                unitDisplayName = "Light Cavalry",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_shield.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 5.3f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 1.8f,
                detectionRange = 9f,
                attackDamage = 16f,
                maxHealth = 120f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "sword"
            },
            // 10. Light_Infantry
            new UnitConfig
            {
                prefabName = "TT_Light_Infantry",
                unitDisplayName = "Light Infantry",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "players.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.8f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.6f,
                detectionRange = 8f,
                attackDamage = 14f,
                maxHealth = 95f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "sword"
            },
            // 11. Mage
            new UnitConfig
            {
                prefabName = "TT_Mage",
                unitDisplayName = "Mage",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "staff_caster.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.8f,
                detectionRange = 8f,
                attackDamage = 18f,
                maxHealth = 90f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "staff_B"
            },
            // 12. Mounted_King
            new UnitConfig
            {
                prefabName = "TT_Mounted_King",
                unitDisplayName = "Mounted King",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_shield.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 5.0f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.0f,
                detectionRange = 9f,
                attackDamage = 24f,
                maxHealth = 180f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "sword_B"
            },
            // 13. Mounted_Knight
            new UnitConfig
            {
                prefabName = "TT_Mounted_Knight",
                unitDisplayName = "Mounted Knight",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_spear.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 5.0f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.4f,
                detectionRange = 9f,
                attackDamage = 22f,
                maxHealth = 160f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "cav_lance"
            },
            // 14. Mounted_Mage
            new UnitConfig
            {
                prefabName = "TT_Mounted_Mage",
                unitDisplayName = "Mounted Mage",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_staff.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 4.8f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.0f,
                detectionRange = 9f,
                attackDamage = 20f,
                maxHealth = 130f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "staff_D"
            },
            // 15. Mounted_Paladin
            new UnitConfig
            {
                prefabName = "TT_Mounted_Paladin",
                unitDisplayName = "Mounted Paladin",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_twohanded.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 4.8f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.0f,
                detectionRange = 9f,
                attackDamage = 24f,
                maxHealth = 160f,
                attackAudio = HammerAudioPath,
                weaponKeyword = "TH_warhammer"
            },
            // 16. Mounted_Priest
            new UnitConfig
            {
                prefabName = "TT_Mounted_Priest",
                unitDisplayName = "Mounted Priest",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "cavalry_staff.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 4.8f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 2.0f,
                detectionRange = 9f,
                attackDamage = 18f,
                maxHealth = 130f,
                attackAudio = HammerAudioPath,
                weaponKeyword = "mace_B"
            },
            // 17. Mounted_Scout
            new UnitConfig
            {
                prefabName = "TT_Mounted_Scout",
                unitDisplayName = "Mounted Scout",
                combatType = PeasantUnitController.UnitCombatType.Archer,
                controllerName = "cavalry_archer.controller",
                isCavalry = true,
                isSettler = false,
                moveSpeed = 5.4f,
                colliderRadius = 0.8f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 15f,
                detectionRange = 25f,
                attackDamage = 15f,
                maxHealth = 120f,
                attackAudio = ArrowAudioPath,
                weaponKeyword = "short_bow"
            },
            // 18. Paladin
            new UnitConfig
            {
                prefabName = "TT_Paladin",
                unitDisplayName = "Paladin",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "twohanded.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.3f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.8f,
                detectionRange = 8f,
                attackDamage = 24f,
                maxHealth = 140f,
                attackAudio = HammerAudioPath,
                weaponKeyword = "TH_maul"
            },
            // 19. Priest
            new UnitConfig
            {
                prefabName = "TT_Priest",
                unitDisplayName = "Priest",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "staff_caster.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.8f,
                detectionRange = 8f,
                attackDamage = 14f,
                maxHealth = 95f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "staff_A"
            },
            // 20. Scout
            new UnitConfig
            {
                prefabName = "TT_Scout",
                unitDisplayName = "Scout",
                combatType = PeasantUnitController.UnitCombatType.Archer,
                controllerName = "archer.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.8f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 15f,
                detectionRange = 25f,
                attackDamage = 14f,
                maxHealth = 90f,
                attackAudio = ArrowAudioPath,
                weaponKeyword = "short_bow"
            },
            // 21. Settler
            new UnitConfig
            {
                prefabName = "TT_Settler",
                unitDisplayName = "Settler",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "settler.controller",
                isCavalry = true,
                isSettler = true,
                moveSpeed = 3.0f,
                colliderRadius = 1.0f,
                colliderHeight = 2f,
                colliderCenter = new Vector3(0f, 1f, 0f),
                attackRange = 1.6f,
                detectionRange = 6f,
                attackDamage = 10f,
                maxHealth = 200f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "Cart"
            },
            // 22. Spearman
            new UnitConfig
            {
                prefabName = "TT_Spearman",
                unitDisplayName = "Spearman",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "spearman.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 2.2f,
                detectionRange = 8f,
                attackDamage = 16f,
                maxHealth = 105f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "spear"
            },
            // 23. Swordman
            new UnitConfig
            {
                prefabName = "TT_Swordman",
                unitDisplayName = "Swordman",
                combatType = PeasantUnitController.UnitCombatType.Melee,
                controllerName = "players.controller",
                isCavalry = false,
                isSettler = false,
                moveSpeed = 3.5f,
                colliderRadius = 0.5f,
                colliderHeight = 1f,
                colliderCenter = new Vector3(0f, 0.5f, 0f),
                attackRange = 1.6f,
                detectionRange = 8f,
                attackDamage = 15f,
                maxHealth = 100f,
                attackAudio = SwordAudioPath,
                weaponKeyword = "short_sword"
            }
        };

        [MenuItem("Tools/Generate All Unit Prefabs")]
        public static string GenerateAll()
        {
            EnsureDirectories();
            CreateMissingAnimatorControllers();

            var characterAvatar = LoadAvatar(CharacterAvatarPath);
            var cavalryAvatar = LoadAvatar(CavalryAvatarPath);
            var cartAvatar = LoadAvatar(CartAvatarPath);

            var healthBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath);
            var arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            var textureBlue = AssetDatabase.LoadAssetAtPath<Texture>(BlueTexturePath);
            var textureRed = AssetDatabase.LoadAssetAtPath<Texture>(RedTexturePath);

            var swordClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SwordAudioPath);
            var hammerClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HammerAudioPath);
            var arrowClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ArrowAudioPath);

            var screamClips = new List<AudioClip>();
            foreach (var sp in ScreamAudioPaths)
            {
                var ac = AssetDatabase.LoadAssetAtPath<AudioClip>(sp);
                if (ac != null) screamClips.Add(ac);
            }

            int peasantLayer = LayerMask.NameToLayer("peasent");
            if (peasantLayer < 0) peasantLayer = 0;

            var results = new List<string>();

            foreach (var cfg in RemainingUnits)
            {
                string sourcePath = $"{BasePrefabsDir}/{cfg.prefabName}.prefab";
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (basePrefab == null)
                {
                    results.Add($"[ERROR] Base prefab not found: {sourcePath}");
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(basePrefab);
                instance.name = cfg.prefabName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Set tag and layer recursively
                SetLayerAndTagRecursively(instance, peasantLayer, "Untagged");

                // Animator Setup
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.applyRootMotion = false;

                if (cfg.isSettler)
                    animator.avatar = cartAvatar;
                else if (cfg.isCavalry)
                    animator.avatar = cavalryAvatar;
                else
                    animator.avatar = characterAvatar;

                string controllerPath = $"{AnimControllerDir}/{cfg.controllerName}";
                var runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                animator.runtimeAnimatorController = runtimeController;

                // CapsuleCollider Setup
                var collider = instance.GetComponent<CapsuleCollider>();
                if (collider == null) collider = instance.AddComponent<CapsuleCollider>();
                collider.center = cfg.colliderCenter;
                collider.radius = cfg.colliderRadius;
                collider.height = cfg.colliderHeight;
                collider.isTrigger = false;

                // NavMeshAgent Setup
                var agent = instance.GetComponent<NavMeshAgent>();
                if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
                agent.speed = cfg.moveSpeed;
                agent.angularSpeed = 120f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0f;
                agent.radius = cfg.colliderRadius;
                agent.height = cfg.colliderHeight >= 1.5f ? cfg.colliderHeight : 2f;

                // AudioSource Setup
                var audioSource = instance.GetComponent<AudioSource>();
                if (audioSource == null) audioSource = instance.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;

                // Healthbar Setup
                Transform headContainer = FindChildRecursive(instance.transform, "HEAD_CONTAINER");
                Transform healthBarCube = null;

                if (headContainer != null && healthBarPrefab != null)
                {
                    // Remove any old healthbar child if present
                    Transform oldHb = headContainer.Find("healthbar");
                    if (oldHb != null) UnityEngine.Object.DestroyImmediate(oldHb.gameObject);

                    GameObject hbInstance = UnityEngine.Object.Instantiate(healthBarPrefab, headContainer);
                    hbInstance.name = "healthbar";
                    hbInstance.transform.localPosition = new Vector3(-0.17f, 0.99f, 0.79f);
                    hbInstance.transform.localEulerAngles = new Vector3(89.98f, 0f, 0f);
                    hbInstance.transform.localScale = new Vector3(0.87f, 0.01f, 0.14f);
                    SetLayerAndTagRecursively(hbInstance, peasantLayer, "Untagged");

                    healthBarCube = FindChildRecursive(hbInstance.transform, "heathBarmain");
                }

                // Weapon finding for attackPoint / arrowSpawnPoint
                Transform weaponTransform = null;
                if (!string.IsNullOrEmpty(cfg.weaponKeyword))
                {
                    weaponTransform = FindChildRecursive(instance.transform, cfg.weaponKeyword);
                }
                if (weaponTransform == null)
                {
                    // Fallback to active renderer in hand
                    var rHand = FindChildRecursive(instance.transform, "R_hand_container");
                    var lHand = FindChildRecursive(instance.transform, "L_hand_container");
                    if (rHand != null)
                    {
                        foreach (Transform child in rHand)
                        {
                            if (child.gameObject.activeSelf) { weaponTransform = child; break; }
                        }
                    }
                    if (weaponTransform == null && lHand != null)
                    {
                        foreach (Transform child in lHand)
                        {
                            if (child.gameObject.activeSelf) { weaponTransform = child; break; }
                        }
                    }
                }
                if (weaponTransform == null) weaponTransform = instance.transform;

                // PeasantUnitController Setup
                var controller = instance.GetComponent<PeasantUnitController>();
                if (controller == null) controller = instance.AddComponent<PeasantUnitController>();

                // Configure via SerializedObject for robust Editor serialization
                var so = new SerializedObject(controller);
                so.Update();

                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("agent").objectReferenceValue = agent;
                so.FindProperty("attackPoint").objectReferenceValue = weaponTransform;
                so.FindProperty("teamId").intValue = 0;
                so.FindProperty("unitName").stringValue = cfg.unitDisplayName;
                so.FindProperty("unitType").enumValueIndex = (int)cfg.combatType;
                so.FindProperty("maxHealth").floatValue = cfg.maxHealth;
                so.FindProperty("destroyAfterDeath").boolValue = true;
                so.FindProperty("destroyDelay").floatValue = 4f;

                if (healthBarCube != null)
                {
                    so.FindProperty("healthBarCube").objectReferenceValue = healthBarCube;
                }
                so.FindProperty("healthBarScaleAxis").vector3Value = Vector3.right;
                so.FindProperty("hideHealthBarOnDeath").boolValue = true;

                so.FindProperty("moveSpeed").floatValue = cfg.moveSpeed;
                so.FindProperty("acceleration").floatValue = 12f;
                so.FindProperty("turnSpeed").floatValue = 720f;
                so.FindProperty("stoppingDistance").floatValue = 0.15f;
                so.FindProperty("updateRotation").boolValue = true;
                so.FindProperty("syncAnimSpeedToMoveSpeed").boolValue = true;
                so.FindProperty("referenceRunSpeed").floatValue = cfg.moveSpeed;
                so.FindProperty("animSpeedDampTime").floatValue = 0.12f;
                so.FindProperty("runAnimationSpeedMultiplier").floatValue = 1f;
                so.FindProperty("powerMultiplier").floatValue = 1f;

                so.FindProperty("canAutoAcquireTargets").boolValue = true;
                so.FindProperty("detectionRange").floatValue = cfg.detectionRange;
                so.FindProperty("targetLayers").intValue = -1;
                so.FindProperty("attackRange").floatValue = cfg.attackRange;
                so.FindProperty("attackDamage").floatValue = cfg.attackDamage;
                so.FindProperty("attackCooldown").floatValue = 1.4f;
                so.FindProperty("hitDelay").floatValue = 0.45f;
                so.FindProperty("attackRangePadding").floatValue = 0.15f;
                so.FindProperty("stopToAttack").boolValue = true;
                so.FindProperty("faceTargetBeforeAttack").boolValue = true;

                // Archer / Ranged properties
                if (cfg.combatType == PeasantUnitController.UnitCombatType.Archer)
                {
                    so.FindProperty("arrowPrefab").objectReferenceValue = arrowPrefab;
                    so.FindProperty("arrowSpawnPoint").objectReferenceValue = weaponTransform;
                    so.FindProperty("archerHitDelay").floatValue = 0.55f;
                    so.FindProperty("archerAimDelay").floatValue = 0.85f;
                    so.FindProperty("lineOfSightObstacleLayers").intValue = -1;
                    so.FindProperty("checkLineOfSightBeforeAttack").boolValue = true;
                    so.FindProperty("lockTargetBeforeAttack").boolValue = true;
                    so.FindProperty("lockOnFireDelay").floatValue = 0.7f;
                    so.FindProperty("targetLockAngleThreshold").floatValue = 10f;
                    so.FindProperty("relockBetweenShots").boolValue = false;
                }

                so.FindProperty("applyFlockingSeparation").boolValue = true;
                so.FindProperty("maintainPersonalSpaceWhenIdle").boolValue = true;

                so.FindProperty("audioSource").objectReferenceValue = audioSource;

                AudioClip atkClip = swordClip;
                if (cfg.attackAudio == HammerAudioPath) atkClip = hammerClip;
                else if (cfg.attackAudio == ArrowAudioPath) atkClip = arrowClip;
                so.FindProperty("attackAudioClip").objectReferenceValue = atkClip;

                // Scream audio clips
                var screamsProp = so.FindProperty("screamAudioClips");
                screamsProp.arraySize = screamClips.Count;
                for (int i = 0; i < screamClips.Count; i++)
                {
                    screamsProp.GetArrayElementAtIndex(i).objectReferenceValue = screamClips[i];
                }

                so.FindProperty("attackAudioVolume").floatValue = 0.85f;
                so.FindProperty("screamAudioVolume").floatValue = 0.9f;
                so.FindProperty("pitchVariation").floatValue = 0.12f;
                so.FindProperty("startScreamDelay").floatValue = 0.25f;
                so.FindProperty("minMidScreamInterval").floatValue = 5f;
                so.FindProperty("maxMidScreamInterval").floatValue = 12f;

                so.FindProperty("runningBool").stringValue = "running";
                so.FindProperty("speedFloat").stringValue = "speed";
                so.FindProperty("attackTrigger").stringValue = "attack";
                so.FindProperty("hurtTrigger").stringValue = "hurt";
                so.FindProperty("deathTrigger").stringValue = "death";

                so.FindProperty("showDebugGizmos").boolValue = true;
                so.FindProperty("verboseDebugLogging").boolValue = false;

                // Textures
                so.FindProperty("texture_self").objectReferenceValue = textureBlue;
                so.FindProperty("texture_opp").objectReferenceValue = textureRed;

                // Populate active unitRenderers (excluding healthbar renderers)
                var validRenderers = new List<Renderer>();
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.gameObject.activeSelf && !r.name.Contains("heathBar") && !r.name.Contains("healthbar"))
                    {
                        validRenderers.Add(r);
                    }
                }
                var rendProp = so.FindProperty("unitRenderers");
                rendProp.arraySize = validRenderers.Count;
                for (int i = 0; i < validRenderers.Count; i++)
                {
                    rendProp.GetArrayElementAtIndex(i).objectReferenceValue = validRenderers[i];
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(instance);

                // Save Prefab in characters folder
                string targetCharactersPath = $"{OutputCharactersDir}/{cfg.prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, targetCharactersPath);

                // Also save in prefabs folder
                string targetPrefabsPath = $"{OutputPrefabsDir}/{cfg.prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, targetPrefabsPath);

                // Also save in Powerups folder
                string targetPowerupsPath = $"{OutputPowerupsDir}/{cfg.prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, targetPowerupsPath);

                UnityEngine.Object.DestroyImmediate(instance);
                results.Add($"[SUCCESS] Created {cfg.prefabName} (controller: {cfg.controllerName}, combat: {cfg.combatType})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return string.Join("\n", results.ToArray());
        }

        public static void CreateMissingAnimatorControllers()
        {
            if (!Directory.Exists(AnimControllerDir))
            {
                Directory.CreateDirectory(AnimControllerDir);
                AssetDatabase.Refresh();
            }

            foreach (var kvp in ControllerDefinitions)
            {
                string controllerPath = $"{AnimControllerDir}/{kvp.Key}";
                // Build fresh or update controller
                BuildAnimatorController(controllerPath, kvp.Value);
            }

            AssetDatabase.SaveAssets();
        }

        private static void BuildAnimatorController(string path, ControllerClips clips)
        {
            var idleClip = LoadAnimationClip(clips.idleFbx);
            var runClip = LoadAnimationClip(clips.runFbx);
            var attackClip = LoadAnimationClip(clips.attackFbx);
            var hurtClip = LoadAnimationClip(clips.hurtFbx);
            var deathClip = LoadAnimationClip(clips.deathFbx);

            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null)
            {
                ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            else
            {
                // Clear existing parameters and states to rebuild cleanly
                while (ac.parameters.Length > 0)
                {
                    ac.RemoveParameter(0);
                }
                var sm = ac.layers[0].stateMachine;
                while (sm.states.Length > 0)
                {
                    sm.RemoveState(sm.states[0].state);
                }
                while (sm.anyStateTransitions.Length > 0)
                {
                    sm.RemoveAnyStateTransition(sm.anyStateTransitions[0]);
                }
            }

            ac.AddParameter("running", AnimatorControllerParameterType.Bool);
            ac.AddParameter("attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("hurt", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("death", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("speed", AnimatorControllerParameterType.Float);

            var rootSm = ac.layers[0].stateMachine;

            var idleState = rootSm.AddState("infantry_01_idle");
            idleState.motion = idleClip;
            rootSm.defaultState = idleState;

            var runState = rootSm.AddState("run");
            runState.motion = runClip;

            var attackState = rootSm.AddState("attack");
            attackState.motion = attackClip;

            var hurtState = rootSm.AddState("hurt");
            hurtState.motion = hurtClip;

            var deathState = rootSm.AddState("death");
            deathState.motion = deathClip;

            // Idle -> Run
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.1f;
            idleToRun.AddCondition(AnimatorConditionMode.If, 0, "running");

            // Run -> Idle
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.1f;
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "running");

            // AnyState -> Attack
            var anyToAttack = rootSm.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.05f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "attack");

            // Attack -> Idle
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.duration = 0.08f;

            // Attack -> Run
            var attackToRun = attackState.AddTransition(runState);
            attackToRun.hasExitTime = false;
            attackToRun.duration = 0.25f;
            attackToRun.AddCondition(AnimatorConditionMode.If, 0, "running");

            // AnyState -> Hurt
            var anyToHurt = rootSm.AddAnyStateTransition(hurtState);
            anyToHurt.hasExitTime = false;
            anyToHurt.duration = 0.05f;
            anyToHurt.canTransitionToSelf = false;
            anyToHurt.AddCondition(AnimatorConditionMode.If, 0, "hurt");

            // Hurt -> Idle
            var hurtToIdle = hurtState.AddTransition(idleState);
            hurtToIdle.hasExitTime = true;
            hurtToIdle.duration = 0.08f;

            // AnyState -> Death
            var anyToDeath = rootSm.AddAnyStateTransition(deathState);
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.03f;
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "death");

            EditorUtility.SetDirty(ac);
        }

        private static AnimationClip LoadAnimationClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static Avatar LoadAvatar(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in assets)
            {
                if (a is Avatar av) return av;
            }
            return null;
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(OutputCharactersDir)) Directory.CreateDirectory(OutputCharactersDir);
            if (!Directory.Exists(OutputPrefabsDir)) Directory.CreateDirectory(OutputPrefabsDir);
            if (!Directory.Exists(OutputPowerupsDir)) Directory.CreateDirectory(OutputPowerupsDir);
            if (!Directory.Exists(AnimControllerDir)) Directory.CreateDirectory(AnimControllerDir);
            AssetDatabase.Refresh();
        }

        private static void SetLayerAndTagRecursively(GameObject go, int layer, string tag)
        {
            go.layer = layer;
            if (!string.IsNullOrEmpty(tag)) go.tag = tag;
            foreach (Transform child in go.transform)
            {
                SetLayerAndTagRecursively(child.gameObject, layer, tag);
            }
        }

        private static Transform FindChildRecursive(Transform parent, string nameSubstr)
        {
            if (parent.name.IndexOf(nameSubstr, StringComparison.OrdinalIgnoreCase) >= 0)
                return parent;

            foreach (Transform child in parent)
            {
                var found = FindChildRecursive(child, nameSubstr);
                if (found != null) return found;
            }
            return null;
        }
    }
}
