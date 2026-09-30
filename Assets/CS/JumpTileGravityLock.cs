// Airborne frames and non-support contacts do not release the jump tile's J lock.
internal sealed class JumpTileGravityLock
{
    public bool IsLocked { get; private set; }
    private bool touchingPreviousJump;

    public bool ObserveSupport(bool touchingJumpTile, bool touchingOtherTile)
    {
        if (touchingJumpTile)
        {
            if (touchingPreviousJump) return false;
            touchingPreviousJump = true;
            IsLocked = true;
            return true;
        }
        touchingPreviousJump = false;
        if (touchingOtherTile) IsLocked = false;
        return false;
    }
}

// Analytic projectile motion: horizontal velocity never stops at the range marker.
internal sealed class JumpPadHorizontalFlight
{
    public double Gravity { get; private set; }
    private double horizontalSpeed, verticalSpeed;
    public void Begin(double velocityX, double referenceDistance, double baseGravity)
    {
        horizontalSpeed = velocityX;
        verticalSpeed = 0;
        // At the reference distance, both pads have the same reference drop.
        // Defaults: 30 units/s, 10 units, gravity 60 => about 3.33 units down.
        double ratio = System.Math.Abs(velocityX) / 30 * 10 / System.Math.Max(0.01, referenceDistance);
        Gravity = System.Math.Max(0.01, baseGravity) * ratio * ratio;
    }
    public void Step(double dt, out double velocityX, out double velocityY)
    {
        velocityX = velocityY = 0;
        if (dt <= 0) return;
        velocityX = horizontalSpeed;
        // Average velocity over the step integrates the parabola exactly.
        velocityY = verticalSpeed - 0.5 * Gravity * dt;
        verticalSpeed -= Gravity * dt;
    }
}
