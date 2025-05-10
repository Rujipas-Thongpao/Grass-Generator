using UnityEngine;

[CreateAssetMenu(fileName = "WindSO", menuName = "ScriptableObjects/WindSO", order = 1)]
public class WindSO : ScriptableObject
{
    [Header("Wind Direction")]
    [Range(-180.0f, 180.0f)]
    public float windDirection = 90.0f;

    [Header("Wind Strength")]
    public float windStrength = 1.0f;
}
