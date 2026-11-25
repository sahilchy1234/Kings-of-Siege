using UnityEngine;

/// <summary>
/// Visual Scene Gizmo for the Arrow Launch Point of a defense tower.
/// Allows easy positioning and visualization in the Unity Editor Scene view.
/// </summary>
public class ArrowLaunchPointGizmo : MonoBehaviour
{
    [SerializeField] private Color gizmoColor = new Color(1f, 0.82f, 0.2f, 0.95f);
    [SerializeField] private float sphereRadius = 0.35f;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, sphereRadius);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, sphereRadius * 0.75f);
        Gizmos.DrawWireSphere(transform.position, sphereRadius * 1.35f);
        Gizmos.DrawRay(transform.position, transform.forward * 2.0f);
    }
}
