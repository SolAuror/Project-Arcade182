using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class AirFootyCameraFx : MonoBehaviour
{
    [Header("Shake")]
    [SerializeField, Min(0f)] private float maxShakeOffset = 0.045f;
    [SerializeField, Min(0f)] private float maxShakeRollDegrees = 0.25f;
    [SerializeField, Min(0.1f)] private float traumaDecayPerSecond = 2.4f;
    [SerializeField, Min(0.1f)] private float shakeNoiseSpeed = 16f;

    [Header("Lens Kick")]
    [SerializeField, Min(0f)] private float fovKickDegrees = 0.45f;

    private Camera fxCamera;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private float baseFieldOfView;
    private float baseOrthographicSize;
    private float trauma;
    private AirFootyCinemachineCameraRig cinemachineRig;

    public void AddTrauma(float amount)
    {
        if (cinemachineRig == null)
        {
            cinemachineRig = FindFirstObjectByType<AirFootyCinemachineCameraRig>();
        }
        if (cinemachineRig != null && cinemachineRig.IsReady)
        {
            cinemachineRig.AddImpact(amount);
            return;
        }

        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
    }

    public void RefreshBaseline()
    {
        if (fxCamera == null)
        {
            fxCamera = GetComponent<Camera>();
        }

        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseFieldOfView = fxCamera.fieldOfView;
        baseOrthographicSize = fxCamera.orthographicSize;
    }

    private void Awake()
    {
        fxCamera = GetComponent<Camera>();
        RefreshBaseline();
    }

    private void OnDisable()
    {
        trauma = 0f;
        RestoreBaseline();
    }

    private void LateUpdate()
    {
        if (trauma <= 0f)
        {
            return;
        }

        trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * Time.unscaledDeltaTime);
        float shake = trauma * trauma;
        if (shake <= 0.0001f)
        {
            RestoreBaseline();
            return;
        }

        float time = Time.unscaledTime * shakeNoiseSpeed;
        Vector3 offset = new Vector3(
            (Mathf.PerlinNoise(time, 0.31f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0.73f, time) - 0.5f) * 2f,
            0f) * (maxShakeOffset * shake);
        float roll = (Mathf.PerlinNoise(time, 8.9f) - 0.5f) *
                     2f *
                     maxShakeRollDegrees *
                     shake;

        transform.SetLocalPositionAndRotation(
            baseLocalPosition + offset,
            baseLocalRotation * Quaternion.Euler(0f, 0f, roll));

        if (fxCamera.orthographic)
        {
            float kick = baseFieldOfView > 0f ? fovKickDegrees / baseFieldOfView : 0f;
            fxCamera.orthographicSize = baseOrthographicSize * (1f + kick * shake);
        }
        else
        {
            fxCamera.fieldOfView = baseFieldOfView + fovKickDegrees * shake;
        }
    }

    private void RestoreBaseline()
    {
        if (fxCamera == null)
        {
            return;
        }

        transform.SetLocalPositionAndRotation(baseLocalPosition, baseLocalRotation);
        fxCamera.fieldOfView = baseFieldOfView;
        fxCamera.orthographicSize = baseOrthographicSize;
    }
}
