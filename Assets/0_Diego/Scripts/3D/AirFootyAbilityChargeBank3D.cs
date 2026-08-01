using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AirFootyAbilityChargeBank3D : MonoBehaviour
{
    [SerializeField, Min(1)] private int maximumCharges = 3;
    [SerializeField, Min(0.05f)] private float secondsPerCharge = 0.9f;

    private float rechargeProgress;

    public event Action ChargesChanged;

    public int CurrentCharges { get; private set; }
    public int MaximumCharges => maximumCharges;
    public float RechargeFraction =>
        CurrentCharges >= maximumCharges
            ? 1f
            : Mathf.Clamp01(rechargeProgress / secondsPerCharge);

    private void Awake()
    {
        CurrentCharges = maximumCharges;
    }

    private void Update()
    {
        if (CurrentCharges >= maximumCharges ||
            Mathf.Approximately(Time.timeScale, 0f))
        {
            return;
        }

        rechargeProgress += Time.deltaTime;
        while (CurrentCharges < maximumCharges &&
               rechargeProgress >= secondsPerCharge)
        {
            rechargeProgress -= secondsPerCharge;
            CurrentCharges++;
            ChargesChanged?.Invoke();
        }

        if (CurrentCharges >= maximumCharges)
        {
            rechargeProgress = 0f;
        }
    }

    public bool TrySpend()
    {
        if (CurrentCharges <= 0)
        {
            return false;
        }

        CurrentCharges--;
        if (CurrentCharges == maximumCharges - 1)
        {
            rechargeProgress = 0f;
        }
        ChargesChanged?.Invoke();
        return true;
    }

    public void Refill()
    {
        bool changed =
            CurrentCharges != maximumCharges ||
            rechargeProgress > 0f;
        CurrentCharges = maximumCharges;
        rechargeProgress = 0f;
        if (changed)
        {
            ChargesChanged?.Invoke();
        }
    }

    private void OnValidate()
    {
        maximumCharges = Mathf.Max(1, maximumCharges);
        secondsPerCharge = Mathf.Max(0.05f, secondsPerCharge);
        if (!Application.isPlaying)
        {
            CurrentCharges = maximumCharges;
        }
    }
}
