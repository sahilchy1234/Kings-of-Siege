#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Sets up the SegmentedEnergySliderController and Juicy Card Buttons in the active scene:
/// 1. Finds 'slider' Image under 'box' and links to fillImage.
/// 2. Finds 'golden' -> 'Text (TMP)' and links to goldenBadgeRect, goldenBadgeImage, segmentNumberText.
/// 3. Attaches JuicyCardButton to 'powerup', 'powerup (1)', 'powerup (2)', 'powerup (3)' under 'box/Image'.
/// 4. Also connects the 4 active category buttons under '[PowerupsBar]'.
/// 5. Configures cost weightages, interactability auto-disabling, and ghost drain bar.
/// </summary>
public static class EnergySliderSceneSetup
{
    [MenuItem("Tools/SEIGE/Fix & Align Deck Cards in Active Scene", false, 0)]
    public static void FixAndAnchorDeckInScene()
    {
        GameObject boxObj = GameObject.Find("box");
        GameObject powerupsBar = GameObject.Find("[PowerupsBar]");

        if (powerupsBar == null)
        {
            // Try finding in scene including inactive
            PowerupUIBar barComp = Object.FindFirstObjectByType<PowerupUIBar>(FindObjectsInactive.Include);
            if (barComp != null) powerupsBar = barComp.gameObject;
        }

        if (boxObj != null && powerupsBar != null)
        {
            Undo.RecordObject(powerupsBar.transform, "Anchor Deck to Box");

            // 1. Ensure PowerupsBar belongs to Canvas directly above box (not inside box)
            Canvas canvas = boxObj.GetComponentInParent<Canvas>();
            if (canvas != null && powerupsBar.transform.parent != canvas.transform)
            {
                powerupsBar.transform.SetParent(canvas.transform, false);
            }

            // 2. Ensure Troops Deck under box/Image is ALWAYS ACTIVE and visible
            Transform troopDeck = boxObj.transform.Find("Image");
            if (troopDeck != null)
            {
                Undo.RecordObject(troopDeck.gameObject, "Ensure Troop Deck Active");
                troopDeck.gameObject.SetActive(true);
            }

            // 3. Clean up empty GameObject under Canvas if left behind
            GameObject canvasEmptyGo = GameObject.Find("Canvas/GameObject");
            if (canvasEmptyGo != null && canvasEmptyGo.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(canvasEmptyGo);
            }

            // 4. Configure PowerupsBar RectTransform above box
            RectTransform barRect = powerupsBar.GetComponent<RectTransform>();
            if (barRect != null)
            {
                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.anchoredPosition = new Vector2(1.39f, -125f);
                barRect.sizeDelta = new Vector2(336f, 84f);
                barRect.localScale = Vector3.one;
                barRect.localRotation = Quaternion.identity;
            }

            // 5. Horizontal Layout Group
            HorizontalLayoutGroup layout = powerupsBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = powerupsBar.AddComponent<HorizontalLayoutGroup>();
            layout.enabled = true;
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 6. Normalize and position all 4 child cards
            float cardWidth = 74f;
            float spacing = 14f;
            int childCount = powerupsBar.transform.childCount;
            float totalSpan = childCount * cardWidth + Mathf.Max(0, childCount - 1) * spacing;
            float startX = -totalSpan * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < childCount; i++)
            {
                Transform c = powerupsBar.transform.GetChild(i);
                Undo.RecordObject(c, "Normalize Card Transform");
                c.localScale = Vector3.one;
                c.localRotation = Quaternion.identity;

                RectTransform crt = c.GetComponent<RectTransform>();
                if (crt != null)
                {
                    crt.anchorMin = new Vector2(0.5f, 0.5f);
                    crt.anchorMax = new Vector2(0.5f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(74f, 82f);
                    crt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 0f);
                }

                PowerupButtonUI pBtn = c.GetComponent<PowerupButtonUI>();
                if (pBtn != null)
                {
                    pBtn.ResetToRestingRotationAndScale();
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(barRect);

            RectTransform boxRect = boxObj.GetComponent<RectTransform>();
            if (boxRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(boxRect);

            EditorSceneManager.MarkSceneDirty(boxObj.scene);
            EditorSceneManager.SaveScene(boxObj.scene);

            Debug.Log("<color=#00FF88><b>[Deck Fix]</b> Successfully aligned and anchored [PowerupsBar] inside box card tray!</color>");
        }
        else
        {
            Debug.LogWarning($"[Deck Fix] Could not find box (found: {boxObj != null}) or [PowerupsBar] (found: {powerupsBar != null}).");
        }
    }

    [MenuItem("Tools/SEIGE/Setup Energy Slider & Juicy Buttons in Scene", false, 1)]
    public static void SetupSliderInScene()
    {
        // 1. Locate the slider image
        GameObject sliderObj = GameObject.Find("slider");
        if (sliderObj == null)
        {
            GameObject boxObj = GameObject.Find("box");
            if (boxObj != null)
            {
                Transform t = boxObj.transform.Find("slider");
                if (t != null) sliderObj = t.gameObject;
            }
        }

        if (sliderObj == null)
        {
            Debug.LogError("[EnergySliderSceneSetup] Could not find GameObject named 'slider' in the scene.");
            return;
        }

        Undo.RecordObject(sliderObj, "Setup Energy Slider");

        // 2. Add or get SegmentedEnergySliderController
        SegmentedEnergySliderController controller = sliderObj.GetComponent<SegmentedEnergySliderController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<SegmentedEnergySliderController>(sliderObj);
        }

        Image fillImage = sliderObj.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        // 3. Find the golden hexagon and TMP text under golden
        TMP_Text numberText = null;
        RectTransform goldenRect = null;
        Image goldenImage = null;
        GameObject goldenObj = GameObject.Find("golden");
        if (goldenObj == null && sliderObj.transform.parent != null)
        {
            Transform gt = sliderObj.transform.parent.Find("golden");
            if (gt != null) goldenObj = gt.gameObject;
        }

        if (goldenObj != null)
        {
            goldenRect = goldenObj.GetComponent<RectTransform>();
            goldenImage = goldenObj.GetComponent<Image>();
            numberText = goldenObj.GetComponentInChildren<TMP_Text>(true);
        }

        // 4. Setup serialized properties on controller
        SerializedObject so = new SerializedObject(controller);
        so.Update();

        SerializedProperty totalSegmentsProp = so.FindProperty("totalSegments");
        if (totalSegmentsProp != null) totalSegmentsProp.intValue = 8;

        SerializedProperty startingEnergyProp = so.FindProperty("startingEnergy");
        if (startingEnergyProp != null) startingEnergyProp.floatValue = 4f; // starts with 4 energy

        SerializedProperty fillRateProp = so.FindProperty("fillRate");
        if (fillRateProp != null) fillRateProp.floatValue = 0.8f; // smooth fill speed

        SerializedProperty isFillingProp = so.FindProperty("isFilling");
        if (isFillingProp != null) isFillingProp.boolValue = true;

        SerializedProperty fillImageProp = so.FindProperty("fillImage");
        if (fillImageProp != null) fillImageProp.objectReferenceValue = fillImage;

        SerializedProperty goldenRectProp = so.FindProperty("goldenBadgeRect");
        if (goldenRectProp != null) goldenRectProp.objectReferenceValue = goldenRect;

        SerializedProperty goldenImageProp = so.FindProperty("goldenBadgeImage");
        if (goldenImageProp != null) goldenImageProp.objectReferenceValue = goldenImage;

        SerializedProperty numberTextProp = so.FindProperty("segmentNumberText");
        if (numberTextProp != null) numberTextProp.objectReferenceValue = numberText;

        SerializedProperty textFormatProp = so.FindProperty("textFormat");
        if (textFormatProp != null) textFormatProp.stringValue = "{0}";

        // 5. Gather button configs
        SerializedProperty configsProp = so.FindProperty("buttonConfigs");
        configsProp.ClearArray();

        // (A) The 4 primary troop cards inside box/Image
        GameObject boxImage = GameObject.Find("box/Image");
        if (boxImage == null)
        {
            GameObject box = GameObject.Find("box");
            if (box != null)
            {
                Transform imgT = box.transform.Find("Image");
                if (imgT != null) boxImage = imgT.gameObject;
            }
        }

        string[] fourNames = new string[] { "powerup", "powerup (1)", "powerup (2)", "powerup (3)" };
        float[] fourCosts = new float[] { 1f, 2f, 3f, 4f };

        if (boxImage != null)
        {
            for (int i = 0; i < fourNames.Length; i++)
            {
                Transform child = boxImage.transform.Find(fourNames[i]);
                if (child == null && i < boxImage.transform.childCount)
                {
                    child = boxImage.transform.GetChild(i);
                }
                if (child != null)
                {
                    Button btn = child.GetComponent<Button>();
                    if (btn == null)
                    {
                        btn = Undo.AddComponent<Button>(child.gameObject);
                        Image targetImg = child.GetComponent<Image>();
                        if (targetImg != null)
                        {
                            btn.targetGraphic = targetImg;
                        }
                    }

                    CanvasGroup cg = child.GetComponent<CanvasGroup>();
                    if (cg == null)
                    {
                        cg = Undo.AddComponent<CanvasGroup>(child.gameObject);
                    }

                    // Attach JuicyCardButton for juicy squash, pop, and reject feedback!
                    JuicyCardButton juicy = child.GetComponent<JuicyCardButton>();
                    if (juicy == null)
                    {
                        juicy = Undo.AddComponent<JuicyCardButton>(child.gameObject);
                    }
                    juicy.Initialize();

                    TMP_Text costLabel = child.GetComponentInChildren<TMP_Text>(true);

                    int newIdx = configsProp.arraySize;
                    configsProp.InsertArrayElementAtIndex(newIdx);
                    SerializedProperty elem = configsProp.GetArrayElementAtIndex(newIdx);

                    elem.FindPropertyRelative("label").stringValue = fourNames[i];
                    elem.FindPropertyRelative("button").objectReferenceValue = btn;
                    elem.FindPropertyRelative("energyCost").floatValue = fourCosts[i];
                    elem.FindPropertyRelative("costText").objectReferenceValue = costLabel;
                    elem.FindPropertyRelative("disableWhenInsufficient").boolValue = true;
                    elem.FindPropertyRelative("canvasGroup").objectReferenceValue = cg;
                    elem.FindPropertyRelative("disabledAlpha").floatValue = 0.5f;
                }
            }
        }

        // (B) Configure the powerup buttons in [PowerupsBar]
        GameObject powerupsBar = GameObject.Find("[PowerupsBar]");
        if (powerupsBar != null)
        {
            PowerupButtonUI[] barButtons = powerupsBar.GetComponentsInChildren<PowerupButtonUI>(true);
            foreach (var pBtn in barButtons)
            {
                if (pBtn == null) continue;
                Button btn = pBtn.ButtonComponent != null ? pBtn.ButtonComponent : pBtn.GetComponent<Button>();
                if (btn == null)
                {
                    btn = Undo.AddComponent<Button>(pBtn.gameObject);
                }

                CanvasGroup cg = pBtn.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = Undo.AddComponent<CanvasGroup>(pBtn.gameObject);
                }

                TMP_Text costLabel = pBtn.GetComponentInChildren<TMP_Text>(true);

                float cost = 2f;
                if (PowerupManager.Instance != null)
                {
                    var data = PowerupManager.Instance.GetConfig(pBtn.BoundPowerupType);
                    if (data != null) cost = data.energyCost;
                }

                int newIdx = configsProp.arraySize;
                configsProp.InsertArrayElementAtIndex(newIdx);
                SerializedProperty elem = configsProp.GetArrayElementAtIndex(newIdx);

                elem.FindPropertyRelative("label").stringValue = pBtn.gameObject.name;
                elem.FindPropertyRelative("button").objectReferenceValue = btn;
                elem.FindPropertyRelative("energyCost").floatValue = cost;
                elem.FindPropertyRelative("costText").objectReferenceValue = costLabel;
                elem.FindPropertyRelative("disableWhenInsufficient").boolValue = true;
                elem.FindPropertyRelative("canvasGroup").objectReferenceValue = cg;
                elem.FindPropertyRelative("disabledAlpha").floatValue = 0.5f;
            }
        }

        so.ApplyModifiedProperties();

        controller.EnsureComponents();

        // Mark scene dirty so user can save
        EditorSceneManager.MarkSceneDirty(sliderObj.scene);

        Debug.Log($"<color=#55ff55><b>[SegmentedEnergySliderController] Successfully configured in scene!</b></color> FillImage: {fillImage.name}, GoldenHexagon: {(goldenRect != null ? goldenRect.name : "null")}, Total Buttons Configured: {configsProp.arraySize}");
    }

    [MenuItem("Tools/SEIGE/Preview Juicy UI Animations (Editor Mode)", false, 2)]
    public static void PreviewJuicyAnimationsInEditor()
    {
        SegmentedEnergySliderController controller = Object.FindFirstObjectByType<SegmentedEnergySliderController>();
        if (controller == null)
        {
            SetupSliderInScene();
            controller = Object.FindFirstObjectByType<SegmentedEnergySliderController>();
        }

        if (controller == null) return;

        controller.EnsureComponents();

        // Start edit-mode DOTween loop
        DOTweenEditModePreview.StartPreview(8f);

        // 1. Play segment level-up punch
        controller.PlaySegmentGainedJuice(5);

        // 2. Play juice on each troop button
        JuicyCardButton[] juicyButtons = Object.FindObjectsByType<JuicyCardButton>(FindObjectsSortMode.None);
        for (int i = 0; i < juicyButtons.Length; i++)
        {
            float delay = i * 0.08f;
            int idx = i;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (juicyButtons[idx] != null)
                {
                    juicyButtons[idx].PlaySpawnPop();
                }
            });
        }

        // 3. Play deck entrance on powerup bar
        PowerupUIBar uiBar = Object.FindFirstObjectByType<PowerupUIBar>();
        if (uiBar != null)
        {
            uiBar.PlayDeckEntranceAnimation();
        }

        Debug.Log("<color=#FFD700><b>[Juicy UI Preview]</b> Playing juicy animations directly in Scene/Game view!</color>");
    }

    [MenuItem("Tools/SEIGE/Reset All Cards & UI to Original Transform", false, 3)]
    public static void ResetAllCardsToOriginalTransform()
    {
        // 1. Stop all preview tweens
        DOTweenEditModePreview.StopPreview();
        DOTween.KillAll();

        // 2. Reset JuicyCardButton elements
        // 2. Reset Golden Badge Transform explicitly to original anchor and scale
        GameObject goldenObj = GameObject.Find("golden");
        if (goldenObj != null)
        {
            RectTransform grt = goldenObj.GetComponent<RectTransform>();
            if (grt != null)
            {
                Undo.RecordObject(grt, "Reset Golden Transform");
                grt.DOKill();
                grt.anchoredPosition = new Vector2(-167.9f, 49.2f);
                grt.localRotation = Quaternion.identity;
                grt.localScale = new Vector3(0.38034934f, 0.38034934f, 0.38034934f);
            }
        }

        // 3. Reset Slider Image Transform explicitly
        GameObject sliderObj = GameObject.Find("slider");
        if (sliderObj != null)
        {
            RectTransform srt = sliderObj.GetComponent<RectTransform>();
            if (srt != null)
            {
                Undo.RecordObject(srt, "Reset Slider Transform");
                srt.DOKill();
                srt.anchoredPosition = new Vector2(0.1541f, 49.4f);
                srt.localRotation = Quaternion.identity;
                srt.localScale = new Vector3(1f, 0.675f, 1f);
            }
        }

        // 4. Reset JuicyCardButton elements (Troop Cards)
        JuicyCardButton[] juicyButtons = Object.FindObjectsByType<JuicyCardButton>(FindObjectsSortMode.None);
        foreach (var jb in juicyButtons)
        {
            if (jb == null) continue;
            Undo.RecordObject(jb.transform, "Reset Card Transform");
            jb.Initialize();
            jb.ResetToRestingTransform();
            jb.transform.localRotation = Quaternion.identity;
            jb.ResetToRestingRotationAndScale();
        }

        // 3. Reset PowerupButtonUI elements
        // 5. Reset PowerupButtonUI elements (Spell Cards)
        PowerupButtonUI[] powerupButtons = Object.FindObjectsByType<PowerupButtonUI>(FindObjectsSortMode.None);
        foreach (var pb in powerupButtons)
        {
            if (pb == null) continue;
            Undo.RecordObject(pb.transform, "Reset Powerup Transform");
            pb.ResetToRestingTransform();
            pb.transform.localRotation = Quaternion.identity;
            pb.ResetToRestingRotationAndScale();
        }

        // 4. Reset Slider & Golden Badge
        // 6. Force Layout Groups to rebuild immediately (un-stacks cards and distributes them evenly)
        GameObject boxImage = GameObject.Find("box/Image");
        if (boxImage == null)
        {
            GameObject box = GameObject.Find("box");
            if (box != null)
            {
                Transform t = box.transform.Find("Image");
                if (t != null) boxImage = t.gameObject;
            }
        }
        if (boxImage != null)
        {
            RectTransform rt = boxImage.GetComponent<RectTransform>();
            if (rt != null)
            {
                for (int i = 0; i < rt.childCount; i++)
                {
                    Transform c = rt.GetChild(i);
                    c.localRotation = Quaternion.identity;
                    c.localScale = new Vector3(0.8117686f, 0.73484f, 0.73484f);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        GameObject powerupsBar = GameObject.Find("[PowerupsBar]");
        if (powerupsBar != null)
        {
            HorizontalLayoutGroup hlg = powerupsBar.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.enabled = true;
                hlg.spacing = 14f;
            }

            RectTransform rt = powerupsBar.GetComponent<RectTransform>();
            if (rt != null)
            {
                float cardW = 74f;
                float spc = 14f;
                int count = rt.childCount;
                float span = count * cardW + Mathf.Max(0, count - 1) * spc;
                float sX = -span * 0.5f + cardW * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    Transform c = rt.GetChild(i);
                    c.localRotation = Quaternion.identity;
                    c.localScale = Vector3.one;

                    RectTransform crt = c.GetComponent<RectTransform>();
                    if (crt != null)
                    {
                        crt.anchoredPosition = new Vector2(sX + i * (cardW + spc), 0f);
                    }
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        // 7. Reset Slider & Golden Badge on controller
        SegmentedEnergySliderController controller = Object.FindFirstObjectByType<SegmentedEnergySliderController>();
        if (controller != null)
        {
            Undo.RecordObject(controller.transform, "Reset Slider Transform");
            controller.EnsureComponents();
            controller.ResetGoldenResting();
            controller.ResetFillResting();
            if (controller.GoldenBadgeRect != null)
            {
                controller.GoldenBadgeRect.localRotation = Quaternion.identity;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#00FF88><b>[Reset UI]</b> All cards, badges, and sliders successfully reset to exact original position & rotation!</color>");
        Debug.Log("<color=#00FF88><b>[Reset UI]</b> All cards, badges, and sliders successfully reset to exact original position & rotation, and layout groups rebuilt!</color>");
    }
}
#endif
