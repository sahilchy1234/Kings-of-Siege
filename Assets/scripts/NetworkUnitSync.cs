using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

/// <summary>
/// Synchronizes peasant unit movement, rotation, animations, health, and combat states
/// across the network using Host-Authoritative state replication.
/// 
/// - On MasterClient (Host): Runs NavMeshAgent, physics, and authoritative combat simulation.
///   Serializes position, rotation, health, and animations.
/// - On Non-MasterClient (Guest): Acts as a smooth kinematic proxy follower. Disables local NavMeshAgent
///   and AI targeting to avoid fighting network position. Interpolates smoothly towards Master's transform.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class NetworkUnitSync : MonoBehaviourPun, IPunObservable
{
    [Header("Interpolation Settings")]
    [SerializeField, Min(1f)] private float positionLerpSpeed = 16f;
    [SerializeField, Min(1f)] private float rotationSlerpSpeed = 16f;
    [SerializeField, Min(1f)] private float teleportDistanceThreshold = 5f;

    private PeasantUnitController controller;
    private NavMeshAgent agent;
    private Animator animator;

    // Network replicated state
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkHealth;
    private byte networkState;
    private bool networkIsRunning;
    private bool isDeadReceived;

    private bool isInitialized = false;
    private byte lastObservedState = 0;

    public void Initialize(PeasantUnitController ctrl, PhotonView view)
    {
        controller = ctrl;
        agent = ctrl.GetComponent<NavMeshAgent>();
        animator = ctrl.GetComponentInChildren<Animator>();

        if (view != null)
        {
            if (view.ObservedComponents == null)
            {
                view.ObservedComponents = new System.Collections.Generic.List<Component>();
            }
            if (!view.ObservedComponents.Contains(this))
            {
                view.ObservedComponents.Add(this);
                view.Synchronization = ViewSynchronization.UnreliableOnChange;
            }
        }

        bool isMaster = PhotonNetwork.IsMasterClient;

        if (!isMaster)
        {
            // Configure guest unit as a proxy follower:
            // 1. Disable NavMeshAgent so it doesn't fight network position updates
            if (agent != null)
            {
                agent.enabled = false;
            }

            // 2. Disable local target auto-acquisition on proxy to prevent conflicting local AI decisions
            if (controller != null)
            {
                controller.canAutoAcquireTargets = false;
            }
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkHealth = controller != null ? controller.CurrentHealth : 100f;
        networkIsRunning = false;
        networkState = (byte)PeasantUnitController.UnitState.Idle;

        isInitialized = true;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (controller == null) return;

        if (stream.IsWriting)
        {
            // === MASTER CLIENT (AUTHORITY) SENDS STATE ===
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(controller.CurrentHealth);
            stream.SendNext((byte)controller.CurrentState);
            stream.SendNext(controller.IsRunning);
        }
        else
        {
            // === GUEST CLIENT (REPLICA) RECEIVES STATE ===
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            float receivedHealth = (float)stream.ReceiveNext();
            byte receivedState = (byte)stream.ReceiveNext();
            bool receivedRunning = (bool)stream.ReceiveNext();

            networkHealth = receivedHealth;
            networkState = receivedState;
            networkIsRunning = receivedRunning;

            // Synchronize health bar and damage
            if (controller != null)
            {
                if (Mathf.Abs(controller.CurrentHealth - receivedHealth) > 0.05f)
                {
                    controller.SetHealthNetworked(receivedHealth);
                }

                // If Master unit died, trigger death on proxy
                if (receivedHealth <= 0f && !controller.IsDead && !isDeadReceived)
                {
                    isDeadReceived = true;
                    controller.DieNetworked();
                }
            }

            // Check if Master unit transitioned into attack state
            if (receivedState == (byte)PeasantUnitController.UnitState.Attacking &&
                lastObservedState != (byte)PeasantUnitController.UnitState.Attacking)
            {
                if (controller != null && !controller.IsDead)
                {
                    controller.PlayAttackAnimationNetworked();
                }
            }
            lastObservedState = receivedState;
        }
    }

    private void Update()
    {
        // Solo/offline or MasterClient drives movement via local NavMeshAgent/controller
        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (!isInitialized || controller == null || controller.IsDead)
        {
            return;
        }

        // Smoothly interpolate position
        float distance = Vector3.Distance(transform.position, networkPosition);
        if (distance > teleportDistanceThreshold)
        {
            // Snap if distance is too large (e.g. spawn teleportation)
            transform.position = networkPosition;
            transform.rotation = networkRotation;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * rotationSlerpSpeed);
        }

        // Synchronize running animation
        if (animator != null)
        {
            animator.SetBool(controller.RunningBoolName, networkIsRunning);
        }
    }
}

