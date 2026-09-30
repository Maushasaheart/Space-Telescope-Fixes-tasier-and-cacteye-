using System;

// Pure geometry: coordinates are relative to the telescope camera in scaled space.
internal static class ScopeVisibility
{
    internal static bool NeedsTextures(double x, double y, double z, double radius,
        double verticalFov, double aspect, int height, double near, double far)
    {
        if (radius <= 0 || height <= 0 || aspect <= 0 || verticalFov <= 0 || verticalFov >= 179)
            return false;
        double distanceSquared = x * x + y * y + z * z;
        if (double.IsNaN(distanceSquared) || double.IsInfinity(distanceSquared)) return false;
        double paddedRadius = radius * 1.02;
        if (z + paddedRadius < near || z - paddedRadius > far) return false;
        double tanV = Math.Tan(verticalFov * Math.PI / 360.0);
        double tanH = tanV * aspect;
        if (Math.Abs(x) - z * tanH > paddedRadius * Math.Sqrt(1 + tanH * tanH)) return false;
        if (Math.Abs(y) - z * tanV > paddedRadius * Math.Sqrt(1 + tanV * tanV)) return false;
        if (distanceSquared <= paddedRadius * paddedRadius) return true;
        double pixels = radius * height / (tanV * Math.Sqrt(distanceSquared - radius * radius));
        return pixels >= 1.0;
    }
}
