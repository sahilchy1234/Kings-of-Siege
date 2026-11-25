using System.Collections;
using UnityEngine;

/// <summary>
/// Attached automatically by SimpleGameObjectPool to track which prefab an object came from.
/// </summary>
public class PoolReturnTag : MonoBehaviour
{
    public GameObject SourcePrefab;

    private Coroutine _returnRoutine;

    public void ReturnToPool(float delay)
    {
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _returnRoutine = StartCoroutine(ReturnAfterDelay(delay));
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _returnRoutine = null;
        SimpleGameObjectPool.Return(gameObject);
    }

    private void OnDisable()
    {
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }
    }
}
