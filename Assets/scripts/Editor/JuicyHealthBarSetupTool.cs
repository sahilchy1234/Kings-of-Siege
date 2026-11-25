using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor setup tool to configure and verify all juicy health bars
/// for Players (HUD and soldiers), Siege Machines, and Fortresses in BattleScene.
/// </summary>
public static class JuicyHealthBarSetupTool
{
    [MenuItem("Tools/Battlefield/Setup Juicy Health Bars (Players & Machines)")]
    public static void ManualSetup()
    {
        SetupJuicyHealthBars(silent: false);
    }

    public static void SetupJuicyHealthBars(bool silent = false)
    {
        if (!silent)
            Debug.Log("<color=cyan>[JuicyHealthBarSetup] Starting setup for Players and Machines health bars...</color>");

        bool sceneModified = false;

        // 1. Setup Screen Dominance Health Bars in Canvas
        sceneModified |= SetupCanvasDominanceHealthBars(silent);

        // 2. Setup Castles / Fortresses in Scene
        sceneModified |= SetupCastlesInScene(silent);

        // 3. Setup Defensive Towers in Scene
        sceneModified |= SetupTowersInScene(silent);

        // 4. Setup Prefabs (Machines and Soldiers)
        SetupAllPrefabs(silent);

        if (sceneModified && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if (!silent)
                Debug.Log("<color=green>[JuicyHealthBarSetup] Scene modifications saved successfully!</color>");
        }

        if (!silent)
            Debug.Log("<color=green>[JuicyHealthBarSetup] Setup Complete! Players and Machines now have ultra-juicy health bars.</color>");
    }

    private static bool SetupCanvasDominanceHealthBars(bool silent)
    {
        var bdh = Object.FindFirstObjectByType<BattleDominanceHealthBar>();
        if (bdh == null) return false;

        Undo.RecordObject(bdh.gameObject, "Setup Dominance Health Bar");

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null) return false;

        // Load custom high-resolution DominanceHUD sprites
        var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/DominanceHUD/Dominance_Frame_Bg.png");
        var playerFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/DominanceHUD/Dominance_Fill_Player.png");
        var enemyFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/DominanceHUD/Dominance_Fill_Enemy.png");
        var ghostFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/DominanceHUD/Dominance_Fill_Ghost.png");
        var timerPillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/DominanceHUD/Dominance_Timer_Pill.png");

        // 1. Create or find BattleDominanceBar top container
        Transform barRootTr = canvasGo.transform.Find("BattleDominanceBar");
        GameObject barRootGo;
        if (barRootTr == null)
        {
            barRootGo = new GameObject("BattleDominanceBar", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(barRootGo, "Create BattleDominanceBar");
            barRootGo.transform.SetParent(canvasGo.transform, false);
            barRootTr = barRootGo.transform;
        }
        else
        {
            barRootGo = barRootTr.gameObject;
        }

        var rootRt = barRootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.anchoredPosition = new Vector2(0f, -10f);
        rootRt.sizeDelta = new Vector2(320f, 44f);
        rootRt.localScale = Vector3.one;

        // 2. Center Timer Badge & TimerText
        Transform timerBadgeTr = barRootTr.Find("CenterTimerBadge");
        GameObject timerBadgeGo;
        if (timerBadgeTr == null)
        {
            timerBadgeGo = new GameObject("CenterTimerBadge", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(timerBadgeGo, "Create CenterTimerBadge");
            timerBadgeGo.transform.SetParent(barRootTr, false);
            timerBadgeTr = timerBadgeGo.transform;
        }
        else
        {
            timerBadgeGo = timerBadgeTr.gameObject;
        }

        var timerBadgeRt = timerBadgeGo.GetComponent<RectTransform>();
        timerBadgeRt.anchorMin = new Vector2(0.5f, 0.5f);
        timerBadgeRt.anchorMax = new Vector2(0.5f, 0.5f);
        timerBadgeRt.pivot = new Vector2(0.5f, 0.5f);
        timerBadgeRt.anchoredPosition = new Vector2(0f, -2f);
        timerBadgeRt.sizeDelta = new Vector2(74f, 26f);
        timerBadgeRt.localScale = Vector3.one;

        var timerBadgeImg = timerBadgeGo.GetComponent<Image>();
        if (timerPillSprite != null)
        {
            timerBadgeImg.sprite = timerPillSprite;
            timerBadgeImg.type = Image.Type.Sliced;
            timerBadgeImg.color = Color.white;
        }

        Transform timerTextTr = canvasGo.transform.Find("TimerText");
        if (timerTextTr == null) timerTextTr = timerBadgeTr.Find("TimerText");
        if (timerTextTr != null)
        {
            timerTextTr.SetParent(timerBadgeTr, false);
            var tRt = timerTextTr.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            tRt.localScale = Vector3.one;

            var tmp = timerTextTr.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.enableAutoSizing = false;
                tmp.fontSize = 14f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
        }

        // 3. Move and style Player bar
        Transform playerTr = canvasGo.transform.Find("player") ?? barRootTr.Find("player");
        if (playerTr != null)
        {
            playerTr.SetParent(barRootTr, false);
            var pRt = playerTr.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(1f, 0.5f);
            pRt.anchoredPosition = new Vector2(-41f, -2f);
            pRt.sizeDelta = new Vector2(118f, 20f);
            pRt.localScale = Vector3.one;

            var pBgImg = playerTr.GetComponent<Image>();
            if (pBgImg != null && frameSprite != null)
            {
                pBgImg.sprite = frameSprite;
                pBgImg.type = Image.Type.Sliced;
                pBgImg.color = Color.white;
            }

            Transform pFillTr = playerTr.Find("healthbar");
            Transform pGhostTr = playerTr.Find("GhostFill");
            Image pFill = pFillTr != null ? pFillTr.GetComponent<Image>() : null;

            if (pFill != null && pGhostTr == null)
            {
                GameObject ghostGo = Object.Instantiate(pFill.gameObject, playerTr);
                ghostGo.name = "GhostFill";
                ghostGo.transform.SetSiblingIndex(pFillTr.GetSiblingIndex());
                pGhostTr = ghostGo.transform;
                Undo.RegisterCreatedObjectUndo(ghostGo, "Create Player GhostFill");
            }

            if (pGhostTr != null)
            {
                var gRt = pGhostTr.GetComponent<RectTransform>();
                gRt.anchorMin = Vector2.zero;
                gRt.anchorMax = Vector2.one;
                gRt.offsetMin = new Vector2(2f, 2f);
                gRt.offsetMax = new Vector2(-2f, -2f);
                gRt.localScale = Vector3.one;

                var gImg = pGhostTr.GetComponent<Image>();
                if (ghostFillSprite != null) gImg.sprite = ghostFillSprite;
                gImg.type = Image.Type.Filled;
                gImg.fillMethod = Image.FillMethod.Horizontal;
                gImg.fillOrigin = 0;
                gImg.color = Color.white;
            }

            if (pFillTr != null)
            {
                var fRt = pFillTr.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero;
                fRt.anchorMax = Vector2.one;
                fRt.offsetMin = new Vector2(2f, 2f);
                fRt.offsetMax = new Vector2(-2f, -2f);
                fRt.localScale = Vector3.one;

                if (pFill != null)
                {
                    if (playerFillSprite != null) pFill.sprite = playerFillSprite;
                    pFill.type = Image.Type.Filled;
                    pFill.fillMethod = Image.FillMethod.Horizontal;
                    pFill.fillOrigin = 0;
                    pFill.color = Color.white;
                }
            }

            Transform pNameTr = playerTr.Find("opponentname");
            if (pNameTr != null)
            {
                var nRt = pNameTr.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(1f, 1f);
                nRt.pivot = new Vector2(0f, 0f);
                nRt.anchoredPosition = new Vector2(2f, 3f);
                nRt.sizeDelta = new Vector2(118f, 15f);
                nRt.localScale = Vector3.one;

                var nTmp = pNameTr.GetComponent<TextMeshProUGUI>();
                if (nTmp != null)
                {
                    nTmp.enableAutoSizing = false;
                    nTmp.fontSize = 11.5f;
                    nTmp.fontStyle = FontStyles.Bold;
                    nTmp.alignment = TextAlignmentOptions.Left;
                    nTmp.text = "PLAYER";
                    Color c;
                    ColorUtility.TryParseHtmlString("#93C5FD", out c);
                    nTmp.color = c;
                }
            }
        }

        // 4. Move and style Opponent / AI bar
        Transform enemyTr = canvasGo.transform.Find("healthOpponent") ?? barRootTr.Find("healthOpponent");
        if (enemyTr != null)
        {
            enemyTr.SetParent(barRootTr, false);
            var eRt = enemyTr.GetComponent<RectTransform>();
            eRt.anchorMin = new Vector2(0.5f, 0.5f);
            eRt.anchorMax = new Vector2(0.5f, 0.5f);
            eRt.pivot = new Vector2(0f, 0.5f);
            eRt.anchoredPosition = new Vector2(41f, -2f);
            eRt.sizeDelta = new Vector2(118f, 20f);
            eRt.localScale = Vector3.one;

            var eBgImg = enemyTr.GetComponent<Image>();
            if (eBgImg != null && frameSprite != null)
            {
                eBgImg.sprite = frameSprite;
                eBgImg.type = Image.Type.Sliced;
                eBgImg.color = Color.white;
            }

            Transform eFillTr = enemyTr.Find("healthbar");
            Transform eGhostTr = enemyTr.Find("GhostFill");
            Image eFill = eFillTr != null ? eFillTr.GetComponent<Image>() : null;

            if (eFill != null && eGhostTr == null)
            {
                GameObject ghostGo = Object.Instantiate(eFill.gameObject, enemyTr);
                ghostGo.name = "GhostFill";
                ghostGo.transform.SetSiblingIndex(eFillTr.GetSiblingIndex());
                eGhostTr = ghostGo.transform;
                Undo.RegisterCreatedObjectUndo(ghostGo, "Create Enemy GhostFill");
            }

            if (eGhostTr != null)
            {
                var gRt = eGhostTr.GetComponent<RectTransform>();
                gRt.anchorMin = Vector2.zero;
                gRt.anchorMax = Vector2.one;
                gRt.offsetMin = new Vector2(2f, 2f);
                gRt.offsetMax = new Vector2(-2f, -2f);
                gRt.localScale = Vector3.one;

                var gImg = eGhostTr.GetComponent<Image>();
                if (ghostFillSprite != null) gImg.sprite = ghostFillSprite;
                gImg.type = Image.Type.Filled;
                gImg.fillMethod = Image.FillMethod.Horizontal;
                gImg.fillOrigin = 1;
                gImg.color = Color.white;
            }

            if (eFillTr != null)
            {
                var fRt = eFillTr.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero;
                fRt.anchorMax = Vector2.one;
                fRt.offsetMin = new Vector2(2f, 2f);
                fRt.offsetMax = new Vector2(-2f, -2f);
                fRt.localScale = Vector3.one;

                if (eFill != null)
                {
                    if (enemyFillSprite != null) eFill.sprite = enemyFillSprite;
                    eFill.type = Image.Type.Filled;
                    eFill.fillMethod = Image.FillMethod.Horizontal;
                    eFill.fillOrigin = 1;
                    eFill.color = Color.white;
                }
            }

            Transform eNameTr = enemyTr.Find("opponentname");
            if (eNameTr != null)
            {
                var nRt = eNameTr.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(1f, 1f);
                nRt.pivot = new Vector2(1f, 0f);
                nRt.anchoredPosition = new Vector2(-2f, 3f);
                nRt.sizeDelta = new Vector2(118f, 15f);
                nRt.localScale = Vector3.one;

                var nTmp = eNameTr.GetComponent<TextMeshProUGUI>();
                if (nTmp != null)
                {
                    nTmp.enableAutoSizing = false;
                    nTmp.fontSize = 11.5f;
                    nTmp.fontStyle = FontStyles.Bold;
                    nTmp.alignment = TextAlignmentOptions.Right;
                    nTmp.text = "AI";
                    Color c;
                    ColorUtility.TryParseHtmlString("#FCA5A5", out c);
                    nTmp.color = c;
                }
            }
        }

        // 5. Serialized Object update on BattleDominanceHealthBar
        SerializedObject so = new SerializedObject(bdh);
        so.Update();

        Transform pFillTransform = playerTr != null ? playerTr.Find("healthbar") : null;
        Transform eFillTransform = enemyTr != null ? enemyTr.Find("healthbar") : null;
        Transform pGhostTransform = playerTr != null ? playerTr.Find("GhostFill") : null;
        Transform eGhostTransform = enemyTr != null ? enemyTr.Find("GhostFill") : null;

        if (pFillTransform != null)
            so.FindProperty("playerFillImage").objectReferenceValue = pFillTransform.GetComponent<Image>();

        if (eFillTransform != null)
            so.FindProperty("enemyFillImage").objectReferenceValue = eFillTransform.GetComponent<Image>();

        if (pGhostTransform != null)
            so.FindProperty("playerGhostFill").objectReferenceValue = pGhostTransform.GetComponent<Image>();

        if (eGhostTransform != null)
            so.FindProperty("enemyGhostFill").objectReferenceValue = eGhostTransform.GetComponent<Image>();

        Transform pScoreTr = playerTr != null ? playerTr.Find("opponentname") : null;
        if (pScoreTr != null)
            so.FindProperty("playerScoreText").objectReferenceValue = pScoreTr.GetComponent<TextMeshProUGUI>();

        Transform eScoreTr = enemyTr != null ? enemyTr.Find("opponentname") : null;
        if (eScoreTr != null)
            so.FindProperty("enemyScoreText").objectReferenceValue = eScoreTr.GetComponent<TextMeshProUGUI>();

        var showProp = so.FindProperty("showPercentage");
        if (showProp != null) showProp.boolValue = false;

        so.FindProperty("ghostColor").colorValue = Color.white;
        so.FindProperty("measureMode").enumValueIndex = (int)BattleDominanceHealthBar.DominanceMeasureMode.Comprehensive;
        so.FindProperty("castleWeight").floatValue = 0.50f;
        so.FindProperty("armyWeight").floatValue = 0.35f;
        so.FindProperty("territoryWeight").floatValue = 0.15f;

        so.ApplyModifiedProperties();

        // 6. Wire GameManager.timerText
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null && timerTextTr != null)
        {
            var gmSo = new SerializedObject(gm);
            gmSo.Update();
            var tmp = timerTextTr.GetComponent<TextMeshProUGUI>();
            if (tmp != null) gmSo.FindProperty("timerText").objectReferenceValue = tmp;
            gmSo.ApplyModifiedProperties();
        }

        // 7. Position Toast container nicely below top HUD
        var toastTr = canvasGo.transform.Find("GameObject");
        if (toastTr != null)
        {
            var tRt = toastTr.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 1f);
            tRt.anchorMax = new Vector2(0.5f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -65f);
        }

        if (!silent)
            Debug.Log("<color=green>[JuicyHealthBarSetup] Successfully configured top Battle Dominance HUD with symmetrical layout and sleek graphics.</color>");

        return true;
    }

    private static bool SetupCastlesInScene(bool silent)
    {
        bool changed = false;
        var objectives = Object.FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None);

        foreach (var obj in objectives)
        {
            if (obj == null) continue;
            bool isCastle = obj.ObjectiveName == "Castle" || obj.name.ToLower().Contains("castle");
            if (!isCastle) continue;

            Undo.RecordObject(obj.gameObject, "Setup Castle Health Bar");

            Transform hb = obj.transform.Find("healthbar");
            if (hb == null)
            {
                var allT = obj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allT)
                {
                    if (t.name.Equals("healthbar", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hb = t;
                        break;
                    }
                }
            }

            if (hb != null)
            {
                var juicy = hb.GetComponent<JuicyUnitHealthBar>();
                if (juicy == null)
                {
                    juicy = Undo.AddComponent<JuicyUnitHealthBar>(hb.gameObject);
                }

                var tiltFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset");
                if (tiltFont != null) juicy.DamageNumberFont = tiltFont;

                juicy.SetupObjective(obj.transform, obj.TeamId, obj.MaxHealth);

                // Wire up on PeasantObjectiveTarget
                SerializedObject so = new SerializedObject(obj);
                so.Update();
                var prop = so.FindProperty("juicyHealthBar");
                if (prop != null) prop.objectReferenceValue = juicy;
                so.ApplyModifiedProperties();

                changed = true;
                if (!silent)
                    Debug.Log($"[JuicyHealthBarSetup] Configured Fortress Health Bar on '{obj.name}'");
            }
        }

        return changed;
    }

    private static bool SetupTowersInScene(bool silent)
    {
        bool changed = false;
        var towers = Object.FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None);

        foreach (var tower in towers)
        {
            if (tower == null) continue;

            Undo.RecordObject(tower.gameObject, "Setup Tower Health Bar");

            // 1. Purge any stray stationed archer health bars or stray UICanvases from the tower
            for (int i = tower.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = tower.transform.GetChild(i);
                if (child.name.Equals("StationedArcher", System.StringComparison.OrdinalIgnoreCase))
                {
                    var strayBars = child.GetComponentsInChildren<JuicyUnitHealthBar>(true);
                    for (int b = 0; b < strayBars.Length; b++)
                    {
                        Undo.DestroyObjectImmediate(strayBars[b].gameObject);
                        changed = true;
                    }
                    for (int c = child.childCount - 1; c >= 0; c--)
                    {
                        Transform grandChild = child.GetChild(c);
                        if (grandChild.name.Equals("healthbar", System.StringComparison.OrdinalIgnoreCase) ||
                            grandChild.name.Equals("UICanvas", System.StringComparison.OrdinalIgnoreCase))
                        {
                            Undo.DestroyObjectImmediate(grandChild.gameObject);
                            changed = true;
                        }
                    }
                }
            }

            // 2. Ensure dedicated TowerHealthBar is present and configured
            var hb = tower.GetComponentInChildren<TowerHealthBar>(true);
            if (hb == null)
            {
                Transform hbTr = tower.transform.Find("TowerHealthBar");
                if (hbTr != null) hb = hbTr.GetComponent<TowerHealthBar>();
            }

            if (hb == null)
            {
                GameObject hbGo = new GameObject("TowerHealthBar");
                hbGo.transform.SetParent(tower.transform, false);
                hb = hbGo.AddComponent<TowerHealthBar>();
                Undo.RegisterCreatedObjectUndo(hbGo, "Create TowerHealthBar");
                changed = true;
            }

            if (hb != null)
            {
                Undo.RecordObject(hb.gameObject, "Setup Tower Health Bar Components");
                var tiltFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset");
                if (tiltFont != null) hb.DamageNumberFont = tiltFont;

                hb.SetWorldOffset(new Vector3(0f, 8.5f, 0f));
                hb.EnsureUIComponents();
                hb.Setup(tower.transform, tower.TeamId, tower.MaxHealth);
                hb.UpdatePositionAndRotation();

                // Wire reference on TowerCombatController
                SerializedObject so = new SerializedObject(tower);
                so.Update();
                var hbProp = so.FindProperty("healthBar");
                if (hbProp != null) hbProp.objectReferenceValue = hb;
                so.ApplyModifiedProperties();

                changed = true;
                if (!silent)
                    Debug.Log($"[JuicyHealthBarSetup] Configured Juicy Tower Health Bar on '{tower.name}' (Team {tower.TeamId})");
            }
        }

        return changed;
    }

    private static void SetupAllPrefabs(bool silent)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/prefabs" });
        int updatedCount = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var puc = prefab.GetComponent<PeasantUnitController>();
            if (puc == null) continue;

            bool isMachine = puc.IsMachine ||
                             path.ToLower().Contains("catapult") ||
                             path.ToLower().Contains("ram") ||
                             path.ToLower().Contains("ballista") ||
                             path.ToLower().Contains("cart");

            // Look for healthbar transform
            Transform hb = prefab.transform.Find("healthbar");
            if (hb == null)
            {
                var allT = prefab.GetComponentsInChildren<Transform>(true);
                foreach (var t in allT)
                {
                    if (t.name.Equals("healthbar", System.StringComparison.OrdinalIgnoreCase))
                    {
                        hb = t;
                        break;
                    }
                }
            }

            if (hb != null)
            {
                var juicy = hb.GetComponent<JuicyUnitHealthBar>();
                if (juicy == null)
                {
                    juicy = hb.gameObject.AddComponent<JuicyUnitHealthBar>();
                }

                if (isMachine)
                {
                    juicy.ConfigureAsMachine();
                }
                else
                {
                    juicy.ConfigureAsSoldier();
                }

                var tiltFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UIPACK/ResourcesData/Fonts/TMP_TiltWarp_Outline.asset");
                if (tiltFont != null) juicy.DamageNumberFont = tiltFont;

                juicy.EnsureUIComponents();

                // Wire reference on PeasantUnitController
                SerializedObject so = new SerializedObject(puc);
                so.Update();
                var juicyProp = so.FindProperty("juicyHealthBar");
                if (juicyProp != null) juicyProp.objectReferenceValue = juicy;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(prefab);
                updatedCount++;
            }
        }

        AssetDatabase.SaveAssets();

        if (!silent)
            Debug.Log($"[JuicyHealthBarSetup] Configured JuicyUnitHealthBar on {updatedCount} character and machine prefabs.");
    }
}
