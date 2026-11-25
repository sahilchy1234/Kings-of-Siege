using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Siege.CardsScreen;
using Siege.Profile;
using Siege.Rankings;
using Siege.Settings;
using Siege.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Siege.Tutorial
{
    /// <summary>
    /// First-time player onboarding. Starts itself (no scene wiring needed) and runs once:
    ///   1. Main menu tour: currencies, league, side buttons, Shop / Cards / Rankings / Home tabs, then BATTLE.
    ///   2. Guided first battle vs AI: castles, energy, deploying a unit, casting a power card, capturing towers.
    ///      The battle pauses (timeScale 0) while the King explains, and the AI is slowed for this match.
    ///   3. Back in the menu: welcome gift (coins + gems) and a pointer to chests.
    /// Progress is saved in PlayerPrefs "KoS_Tutorial_Stage", so a closed app resumes at the right part.
    /// Skip at any time (asks to confirm). Replay from Settings > TUTORIAL, or with KoSTutorial.Replay().
    /// </summary>
    public class KoSTutorial : MonoBehaviour
    {
        public const string StageKey = "KoS_Tutorial_Stage";
        public const string RewardKey = "KoS_Tutorial_Rewarded";

        public const int StageNotStarted = 0;
        public const int StageBattlePending = 1;
        public const int StageOutroPending = 2;
        public const int StageComplete = 3;

        public const int RewardCoins = 500;
        public const int RewardGems = 50;

        /// <summary>Set false (e.g. from a debug menu) to stop the tutorial from starting on its own.</summary>
        public static bool AutoStart = true;

        public static KoSTutorial Instance { get; private set; }
        public static bool IsRunning => Instance != null && Instance.running;

        public static int Stage
        {
            get => PlayerPrefs.GetInt(StageKey, StageNotStarted);
            set { PlayerPrefs.SetInt(StageKey, value); PlayerPrefs.Save(); }
        }

        public static bool IsComplete => Stage >= StageComplete;

        private TutorialOverlay overlay;
        private bool running;
        private Coroutine flow;
        private bool paused;
        private float savedTimeScale = 1f;
        private SmartAIPlayer slowedAi;
        private float aiTraining = 1f;
        private float lastAiEnergy = -1f;
        private static FieldInfo aiEnergyField;
        private bool matchEnded;
        private bool battleReminderShown;

        private const string Gold = "<color=#FFD54A>";
        private const string End = "</color>";

        // ───────────────────────────── Boot ─────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[KoSTutorial]");
            DontDestroyOnLoad(go);
            go.AddComponent<KoSTutorial>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            var art = Resources.Load<KoSTutorialArt>("Tutorial/KoSTutorialArt");
            overlay = gameObject.AddComponent<TutorialOverlay>();
            overlay.Build(art);
            overlay.OnSkipConfirmed += OnSkip;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
            RestoreBattleState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            StopFlow();
            if (!AutoStart) return;

            int stage = Stage;
            if (stage >= StageComplete) return;

            if (IsMenuScene(scene))
            {
                if (stage == StageNotStarted) flow = StartCoroutine(MenuFlow());
                else if (stage == StageBattlePending && !battleReminderShown) flow = StartCoroutine(BattleReminderFlow());
                else if (stage == StageOutroPending) flow = StartCoroutine(OutroFlow());
            }
            else if (IsBattleScene(scene) && stage == StageBattlePending)
            {
                flow = StartCoroutine(BattleFlow());
            }
        }

        private static bool IsMenuScene(Scene s) => s.name.ToLowerInvariant().Contains("menu");

        private static bool IsBattleScene(Scene s)
        {
            string n = s.name.ToLowerInvariant();
            return n.Contains("battle") || FindFirstObjectByType<GameManager>() != null;
        }

        // ───────────────────────────── Public API ─────────────────────────────

        /// <summary>Starts the whole tutorial again. In the main menu it begins right away; elsewhere on the next menu load.</summary>
        public static void Replay()
        {
            Stage = StageNotStarted;
            if (Instance == null) { Boot(); return; }
            Instance.battleReminderShown = false;
            if (IsMenuScene(SceneManager.GetActiveScene()))
            {
                Instance.StopFlow();
                Instance.flow = Instance.StartCoroutine(Instance.MenuFlow(0.4f));
            }
        }

        /// <summary>Marks the tutorial finished without rewards.</summary>
        public static void MarkComplete()
        {
            Stage = StageComplete;
            if (Instance != null) Instance.StopFlow();
        }

        private void StopFlow()
        {
            if (flow != null) StopCoroutine(flow);
            flow = null;
            running = false;
            RestoreBattleState();
            if (overlay != null) overlay.Hide();
        }

        private void OnSkip()
        {
            Debug.Log("[KoSTutorial] Skipped by player.");
            Stage = StageComplete;
            if (flow != null) StopCoroutine(flow);
            flow = null;
            running = false;
            RestoreBattleState();
            overlay.Hide();
        }

        // ───────────────────────────── Main menu tour ─────────────────────────────

        private IEnumerator MenuFlow(float delay = 1.6f)
        {
            running = true;
            matchEnded = false;
            overlay.Blank(0f);
            yield return Wait(delay);
            yield return WaitForMenuIdle();
            CloseMenuPages();

            var mmc = MainMenuController.Instance != null ? MainMenuController.Instance : FindFirstObjectByType<MainMenuController>();
            Transform panel = mmc != null ? mmc.transform : FindDeep(null, "Panel");
            Transform canvas = panel != null ? panel.root : null;

            var coins = Rt(FindDeep(panel, "coinCounter"));
            var gems = Rt(FindDeep(panel, "gemsCounter"));
            var avatar = Rt(FindDeep(panel, "avataricon"));
            var gear = Rt(FindDeep(panel, "settings"));
            var league = Rt(FindDeep(panel, "leauge"));
            var side = Rt(FindDeep(panel, "sidepanel"));
            var battle = Rt(FindDeep(panel, "battleButton"));
            var nav = FindDeep(canvas, "downpanel");
            RectTransform Tab(int i) => nav != null && nav.childCount > i ? nav.GetChild(i) as RectTransform : null;
            // The tab roots ("border (i)") are thin frames; the visible icon is border (i)/selected/shop.
            TutorialTarget TabTarget(int i)
            {
                var tab = Tab(i);
                var icon = tab != null ? (tab.Find("selected/shop") as RectTransform ?? tab.Find("selected") as RectTransform) : null;
                return TutorialTarget.UI(icon != null ? icon : tab, 8f);
            }

            overlay.ShowSkipButton(true);
            overlay.SetChapter("Kingdom tour");
            yield return Banner("WELCOME, MY LIEGE!", "A royal tour of your kingdom");

            yield return Say($"Welcome to {Gold}Kings of Siege{End}, my liege! I am your King, and I will teach you how to rule the battlefield.");

            if (coins != null || gems != null)
                yield return Say($"These are your {Gold}Coins{End} and {Gold}Gems{End}. Win battles and open chests to earn more.",
                    TutorialTarget.UIUnion(NonNull(coins, gems)));

            if (avatar != null)
                yield return Say($"Tap your {Gold}portrait{End} any time to see your profile, level and battle record.", TutorialTarget.UI(avatar));

            if (league != null)
                yield return Say($"This is your {Gold}League{End}. Every victory earns {Gold}trophies{End}, and trophies move you up from Bronze to Champion!", TutorialTarget.UI(league));

            if (side != null)
                yield return Say($"{Gold}Missions{End}, {Gold}Daily Rewards{End} and {Gold}Chests{End} live here. Visit them every day for free loot!", TutorialTarget.UI(side));

            // Shop tab
            if (Tab(0) != null)
            {
                yield return Act($"Let's look around the kingdom. Tap the {Gold}SHOP{End}.", TabTarget(0), null, Tab(0));
                if (Aborted) yield break;
                yield return Wait(0.7f);
                yield return Say($"In the {Gold}Shop{End} you can buy chests, new cards, coins and gems. The daily offers change every day, so check back often!", null, 0.26f);
            }

            // Cards tab
            if (Tab(1) != null)
            {
                yield return Act($"Now open your {Gold}CARDS{End}.", TabTarget(1), null, Tab(1));
                if (Aborted) yield break;
                yield return Wait(0.7f);
                yield return Say($"This is your {Gold}Battle Deck{End}: 4 {Gold}army cards{End} that send soldiers, and 8 {Gold}power cards{End} that cast spells like fences, arrow showers and archer towers.", null, 0.26f);
                yield return Say($"Collect card copies and spend coins to {Gold}upgrade{End} them. Every level makes a card 10% stronger. Tap any card to see it or swap it into your deck.", null, 0.26f);
            }

            // Rankings tab
            if (Tab(3) != null)
            {
                yield return Act($"Want to see the greatest kings? Tap {Gold}RANKINGS{End}.", TabTarget(3), null, Tab(3));
                if (Aborted) yield break;
                yield return Wait(0.7f);
                yield return Say($"Here are the top players in the world, your country and the best clans. One day your name will be up here!", null, 0.26f);
                if (Aborted) yield break;
            }

            // The Rankings page covers the nav bar, so the tutorial brings the player home itself.
            CloseMenuPages();
            yield return Wait(0.5f);

            if (gear != null)
                yield return Say($"Sound, graphics and your name are in {Gold}Settings{End}. You can also replay this tutorial from there.", TutorialTarget.UI(gear));

            if (battle != null)
            {
                ForceVsAi(mmc);
                yield return Act($"Enough talk. Time for your first battle! Tap {Gold}BATTLE{End}.", TutorialTarget.UI(battle, 10f), () =>
                {
                    Stage = StageBattlePending;
                    if (mmc != null) mmc.OnBattleButtonClicked();
                }, battle);
                if (Aborted) yield break;
            }
            else
            {
                Stage = StageBattlePending;
            }

            overlay.Hide();
            running = false;
            flow = null;
        }

        /// <summary>Menu tour done but the training battle never ran (app closed, multiplayer...): point at BATTLE again.</summary>
        private IEnumerator BattleReminderFlow()
        {
            running = true;
            matchEnded = false;
            battleReminderShown = true;
            overlay.Blank(0f);
            yield return Wait(1.6f);
            yield return WaitForMenuIdle();
            var mmc = MainMenuController.Instance != null ? MainMenuController.Instance : FindFirstObjectByType<MainMenuController>();
            var battle = Rt(FindDeep(mmc != null ? mmc.transform : null, "battleButton"));
            if (battle == null) { overlay.Hide(); running = false; yield break; }
            overlay.ShowSkipButton(true);
            overlay.SetChapter("Kingdom tour");
            ForceVsAi(mmc);
            yield return Act($"Your training battle awaits, my liege! Tap {Gold}BATTLE{End}.", TutorialTarget.UI(battle, 10f), () =>
            {
                if (mmc != null) mmc.OnBattleButtonClicked();
            }, battle);
            overlay.Hide();
            running = false;
            flow = null;
        }

        // ───────────────────────────── Guided first battle ─────────────────────────────

        private IEnumerator BattleFlow()
        {
            running = true;
            matchEnded = false;

            if (!GameModeManager.IsVsAI)
            {
                Debug.Log("[KoSTutorial] Battle tutorial waits for a match vs AI.");
                running = false;
                yield break;
            }

            // Wait for the ready countdown and the HUD intro.
            float t = 0f;
            while (!MatchReadyScreen.IsMatchStarted && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(2.2f);

            var gm = GameManager.Instance;
            if (gm == null || gm.IsMatchOver) { running = false; yield break; }
            gm.OnMatchFinished += OnMatchFinished;

            int myTeam = GameModeManager.LocalTeamId;
            int enemyTeam = myTeam == 0 ? 1 : 0;
            var myCastle = FindCastle(myTeam);
            var enemyCastle = FindCastle(enemyTeam);
            var enemyTowers = FindObjectives(enemyTeam, enemyCastle);

            SlowAi(0.15f);
            Pause(true);
            overlay.ShowSkipButton(true);
            overlay.SetChapter("Training battle");
            yield return Banner("TRAINING BATTLE", "The battle is paused while you learn");
            if (Aborted) yield break;

            yield return Say($"Welcome to the battlefield, my liege! The battle is paused while I show you how to fight.", null, 0.5f, 0.5f);
            if (Aborted) yield break;

            bool panned = enemyCastle != null && !OnScreen(enemyCastle.transform);
            if (panned) yield return PanCamera(1f, 1.1f);
            if (enemyCastle != null)
                yield return Say($"That is the {Gold}ENEMY CASTLE{End}. Destroy it and you win the battle at once!", TutorialTarget.World(enemyCastle.transform, 6f, 220f), -1f, 0.55f);
            var guard = MostVisible(enemyTowers);
            if (guard != null)
                yield return Say($"These {Gold}towers{End} guard it. Every enemy tower you bring down earns you a {Gold}star{End}.",
                    TutorialTarget.World(guard, 4f, 180f), -1f, 0.55f);
            if (panned && !Aborted) yield return PanCamera(0f, 0.9f);
            if (myCastle != null)
            {
                // The card bar covers our castle, so fade the battle HUD out for this step.
                HideBattleHud(true);
                yield return Wait(0.3f);
                yield return Say($"And this is {Gold}YOUR castle{End}. If it falls, the battle is lost. Defend it!", CastleTarget(myCastle), -1f, 0.55f);
                HideBattleHud(false);
                yield return Wait(0.25f);
            }
            if (Aborted) yield break;

            var energy = SegmentedEnergySliderController.Instance;
            if (energy != null)
                yield return Say($"This is your {Gold}ENERGY{End}. It fills up by itself. Every card costs energy: the number on the card shows how much.",
                    TutorialTarget.UI(energy.transform as RectTransform, 8f), -1f, 0.55f);
            if (Aborted) yield break;

            // Deploy a unit
            var unitCards = UnitCards();
            if (unitCards.Count > 0)
            {
                int before = CountUnits(myTeam);
                var existing = UnitIds(myTeam);
                if (energy != null) energy.AddEnergy(energy.TotalSegments);
                Pause(false);
                yield return Run(new TutorialStep
                {
                    text = $"Tap an {Gold}ARMY CARD{End} to send soldiers into battle!",
                    target = TutorialTarget.UIUnion(unitCards, 8f),
                    handAt = TutorialTarget.UI(unitCards[0], 0f),
                    hand = HandMode.Tap,
                    until = () => CountUnits(myTeam) > before || matchEnded,
                    dim = 0.45f,
                });
                if (Aborted) yield break;
                yield return Wait(0.9f);
                Pause(true);
                var fresh = NewUnits(myTeam, existing);
                if (fresh.Count == 0) { var n = NewestUnit(myTeam); if (n != null) fresh.Add(n); }
                // The soldiers walk out right in front of the castle, under the card bar: hide it while we look at them.
                HideBattleHud(true);
                overlay.Burst(null, 40);
                yield return Say($"Well done! Your soldiers {Gold}march on their own{End} and attack the nearest enemy. Archers shoot from range, knights hit hard up close.",
                    fresh.Count > 0 ? TutorialTarget.WorldBounds(() => RenderBounds(fresh), 1.3f, 300f) : null, -1f, 0.55f);
                HideBattleHud(false);
                if (Aborted) yield break;
            }

            // Cast a power card
            var powerCards = PowerCards();
            if (powerCards.Count > 0 && PowerupManager.Instance != null)
            {
                bool cast = false;
                System.Action<PowerupType> onCast = _ => cast = true;
                PowerupManager.Instance.OnPowerupActivated += onCast;
                if (energy != null) energy.AddEnergy(energy.TotalSegments);
                Pause(false);
                yield return Run(new TutorialStep
                {
                    text = $"{Gold}POWER CARDS{End} are spells. {Gold}Drag{End} one onto your side of the battlefield and let go!",
                    target = TutorialTarget.UIUnion(powerCards, 8f),
                    handAt = TutorialTarget.UI(powerCards[0], 0f),
                    hand = HandMode.Drag,
                    dragTo = () => FieldPoint(myCastle, enemyCastle),
                    until = () => cast || matchEnded,
                    dim = 0.35f,
                });
                if (PowerupManager.Instance != null) PowerupManager.Instance.OnPowerupActivated -= onCast;
                if (Aborted) yield break;
                yield return Wait(1.0f);
                Pause(true);
                overlay.Burst(null, 40);
                yield return Say($"Excellent! {Gold}Fences{End} block the enemy's path, {Gold}Archer Towers{End} shoot from afar, and {Gold}arrows{End} and {Gold}fireballs{End} smash whole groups. Tap a power card without dragging to swap it for another spell of the same kind.", null, 0.6f, 0.5f);
                if (Aborted) yield break;
            }

            // Capturable towers
            var neutral = NeutralTower();
            if (neutral != null)
            {
                bool movedCam = false;
                if (!OnScreen(neutral.transform, 0.12f))
                {
                    float t01 = PanToShow(neutral.transform.position);
                    if (t01 > 0.01f) { yield return PanCamera(t01, 0.9f); movedCam = true; }
                }
                var towerParts = new List<Component> { neutral };
                yield return Say($"Empty towers like this one can be {Gold}captured{End}! Send {Gold}archers{End} near it: they climb in and shoot for you.",
                    TutorialTarget.WorldBounds(() => RenderBounds(towerParts), 1.1f, 220f), -1f, 0.5f);
                if (movedCam && !matchEnded) yield return PanCamera(0f, 0.8f);
            }
            if (Aborted) yield break;

            overlay.Burst();
            yield return Say($"You have {Gold}3 minutes{End}. Destroy more towers than your enemy, or their castle, to win. Now go and conquer, my liege!", null, 0.5f, 0.5f);
            if (Aborted) yield break;

            Stage = StageOutroPending;
            overlay.Hide();
            Pause(false);
            // Keep the training opponent gentle for the rest of the match.
            if (slowedAi != null) aiTraining = 0.6f;
            running = false;
            flow = null;
        }

        private void OnMatchFinished(GameManager.MatchResult result, int stars)
        {
            matchEnded = true;
            if (GameManager.Instance != null) GameManager.Instance.OnMatchFinished -= OnMatchFinished;
            if (Stage == StageBattlePending) Stage = StageOutroPending;
            if (running)
            {
                if (flow != null) StopCoroutine(flow);
                flow = null;
                running = false;
                overlay.Hide();
            }
            RestoreBattleState();
        }

        // ───────────────────────────── After the first battle ─────────────────────────────

        private IEnumerator OutroFlow()
        {
            running = true;
            matchEnded = false;
            overlay.Blank(0f);
            yield return Wait(2.0f);
            yield return WaitForMenuIdle();
            CloseMenuPages();

            var mmc = MainMenuController.Instance != null ? MainMenuController.Instance : FindFirstObjectByType<MainMenuController>();
            Transform panel = mmc != null ? mmc.transform : null;
            var chest = Rt(FindDeep(panel, "button_chest"));
            var coins = Rt(FindDeep(panel, "coinCounter"));
            var gems = Rt(FindDeep(panel, "gemsCounter"));
            var battle = Rt(FindDeep(panel, "battleButton"));
            overlay.ShowSkipButton(false);
            overlay.SetChapter("Royal gift");

            yield return Say($"Well fought, my liege! You have learned the ways of war.");

            bool rewarded = PlayerPrefs.GetInt(RewardKey, 0) == 1;
            if (!rewarded)
            {
                PlayerWallet.AddCoins(RewardCoins);
                PlayerWallet.AddGems(RewardGems);
                PlayerPrefs.SetInt(RewardKey, 1);
                PlayerPrefs.Save();
                overlay.Burst();
                yield return Banner("ROYAL GIFT!", $"+{RewardCoins} coins   +{RewardGems} gems");
                TutorialOverlay.TutorialSound("reward_jingle");
                yield return Say($"Take this royal gift to start your reign: {Gold}{RewardCoins} coins{End} and {Gold}{RewardGems} gems{End}!",
                    (coins != null || gems != null) ? TutorialTarget.UIUnion(NonNull(coins, gems)) : null);
            }

            if (chest != null)
                yield return Say($"Battles also win you {Gold}chests{End}. Open them here to find new cards and gold.", TutorialTarget.UI(chest));

            yield return Say(battle != null
                ? $"Upgrade your cards, climb the leagues and become the greatest king of all. Tap {Gold}BATTLE{End} whenever you are ready!"
                : "Upgrade your cards, climb the leagues and become the greatest king of all!",
                battle != null ? TutorialTarget.UI(battle, 10f) : null);

            Stage = StageComplete;
            overlay.Burst();
            yield return Banner("TUTORIAL COMPLETE!", "Long live the King!");
            Debug.Log("[KoSTutorial] Tutorial complete.");
            overlay.Hide();
            running = false;
            flow = null;
        }

        // ───────────────────────────── Step helpers ─────────────────────────────

        private bool Aborted => overlay.SkipRequested || matchEnded;

        private IEnumerator Run(TutorialStep s)
        {
            if (Aborted) yield break;
            yield return overlay.Run(s);
        }

        private IEnumerator Banner(string title, string subtitle)
        {
            if (Aborted) yield break;
            yield return overlay.Banner(title, subtitle);
        }

        private IEnumerator Say(string text, TutorialTarget target = null, float dialogY = -1f, float dim = 0.62f)
        {
            return Run(new TutorialStep { text = text, target = target, dialogY = dialogY, dim = dim });
        }

        /// <summary>Waits for a tap on the highlight. Any Button under buttonRoot also counts, so a tap that the
        /// real button receives always ends the step even if the highlight is slightly off.</summary>
        private IEnumerator Act(string text, TutorialTarget target, System.Action onTapped = null, Transform buttonRoot = null)
        {
            List<UnityEngine.UI.Button> watch = null;
            if (buttonRoot != null)
            {
                watch = new List<UnityEngine.UI.Button>(buttonRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true));
                var own = buttonRoot.GetComponent<UnityEngine.UI.Button>();
                if (own != null && !watch.Contains(own)) watch.Add(own);
            }
            return Run(new TutorialStep { text = text, target = target, tapTarget = true, hand = HandMode.Tap, onTargetTapped = onTapped, watchButtons = watch, dim = 0.62f });
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ───────────────────────────── Menu helpers ─────────────────────────────

        private static IEnumerator WaitForMenuIdle()
        {
            float t = 0f;
            while (t < 10f && AnyMenuPageOpen()) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static bool AnyMenuPageOpen()
        {
            return (SettingsController.Instance != null && SettingsController.Instance.IsOpen)
                || (ProfileController.Instance != null && ProfileController.Instance.IsOpen);
        }

        private static void CloseMenuPages()
        {
            if (ShopController.Instance != null && ShopController.Instance.IsOpen) ShopController.Instance.Close();
            if (CardsScreenController.Instance != null && CardsScreenController.Instance.IsOpen) CardsScreenController.Instance.Close();
            if (ProfileController.Instance != null && ProfileController.Instance.IsOpen) ProfileController.Instance.Close();
            if (RankingsController.Instance != null) RankingsController.Instance.Close();
        }

        /// <summary>The first battle is always vs AI, even if the BATTLE button normally starts matchmaking.</summary>
        private static void ForceVsAi(MainMenuController mmc)
        {
            if (mmc == null) return;
            var f = typeof(MainMenuController).GetField("battleAction", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(mmc, MainMenuController.BattleAction.PlayVsAI);
        }

        // ───────────────────────────── Battle helpers ─────────────────────────────

        private void Pause(bool on)
        {
            if (on && !paused)
            {
                savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                paused = true;
            }
            else if (!on && paused)
            {
                Time.timeScale = savedTimeScale;
                paused = false;
            }
        }

        /// <summary>Training opponent: the AI only keeps this share of the energy it regenerates.
        /// (BattlePacingDirector rewrites SmartAIPlayer.ExternalEnergyMultiplier every frame, so the energy itself is damped.)</summary>
        private void SlowAi(float share)
        {
            var ai = SmartAIPlayer.Instance;
            if (ai == null) return;
            slowedAi = ai;
            aiTraining = share;
            lastAiEnergy = ai.CurrentEnergy;
            if (aiEnergyField == null) aiEnergyField = typeof(SmartAIPlayer).GetField("currentEnergy", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private readonly List<CanvasGroup> hudGroups = new List<CanvasGroup>();
        private float hudTarget = 1f;

        /// <summary>Fades the battle HUD canvases (cards, energy bar, power cards) out or back in, in unscaled time.</summary>
        private void HideBattleHud(bool hide)
        {
            hudTarget = hide ? 0f : 1f;
            if (!hide || hudGroups.Count > 0) return;
            var roots = new HashSet<Canvas>();
            Component[] parts = { SegmentedEnergySliderController.Instance, FindFirstObjectByType<PowerupUIBar>() };
            foreach (var c in parts)
            {
                if (c == null) continue;
                var cv = c.GetComponentInParent<Canvas>();
                if (cv != null) roots.Add(cv.rootCanvas);
            }
            foreach (var cv in roots)
            {
                if (overlay != null && cv.transform.IsChildOf(overlay.transform)) continue;
                var g = cv.GetComponent<CanvasGroup>();
                if (g == null) g = cv.gameObject.AddComponent<CanvasGroup>();
                hudGroups.Add(g);
            }
        }

        private void UpdateHudFade()
        {
            if (hudGroups.Count == 0) return;
            bool done = true;
            foreach (var g in hudGroups)
            {
                if (g == null) continue;
                g.alpha = Mathf.MoveTowards(g.alpha, hudTarget, Time.unscaledDeltaTime * 5f);
                g.blocksRaycasts = hudTarget > 0.5f;
                if (g.alpha < 1f) done = false;
            }
            if (hudTarget >= 1f && done) hudGroups.Clear();
        }

        private void LateUpdate()
        {
            UpdateHudFade();
            if (slowedAi == null || aiEnergyField == null || aiTraining >= 1f) return;
            float e = slowedAi.CurrentEnergy;
            if (lastAiEnergy >= 0f && e > lastAiEnergy)
            {
                e = lastAiEnergy + (e - lastAiEnergy) * aiTraining;
                aiEnergyField.SetValue(slowedAi, e);
            }
            lastAiEnergy = e;
        }

        /// <summary>Undo the pause. restoreAi also gives the AI its full energy speed back (skip, match end).</summary>
        private void RestoreBattleState(bool restoreAi = true)
        {
            Pause(false);
            foreach (var g in hudGroups) if (g != null) { g.alpha = 1f; g.blocksRaycasts = true; }
            hudGroups.Clear();
            hudTarget = 1f;
            if (restoreAi)
            {
                slowedAi = null;
                aiTraining = 1f;
                lastAiEnergy = -1f;
            }
            if (camOverride && CameraSwipeController.Instance != null)
            {
                CameraSwipeController.Instance.IsCinematicOverride = false;
                CameraSwipeController.Instance.ResetToDefaultPosition();
            }
            camOverride = false;
        }

        private bool camOverride;

        /// <summary>Slides the battle camera toward the enemy (t=1) or back home (t=0) in real time, even while paused.</summary>
        private IEnumerator PanCamera(float t01, float seconds)
        {
            var swipe = CameraSwipeController.Instance;
            if (swipe == null) yield break;
            swipe.IsCinematicOverride = true;
            camOverride = true;
            Vector3 from = swipe.transform.position;
            Vector3 to = swipe.DefaultPosition + swipe.ForwardDirection * swipe.MaxForwardDistance * t01;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                swipe.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            swipe.transform.position = to;
            if (t01 <= 0f)
            {
                swipe.ResetToDefaultPosition();
                swipe.IsCinematicOverride = false;
                camOverride = false;
            }
        }

        private static PeasantObjectiveTarget FindCastle(int team)
        {
            PeasantObjectiveTarget best = null;
            foreach (var o in FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None))
            {
                if (o == null || o.IsDestroyed || o.TeamId != team) continue;
                bool named = o.ObjectiveName != null && o.ObjectiveName.ToLowerInvariant().Contains("castle");
                bool bestNamed = best != null && best.ObjectiveName != null && best.ObjectiveName.ToLowerInvariant().Contains("castle");
                if (best == null || (named && !bestNamed) || (named == bestNamed && o.MaxHealth > best.MaxHealth)) best = o;
            }
            return best;
        }

        private static List<PeasantObjectiveTarget> FindObjectives(int team, PeasantObjectiveTarget except)
        {
            var list = new List<PeasantObjectiveTarget>();
            foreach (var o in FindObjectsByType<PeasantObjectiveTarget>(FindObjectsSortMode.None))
                if (o != null && !o.IsDestroyed && o.TeamId == team && o != except) list.Add(o);
            return list;
        }

        /// <summary>The objective nearest the middle of the screen (null if none is in front of the camera).</summary>
        private static Transform MostVisible(List<PeasantObjectiveTarget> list)
        {
            var cam = Camera.main;
            Transform best = null;
            float bestD = float.MaxValue;
            foreach (var o in list)
            {
                if (o == null) continue;
                if (cam == null) return o.transform;
                Vector3 v = cam.WorldToViewportPoint(o.transform.position);
                if (v.z <= 0f) continue;
                float d = Vector2.Distance(new Vector2(v.x, v.y), new Vector2(0.5f, 0.6f));
                if (d < bestD) { bestD = d; best = o.transform; }
            }
            return best;
        }

        private static bool OnScreen(Transform t, float margin = 0.05f)
        {
            var cam = Camera.main;
            if (cam == null || t == null) return true;
            Vector3 v = cam.WorldToViewportPoint(t.position);
            return v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin + 0.03f && v.y < 0.92f - margin;
        }

        /// <summary>How far forward (0..1 of the swipe range) the camera must move to bring a world point near the
        /// middle of the screen. Moving the camera by d is the same as moving the point by -d.</summary>
        private static float PanToShow(Vector3 world)
        {
            var cam = Camera.main;
            var swipe = CameraSwipeController.Instance;
            if (cam == null || swipe == null) return 0f;
            Vector3 now = swipe.transform.position;
            float best = 0f, bestErr = float.MaxValue;
            for (int i = 0; i <= 40; i++)
            {
                float t01 = i / 40f;
                Vector3 at = swipe.DefaultPosition + swipe.ForwardDirection * swipe.MaxForwardDistance * t01;
                Vector3 v = cam.WorldToViewportPoint(world - (at - now));
                if (v.z <= 0f) continue;
                float err = Mathf.Abs(v.y - 0.5f);
                if (err < bestErr) { bestErr = err; best = t01; }
            }
            return best;
        }

        /// <summary>Combined bounds of the visible meshes and sprites under these objects (effects left out).</summary>
        private static Bounds? RenderBounds<T>(List<T> parts) where T : Component
        {
            bool any = false;
            Bounds b = default;
            foreach (var c in parts)
            {
                if (c == null) continue;
                foreach (var r in c.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            return any ? b : (Bounds?)null;
        }

        private static TutorialTarget CastleTarget(PeasantObjectiveTarget castle)
        {
            var parts = new List<PeasantObjectiveTarget> { castle };
            if (RenderBounds(parts).HasValue) return TutorialTarget.WorldBounds(() => RenderBounds(parts), 0.92f, 340f);
            return TutorialTarget.World(castle.transform, 9f, 340f);
        }

        private static HashSet<int> UnitIds(int team)
        {
            var set = new HashSet<int>();
            foreach (var u in FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None))
                if (u != null && u.TeamId == team) set.Add(u.GetInstanceID());
            return set;
        }

        /// <summary>Units of this team that were not there before, and can be seen (not hidden inside a tower).</summary>
        private static List<PeasantUnitController> NewUnits(int team, HashSet<int> before)
        {
            var list = new List<PeasantUnitController>();
            foreach (var u in FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None))
            {
                if (u == null || u.IsDead || u.TeamId != team || before.Contains(u.GetInstanceID())) continue;
                if (!RenderBounds(new List<PeasantUnitController> { u }).HasValue) continue;
                list.Add(u);
            }
            return list;
        }

        private static TowerCombatController NeutralTower()
        {
            var cam = Camera.main;
            TowerCombatController best = null;
            float bestD = float.MaxValue;
            foreach (var t in FindObjectsByType<TowerCombatController>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsDestroyed || t.TeamId >= 0) continue;
                if (cam == null) return t;
                Vector3 v = cam.WorldToViewportPoint(t.transform.position);
                if (v.z <= 0f) continue;
                float d = Vector2.Distance(new Vector2(v.x, v.y), new Vector2(0.5f, 0.5f));
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private static int CountUnits(int team)
        {
            int n = 0;
            foreach (var u in FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None))
                if (u != null && !u.IsDead && u.TeamId == team) n++;
            return n;
        }

        private static PeasantUnitController NewestUnit(int team)
        {
            PeasantUnitController best = null;
            foreach (var u in FindObjectsByType<PeasantUnitController>(FindObjectsSortMode.None))
                if (u != null && !u.IsDead && u.TeamId == team && (best == null || u.GetInstanceID() < best.GetInstanceID())) best = u;
            return best;
        }

        private static List<RectTransform> UnitCards()
        {
            var list = new List<RectTransform>();
            var e = SegmentedEnergySliderController.Instance;
            if (e != null && e.ButtonConfigs != null)
            {
                foreach (var cfg in e.ButtonConfigs)
                {
                    if (cfg == null || cfg.button == null || !cfg.button.gameObject.activeInHierarchy) continue;
                    if (cfg.button.GetComponent<PowerupButtonUI>() != null) continue;
                    list.Add(cfg.button.transform as RectTransform);
                }
            }
            if (list.Count == 0)
            {
                foreach (var j in FindObjectsByType<JuicyCardButton>(FindObjectsSortMode.None))
                    if (j != null && j.gameObject.activeInHierarchy && j.GetComponent<PowerupButtonUI>() == null) list.Add(j.transform as RectTransform);
            }
            return list;
        }

        private static List<RectTransform> PowerCards()
        {
            var list = new List<RectTransform>();
            var bar = FindFirstObjectByType<PowerupUIBar>();
            if (bar != null && bar.SpawnedButtons != null)
                foreach (var b in bar.SpawnedButtons)
                    if (b != null && b.gameObject.activeInHierarchy) list.Add(b.CardRect != null ? b.CardRect : b.transform as RectTransform);
            if (list.Count == 0)
                foreach (var b in FindObjectsByType<PowerupButtonUI>(FindObjectsSortMode.None))
                    if (b != null && b.gameObject.activeInHierarchy) list.Add(b.transform as RectTransform);
            return list;
        }

        /// <summary>A spot in the middle of the visible field (the player's half at the default camera), for the drag hint.</summary>
        private static Vector2? FieldPoint(PeasantObjectiveTarget mine, PeasantObjectiveTarget enemy)
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        }

        // ───────────────────────────── Generic helpers ─────────────────────────────

        private static RectTransform Rt(Transform t) => t as RectTransform;

        private static List<RectTransform> NonNull(params RectTransform[] items)
        {
            var l = new List<RectTransform>();
            foreach (var i in items) if (i != null) l.Add(i);
            return l;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null)
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var f = FindDeep(root.transform, name);
                    if (f != null) return f;
                }
                return null;
            }
            if (string.Equals(parent.name, name, System.StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var f = FindDeep(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
