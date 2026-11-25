#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Siege.Chests;

namespace Siege.EditorTools
{
    /// <summary>
    /// Automated setup tool to configure, wire, and validate the AAA Juicy Chest System in the MainMenu scene.
    /// Can be executed via menu item 'Tools / Siege / Setup AAA Chest System' or invoked programmatically.
    /// </summary>
    public static class ChestSystemSetupTool
    {
        [MenuItem("Tools/Siege/Setup AAA Chest System", false, 10)]
        public static void SetupChestSystemInScene()
        {
            Debug.Log("<color=#00FFAA><b>[ChestSystemSetupTool] Starting Chest System Configuration...</b></color>");

            // 1. Locate Canvas/chest
            GameObject chestRoot = GameObject.Find("Canvas/chest");
            if (chestRoot == null)
            {
                Debug.LogError("[ChestSystemSetupTool] Could not locate 'Canvas/chest' in active scene.");
                return;
            }

            Undo.RecordObject(chestRoot, "Setup Chest System");

            // Remove any old Animator components on chest1 objects
            Animator[] allAnimators = chestRoot.GetComponentsInChildren<Animator>(true);
            foreach (var anim in allAnimators)
            {
                if (anim != null && anim.gameObject.name.IndexOf("chest1", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Undo.DestroyObjectImmediate(anim);
                }
            }

            // Ensure CanvasGroup on chestRoot
            CanvasGroup rootCg = chestRoot.GetComponent<CanvasGroup>();
            if (rootCg == null) rootCg = Undo.AddComponent<CanvasGroup>(chestRoot);

            // Ensure ChestSystemController
            ChestSystemController controller = chestRoot.GetComponent<ChestSystemController>();
            if (controller == null) controller = Undo.AddComponent<ChestSystemController>(chestRoot);

            SerializedObject so = new SerializedObject(controller);
            so.Update();

            // Root elements
            so.FindProperty("modalRoot").objectReferenceValue = chestRoot;
            so.FindProperty("backgroundDimmer").objectReferenceValue = chestRoot.GetComponent<Image>();
            so.FindProperty("rootCanvasGroup").objectReferenceValue = rootCg;

            // 2. Locate Main Panels
            Transform backpanel = chestRoot.transform.Find("backpanel");
            Transform openingPanel = chestRoot.transform.Find("chestPanel");
            Transform rewardPanel = chestRoot.transform.Find("rewardPanel");

            if (backpanel != null) so.FindProperty("selectionPanel").objectReferenceValue = backpanel.GetComponent<RectTransform>();
            if (openingPanel != null) so.FindProperty("openingPanel").objectReferenceValue = openingPanel.GetComponent<RectTransform>();
            if (rewardPanel != null) so.FindProperty("rewardPanel").objectReferenceValue = rewardPanel.GetComponent<RectTransform>();

            // 3. Load Button Sprites
            Sprite greenBtn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Buttons/Green_Empty.png");
            Sprite cyanBtn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Buttons/Cyan_Empty.png");
            Sprite greyBtn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Buttons/Grey_Empty.png");

            so.FindProperty("readyButtonSprite").objectReferenceValue = greenBtn;
            so.FindProperty("unlockButtonSprite").objectReferenceValue = cyanBtn;
            so.FindProperty("waitingButtonSprite").objectReferenceValue = greyBtn;

            // Sparkle VFX Sprite
            Sprite sparkle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Effects/Sparkle.png");
            if (sparkle != null) so.FindProperty("sparkleSprite").objectReferenceValue = sparkle;

            // Loot & Overflow FX Sprites
            Sprite coin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Icons/Coin.png");
            Sprite coinStack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Icons/Coin_Stack.png");
            Sprite innerGlow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Overlays/Chest_Gold_Glow.png");
            if (coin != null) so.FindProperty("coinSprite").objectReferenceValue = coin;
            if (coinStack != null) so.FindProperty("coinStackSprite").objectReferenceValue = coinStack;
            if (innerGlow != null) so.FindProperty("innerGlowSprite").objectReferenceValue = innerGlow;

            // Audio Source & Clips
            AudioSource aSrc = chestRoot.GetComponent<AudioSource>();
            if (aSrc == null) aSrc = Undo.AddComponent<AudioSource>(chestRoot);
            aSrc.playOnAwake = false;
            so.FindProperty("audioSource").objectReferenceValue = aSrc;

            AudioClip openSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/dragon-studio-hammer-smash-effect-382731.mp3");
            AudioClip flipSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/arrowrelease.mp3");
            AudioClip tickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/arrowhit.mp3");
            AudioClip collectSnd = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/dragon-studio-sword-slice-393847.mp3");

            if (openSound != null) so.FindProperty("openChestSound").objectReferenceValue = openSound;
            if (flipSound != null) so.FindProperty("cardFlipSound").objectReferenceValue = flipSound;
            if (tickSound != null) so.FindProperty("coinTickSound").objectReferenceValue = tickSound;
            if (collectSnd != null) so.FindProperty("collectSound").objectReferenceValue = collectSnd;

            // 4. Configure ChestConfigs
            ConfigureChestConfig(so.FindProperty("silverConfig"), ChestType.Silver, "Silver Chest", 2f, 20, 200, 2, 2, "Silver");
            ConfigureChestConfig(so.FindProperty("goldConfig"), ChestType.Gold, "Gold Chest", 8f, 100, 500, 4, 4, "Gold");
            ConfigureChestConfig(so.FindProperty("masterConfig"), ChestType.Master, "Master Chest", 24f, 500, 1000, 10, 10, "Master");

            // 5. Setup Chest Slots in backpanel
            List<ChestSlotUI> slotList = new List<ChestSlotUI>();
            if (backpanel != null)
            {
                // Ensure close button on backpanel
                Transform closeTrans = backpanel.Find("close");
                if (closeTrans != null)
                {
                    Button closeBtn = closeTrans.GetComponent<Button>();
                    if (closeBtn == null) closeBtn = Undo.AddComponent<Button>(closeTrans.gameObject);
                    so.FindProperty("selectionCloseButton").objectReferenceValue = closeBtn;
                }

                // Slot 0: chest (Silver)
                Transform s0 = backpanel.Find("chest");
                Transform g0 = backpanel.Find("backglow");
                ChestSlotUI slot0 = SetupSlot(s0, 0, ChestType.Silver, g0, greenBtn, cyanBtn, greyBtn);
                if (slot0 != null) slotList.Add(slot0);

                // Slot 1: chest (1) (Gold)
                Transform s1 = backpanel.Find("chest (1)");
                Transform g1 = backpanel.Find("backglow (1)");
                ChestSlotUI slot1 = SetupSlot(s1, 1, ChestType.Gold, g1, greenBtn, cyanBtn, greyBtn);
                if (slot1 != null) slotList.Add(slot1);

                // Slot 2: chest (2) (Master)
                Transform s2 = backpanel.Find("chest (2)");
                Transform g2 = backpanel.Find("backglow (2)");
                ChestSlotUI slot2 = SetupSlot(s2, 2, ChestType.Master, g2, greenBtn, cyanBtn, greyBtn);
                if (slot2 != null) slotList.Add(slot2);
            }

            SerializedProperty slotsProp = so.FindProperty("chestSlots");
            slotsProp.arraySize = slotList.Count;
            for (int i = 0; i < slotList.Count; i++)
            {
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotList[i];
            }

            // 6. Setup Opening Screen (chestPanel)
            if (openingPanel != null)
            {
                Transform closeTrans = openingPanel.Find("close (1)");
                if (closeTrans != null)
                {
                    Button closeBtn = closeTrans.GetComponent<Button>();
                    if (closeBtn == null) closeBtn = Undo.AddComponent<Button>(closeTrans.gameObject);
                    so.FindProperty("openingCloseButton").objectReferenceValue = closeBtn;
                }

                Transform topTextTrans = openingPanel.Find("TopText");
                if (topTextTrans != null) so.FindProperty("openingTopText").objectReferenceValue = topTextTrans.GetComponent<TextMeshProUGUI>();

                Transform chest1Trans = openingPanel.Find("chest1");
                if (chest1Trans != null)
                {
                    so.FindProperty("openingChestImage").objectReferenceValue = chest1Trans.GetComponent<Image>();

                    // Ensure button on chest1 for tap-to-reveal
                    Button chestBtn = chest1Trans.GetComponent<Button>();
                    if (chestBtn == null) chestBtn = Undo.AddComponent<Button>(chest1Trans.gameObject);
                    so.FindProperty("tapToRevealClickZone").objectReferenceValue = chestBtn;
                }

                Transform glowTrans = openingPanel.Find("glow");
                if (glowTrans != null) so.FindProperty("openingGlow").objectReferenceValue = glowTrans.GetComponent<Image>();

                Transform tapTextTrans = openingPanel.Find("TapToReveal");
                if (tapTextTrans != null) so.FindProperty("tapToRevealText").objectReferenceValue = tapTextTrans.GetComponent<TextMeshProUGUI>();
            }

            // 7. Setup Reward Screen (rewardPanel)
            if (rewardPanel != null)
            {
                Transform innerChestPanel = rewardPanel.Find("chestPanel");
                if (innerChestPanel != null)
                {
                    Transform closeTrans = innerChestPanel.Find("close (1)");
                    if (closeTrans != null)
                    {
                        Button closeBtn = closeTrans.GetComponent<Button>();
                        if (closeBtn == null) closeBtn = Undo.AddComponent<Button>(closeTrans.gameObject);
                        so.FindProperty("rewardCloseButton").objectReferenceValue = closeBtn;
                    }

                    Transform topTextTrans = innerChestPanel.Find("TopText");
                    if (topTextTrans != null) so.FindProperty("rewardTopText").objectReferenceValue = topTextTrans.GetComponent<TextMeshProUGUI>();

                    Transform chest1Trans = innerChestPanel.Find("chest1");
                    if (chest1Trans != null) so.FindProperty("rewardChestImage").objectReferenceValue = chest1Trans.GetComponent<Image>();

                    Transform glowTrans = innerChestPanel.Find("glow");
                    if (glowTrans != null) so.FindProperty("rewardGlow").objectReferenceValue = glowTrans.GetComponent<Image>();

                    // Coins
                    Transform coinsTrans = innerChestPanel.Find("goldCOins");
                    if (coinsTrans != null)
                    {
                        so.FindProperty("goldCoinsContainer").objectReferenceValue = coinsTrans.GetComponent<RectTransform>();
                        so.FindProperty("goldCoinsImage").objectReferenceValue = coinsTrans.GetComponent<Image>();
                        Transform coinTextTrans = coinsTrans.Find("Text (TMP)");
                        if (coinTextTrans != null) so.FindProperty("goldCoinsText").objectReferenceValue = coinTextTrans.GetComponent<TextMeshProUGUI>();
                    }

                    // Army Card
                    Transform armyTrans = innerChestPanel.Find("rewardsCardsOrPowerUP");
                    if (armyTrans != null)
                    {
                        so.FindProperty("armyRewardCard").objectReferenceValue = armyTrans.GetComponent<RectTransform>();
                        so.FindProperty("armyCardImage").objectReferenceValue = armyTrans.GetComponent<Image>();
                        Transform countTrans = armyTrans.Find("TextCount");
                        if (countTrans != null) so.FindProperty("armyCardCount").objectReferenceValue = countTrans.GetComponent<TextMeshProUGUI>();
                        Transform nameTrans = armyTrans.Find("name");
                        if (nameTrans != null) so.FindProperty("armyCardName").objectReferenceValue = nameTrans.GetComponent<TextMeshProUGUI>();
                    }

                    // Power Card (Duplicate if missing)
                    Transform powerTrans = innerChestPanel.Find("rewardsPowerCard");
                    if (powerTrans == null && armyTrans != null)
                    {
                        GameObject dup = GameObject.Instantiate(armyTrans.gameObject, innerChestPanel);
                        dup.name = "rewardsPowerCard";
                        Undo.RegisterCreatedObjectUndo(dup, "Create rewardsPowerCard");
                        powerTrans = dup.transform;
                    }

                    if (powerTrans != null)
                    {
                        so.FindProperty("powerRewardCard").objectReferenceValue = powerTrans.GetComponent<RectTransform>();
                        so.FindProperty("powerCardImage").objectReferenceValue = powerTrans.GetComponent<Image>();
                        Transform countTrans = powerTrans.Find("TextCount");
                        if (countTrans != null) so.FindProperty("powerCardCount").objectReferenceValue = countTrans.GetComponent<TextMeshProUGUI>();
                        Transform nameTrans = powerTrans.Find("name");
                        if (nameTrans != null) so.FindProperty("powerCardName").objectReferenceValue = nameTrans.GetComponent<TextMeshProUGUI>();
                    }

                    // Arrange horizontal positions: Army (-48), Coins (0), Power (+48)
                    if (armyTrans != null) armyTrans.GetComponent<RectTransform>().anchoredPosition = new Vector2(-48f, -15.18f);
                    if (coinsTrans != null) coinsTrans.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -12.00f);
                    if (powerTrans != null) powerTrans.GetComponent<RectTransform>().anchoredPosition = new Vector2(48f, -15.18f);

                    // Collect button
                    Transform collectTrans = innerChestPanel.Find("collectButton");
                    if (collectTrans != null)
                    {
                        Button collectBtn = collectTrans.GetComponent<Button>();
                        if (collectBtn == null) collectBtn = Undo.AddComponent<Button>(collectTrans.gameObject);
                        so.FindProperty("collectButton").objectReferenceValue = collectBtn;
                    }
                }
            }

            // 8. Load Card Sprites
            LoadCardSprites(so);

            // 9. Main Menu HUD Connections
            GameObject coinCounterObj = GameObject.Find("Canvas/Panel/toppanel/coinCounter");
            if (coinCounterObj != null)
            {
                so.FindProperty("hudCoinCounterText").objectReferenceValue = coinCounterObj.GetComponentInChildren<TMP_Text>();
            }

            // Side panel chest shortcut button
            GameObject sideImageObj = GameObject.Find("Canvas/Panel/sidepanel/Image");
            if (sideImageObj != null)
            {
                Button sideBtn = sideImageObj.GetComponent<Button>();
                if (sideBtn == null) sideBtn = Undo.AddComponent<Button>(sideImageObj);
                so.FindProperty("sidePanelChestShortcut").objectReferenceValue = sideBtn;
            }

            // Apply modifications
            so.ApplyModifiedProperties();

            // Ensure backpanel anchoredPosition and resting scale
            if (backpanel != null)
            {
                RectTransform bprt = backpanel.GetComponent<RectTransform>();
                bprt.anchoredPosition = new Vector2(2f, -2f);
                bprt.localScale = new Vector3(6.1296763f, 6.1296763f, 6.1296763f);
            }

            // Clear old test slot prefs so default states apply
            PlayerPrefs.DeleteKey("Siege_ChestSlot_0_State");
            PlayerPrefs.DeleteKey("Siege_ChestSlot_1_State");
            PlayerPrefs.DeleteKey("Siege_ChestSlot_2_State");
            PlayerPrefs.DeleteKey("Siege_ChestSlot_0_EndTime");
            PlayerPrefs.DeleteKey("Siege_ChestSlot_1_EndTime");
            PlayerPrefs.DeleteKey("Siege_ChestSlot_2_EndTime");
            PlayerPrefs.Save();

            // Set active states
            if (backpanel != null) backpanel.gameObject.SetActive(true);
            if (openingPanel != null) openingPanel.gameObject.SetActive(false);
            if (rewardPanel != null) rewardPanel.gameObject.SetActive(false);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(chestRoot.scene);
            EditorSceneManager.SaveScene(chestRoot.scene);

            Debug.Log("<color=#00FFAA><b>[ChestSystemSetupTool] Chest System configured and scene saved successfully!</b></color>");
        }

        private static ChestSlotUI SetupSlot(Transform slotTrans, int index, ChestType type, Transform glowTrans, Sprite greenBtn, Sprite cyanBtn, Sprite greyBtn)
        {
            if (slotTrans == null) return null;

            Undo.RecordObject(slotTrans.gameObject, "Setup Chest Slot");
            ChestSlotUI slotUI = slotTrans.GetComponent<ChestSlotUI>();
            if (slotUI == null) slotUI = Undo.AddComponent<ChestSlotUI>(slotTrans.gameObject);

            SerializedObject sObj = new SerializedObject(slotUI);
            sObj.Update();

            sObj.FindProperty("slotIndex").intValue = index;
            sObj.FindProperty("chestType").enumValueIndex = (int)type;

            sObj.FindProperty("chestImage").objectReferenceValue = slotTrans.GetComponent<Image>();

            Transform title = slotTrans.Find("yourChest (1)");
            if (title != null) sObj.FindProperty("titleText").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();

            Transform btnTrans = slotTrans.Find("button");
            if (btnTrans != null)
            {
                Button btn = btnTrans.GetComponent<Button>();
                if (btn == null) btn = Undo.AddComponent<Button>(btnTrans.gameObject);
                sObj.FindProperty("actionButton").objectReferenceValue = btn;
                sObj.FindProperty("buttonImage").objectReferenceValue = btnTrans.GetComponent<Image>();

                Transform btnText = btnTrans.Find("buttontext");
                if (btnText != null) sObj.FindProperty("buttonText").objectReferenceValue = btnText.GetComponent<TextMeshProUGUI>();
            }

            Transform timeTrans = slotTrans.Find("timeicon");
            if (timeTrans != null)
            {
                sObj.FindProperty("timeIcon").objectReferenceValue = timeTrans.GetComponent<Image>();
                Transform tText = timeTrans.Find("timerText");
                if (tText != null) sObj.FindProperty("timerText").objectReferenceValue = tText.GetComponent<TextMeshProUGUI>();
            }

            if (glowTrans != null)
            {
                sObj.FindProperty("backglowObject").objectReferenceValue = glowTrans.gameObject;
                sObj.FindProperty("backglowImage").objectReferenceValue = glowTrans.GetComponent<Image>();
            }

            sObj.FindProperty("readyButtonSprite").objectReferenceValue = greenBtn;
            sObj.FindProperty("unlockButtonSprite").objectReferenceValue = cyanBtn;
            sObj.FindProperty("waitingButtonSprite").objectReferenceValue = greyBtn;

            RectTransform rt = slotTrans.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos = index == 0 ? new Vector2(-46.8f, 0f) : (index == 1 ? new Vector2(0f, 0f) : new Vector2(46.8f, 0f));
                Vector3 scale = new Vector3(0.39893f, 0.39893f, 0.39893f);
                rt.anchoredPosition = pos;
                rt.localScale = scale;
            }

            sObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(slotUI);
            return slotUI;
        }

        private static void ConfigureChestConfig(SerializedProperty prop, ChestType type, string name, float hours, int minC, int maxC, int armyC, int powerC, string folder)
        {
            prop.FindPropertyRelative("type").enumValueIndex = (int)type;
            prop.FindPropertyRelative("displayName").stringValue = name;
            prop.FindPropertyRelative("unlockHours").floatValue = hours;
            prop.FindPropertyRelative("minCoins").intValue = minC;
            prop.FindPropertyRelative("maxCoins").intValue = maxC;
            prop.FindPropertyRelative("armyCopies").intValue = armyC;
            prop.FindPropertyRelative("powerCopies").intValue = powerC;

            // Load sprites
            string basePath = $"Assets/UIPACK/Kings_of_Siege_Chest_Individual_PNGs/{folder}";
            prop.FindPropertyRelative("closedSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{basePath}/{folder}_Closed.png") ??
                AssetDatabase.LoadAssetAtPath<Sprite>($"{basePath}/Animation_Frames/{folder}_01.png");

            prop.FindPropertyRelative("openSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{basePath}/{folder}_Open_With_Light.png") ??
                AssetDatabase.LoadAssetAtPath<Sprite>($"{basePath}/Animation_Frames/{folder}_12.png");

            // Load 12 frames
            SerializedProperty framesProp = prop.FindPropertyRelative("animationFrames");
            framesProp.arraySize = 12;
            for (int f = 1; f <= 12; f++)
            {
                string fStr = f.ToString("D2");
                Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{basePath}/Animation_Frames/{folder}_{fStr}.png");
                framesProp.GetArrayElementAtIndex(f - 1).objectReferenceValue = frameSprite;
            }
        }

        private static void LoadCardSprites(SerializedObject so)
        {
            // Army Unit Cards (26)
            string unitPath = "Assets/UIPACK/Kings_of_Siege_26_Unit_Cards_No_Text";
            string[] uGuids = AssetDatabase.FindAssets("t:Sprite", new string[] { unitPath });
            SerializedProperty armyProp = so.FindProperty("armyUnitSprites");
            armyProp.arraySize = uGuids.Length;
            for (int i = 0; i < uGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(uGuids[i]);
                armyProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            // Power Cards (10)
            string powerPath = "Assets/UIPACK/Kings_of_Siege_10_Power_Cards_No_Text";
            string[] pGuids = AssetDatabase.FindAssets("t:Sprite", new string[] { powerPath });
            SerializedProperty powerProp = so.FindProperty("powerCardSprites");
            powerProp.arraySize = pGuids.Length;
            for (int i = 0; i < pGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(pGuids[i]);
                powerProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            // Card Back
            so.FindProperty("cardBackSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIPACK/Chest_UI_Pack/Cards/Card_Back.png");
        }
    }
}
#endif
