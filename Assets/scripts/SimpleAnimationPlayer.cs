using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flexible helper component to control and play Animator animations.
/// Can auto-play on Start/Awake, loop or trigger specific states by name or index,
/// adjust playback speed, delay execution, or be triggered by events/code/Inspector context menu.
/// </summary>
[DisallowMultipleComponent]
public class SimpleAnimationPlayer : MonoBehaviour
{
    public enum PlayMode
    {
        PlayStateName,
        PlayDefaultState,
        CrossFade,
        TriggerParameter
    }

    [Header("Animator Reference")]
    [Tooltip("Target Animator. If left empty, will automatically grab from this object or its children.")]
    [SerializeField] private Animator animator;

    [Header("Playback Settings")]
    [Tooltip("Play automatically when this object awakens or starts.")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("Delay in seconds before starting playback.")]
    [SerializeField] private float startDelay = 0f;

    [Tooltip("Playback speed multiplier (1 = normal speed, 0 = paused).")]
    [SerializeField] private float playbackSpeed = 1f;

    [Tooltip("Whether the animation should repeat/loop continuously.")]
    [SerializeField] private bool loop = true;

    [Header("Animation Target")]
    [SerializeField] private PlayMode mode = PlayMode.PlayDefaultState;

    [Tooltip("Name of the state or trigger parameter to play.")]
    [SerializeField] private string stateOrParameterName = "spike|spikeAction";

    [Tooltip("Layer index in Animator Controller (usually 0).")]
    [SerializeField] private int layer = 0;

    [Tooltip("Transition duration when using CrossFade mode.")]
    [SerializeField] private float crossFadeDuration = 0.15f;

    private Coroutine playRoutine;

    public Animator TargetAnimator => animator;

    private void Awake()
    {
        EnsureAnimator();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    /// <summary>
    /// Finds or assigns the Animator component automatically if not set.
    /// </summary>
    public void EnsureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    /// <summary>
    /// Starts playing the configured animation.
    /// </summary>
    [ContextMenu("Play Animation")]
    public void Play()
    {
        EnsureAnimator();
        if (animator == null)
        {
            Debug.LogWarning($"[SimpleAnimationPlayer] No Animator found on {gameObject.name} or its children.", this);
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        animator.speed = playbackSpeed;

        do
        {
            switch (mode)
            {
                case PlayMode.PlayDefaultState:
                    animator.Play(0, layer, 0f);
                    break;

                case PlayMode.PlayStateName:
                    if (!string.IsNullOrEmpty(stateOrParameterName))
                        animator.Play(stateOrParameterName, layer, 0f);
                    else
                        animator.Play(0, layer, 0f);
                    break;

                case PlayMode.CrossFade:
                    if (!string.IsNullOrEmpty(stateOrParameterName))
                        animator.CrossFade(stateOrParameterName, crossFadeDuration, layer, 0f);
                    else
                        animator.CrossFadeInFixedTime(0, crossFadeDuration, layer, 0f);
                    break;

                case PlayMode.TriggerParameter:
                    if (!string.IsNullOrEmpty(stateOrParameterName))
                        animator.SetTrigger(stateOrParameterName);
                    break;
            }

            if (!loop) yield break;

            // Wait until next frame so Animator state updates its normalizedTime
            yield return null;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            float length = stateInfo.length > 0f ? (stateInfo.length / Mathf.Max(0.001f, playbackSpeed)) : 1f;

            yield return new WaitForSeconds(length);

        } while (loop);
    }

    /// <summary>
    /// Play a specific animation state by name immediately.
    /// </summary>
    public void PlayState(string stateName, float speed = 1f)
    {
        EnsureAnimator();
        if (animator == null) return;

        this.stateOrParameterName = stateName;
        this.playbackSpeed = speed;
        animator.speed = speed;
        animator.Play(stateName, layer, 0f);
    }

    /// <summary>
    /// Trigger an Animator parameter by name.
    /// </summary>
    public void Trigger(string triggerName)
    {
        EnsureAnimator();
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// Pause animation playback.
    /// </summary>
    [ContextMenu("Pause Animation")]
    public void Pause()
    {
        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    /// <summary>
    /// Resume animation playback at configured speed.
    /// </summary>
    [ContextMenu("Resume Animation")]
    public void Resume()
    {
        if (animator != null)
        {
            animator.speed = playbackSpeed;
        }
    }

    /// <summary>
    /// Stop playback coroutine and rewind to beginning.
    /// </summary>
    [ContextMenu("Stop & Rewind")]
    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        if (animator != null)
        {
            animator.speed = 0f;
            animator.Play(0, layer, 0f);
        }
    }

    /// <summary>
    /// Set custom playback speed dynamically.
    /// </summary>
    public void SetSpeed(float speed)
    {
        playbackSpeed = speed;
        if (animator != null)
        {
            animator.speed = speed;
        }
    }
}

