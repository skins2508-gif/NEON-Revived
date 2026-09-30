using UnityEngine;

/// <summary>Low-angle projectile launch applied only after leaving this Long tilemap.</summary>
[DisallowMultipleComponent]
public sealed class LongNoteLaunchSettings : MonoBehaviour
{
    [Header("포물선 발사")]
    [Min(0.01f)] public float launchSpeed = 30f;
    [Tooltip("수평 기준 발사 각도. 반전 중력에서는 아래쪽으로 대칭 발사합니다.")]
    [Range(0f, 45f)] public float launchAngle = 10f;
    [Tooltip("비행 시작부터 적용되는 중력 가속도 (월드 유닛/초²)")]
    [Min(0.01f)] public float gravity = 16.02906f;
    [Tooltip("착지하지 못했을 때도 이 시간이 지나면 일반 달리기로 복귀합니다.")]
    [Min(0.02f)] public float maximumFlightSeconds = 0.65f;

    // Called only when the component is first added. Existing Inspector values are preserved.
    private void Reset()
    {
        switch (gameObject.name)
        {
            case "Long2": launchSpeed = 45f; maximumFlightSeconds = 0.85f; break;
            case "Long3": launchSpeed = 65f; maximumFlightSeconds = 1.1f; break;
            case "Long4": launchSpeed = 90f; maximumFlightSeconds = 1.4f; break;
            default: launchSpeed = 30f; maximumFlightSeconds = 0.65f; break;
        }
        gravity = 2f * launchSpeed * Mathf.Sin(launchAngle * Mathf.Deg2Rad) / maximumFlightSeconds;
    }
}

// Analytic per-step integration, independent of Unity for deterministic verification.
internal sealed class LongNoteTrajectory
{
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }
    public double Elapsed { get; private set; }
    public double Duration { get; private set; }
    private double accelerationY;
    public bool Finished => Elapsed >= Duration;

    public void Begin(double speed, double angleDegrees, double gravity, double duration, bool reverseGravity)
    {
        double angle = System.Math.Max(0, System.Math.Min(45, angleDegrees)) * System.Math.PI / 180;
        double sign = reverseGravity ? -1 : 1;
        speed = System.Math.Max(0.01, speed);
        VelocityX = speed * System.Math.Cos(angle);
        VelocityY = sign * speed * System.Math.Sin(angle);
        accelerationY = -sign * System.Math.Max(0.01, gravity);
        Duration = System.Math.Max(0.02, duration);
        Elapsed = 0;
    }

    public void Step(double deltaTime, out double x, out double y)
    {
        double dt = System.Math.Min(System.Math.Max(0, deltaTime), System.Math.Max(0, Duration - Elapsed));
        x = VelocityX * dt;
        y = VelocityY * dt + 0.5 * accelerationY * dt * dt;
        VelocityY += accelerationY * dt;
        Elapsed = System.Math.Min(Duration, Elapsed + dt);
    }
}
