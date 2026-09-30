using UnityEngine;

/// <summary>Controls acceleration only while travelling along a long note.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AutoRunner))]
public sealed class LongNoteSpeed : MonoBehaviour
{
    [Header("롱노트 가속")]
    [SerializeField, Min(0.01f)] private float maximumSpeed = 100f;
    [Tooltip("초당 증가하는 속도. 기본값 6이면 속도 20에서 약 13.3초 후 100에 도달합니다.")]
    [SerializeField, Min(0f)] private float acceleration = 6f;

    public float CurrentSpeed { get; private set; }

    public void BeginRide(float baseSpeed)
    {
        CurrentSpeed = Mathf.Min(Mathf.Max(0.01f, Mathf.Abs(baseSpeed)), Mathf.Max(0.01f, maximumSpeed));
    }

    public float Advance(float deltaTime, float baseSpeed)
    {
        if (!isActiveAndEnabled)
            return CurrentSpeed = Mathf.Max(0.01f, Mathf.Abs(baseSpeed));
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, Mathf.Max(0.01f, maximumSpeed),
            Mathf.Max(0f, acceleration) * Mathf.Max(0f, deltaTime));
        return CurrentSpeed;
    }

    public float ReleaseOverTiles(float tilesTravelled)
    {
        if (isActiveAndEnabled)
            CurrentSpeed = (float)LongNoteRideInput.ReduceSpeed(CurrentSpeed, tilesTravelled);
        return CurrentSpeed;
    }
}

// Tile fractions make release decay continuous, including on diagonal routes.
internal static class LongNoteRideInput
{
    public static double ReduceSpeed(double speed, double tiles)
    {
        // Preserve pre-existing speeds below 20 rather than accelerating on release.
        return System.Math.Max(System.Math.Min(20, speed), speed - System.Math.Max(0, tiles));
    }

    public static double Gauge(double progress, bool held, double seconds, double fillSeconds, double tiles)
    {
        double change = held
            ? System.Math.Max(0, seconds) / System.Math.Max(0.01, fillSeconds)
            : -System.Math.Max(0, tiles) / 100;
        return System.Math.Max(0, System.Math.Min(1, progress + change));
    }
}
