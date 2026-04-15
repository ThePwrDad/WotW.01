using Unity.Cinemachine;
using UnityEngine;
using WeightLifter;

public class PlayerCameraScaler : MonoBehaviour
{
    [Tooltip("How aggressively camera pulls back as the player grows. 1 = proportional, 2 = recommended.")]
    public float distanceScaleSensitivity = 2.0f;

    private CinemachineThirdPersonFollow _thirdPersonFollow;
    private float _baseDistance;
    private Transform _playerRoot;
    private float _initialScale;

    private void Start()
    {
        //Find the Cinemachine camera in the Scene
        var vcam = FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null)
        _thirdPersonFollow = vcam.GetComponent<CinemachineThirdPersonFollow>();
        //Fallback: search scene directly
        if (_thirdPersonFollow == null)
        _thirdPersonFollow = FindFirstObjectByType<CinemachineThirdPersonFollow>();

        if (_thirdPersonFollow != null)
        _baseDistance = _thirdPersonFollow.CameraDistance;
        else
        Debug.LogWarning("PlayerCameraScaler: Could not find CinemachineThirdPersonFollow.", this);

        var stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            _playerRoot =stats.transform;
            _initialScale = Mathf.Max(0.001f, _playerRoot.localScale.x);

        }

    }

    private void LateUpdate()
    {
        if (_thirdPersonFollow == null || _playerRoot == null  || _initialScale <=0f) return;

        float scaleRatio = _playerRoot.localScale.x / _initialScale;
        float adjustedRatio = Mathf.Pow(scaleRatio, distanceScaleSensitivity);
        _thirdPersonFollow.CameraDistance = _baseDistance * adjustedRatio;
        
    }
}