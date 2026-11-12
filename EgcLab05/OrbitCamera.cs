using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public class OrbitCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------
    public Vector3 Target { get; set; } = Vector3.Zero; // Punctul spre care privește camera
    public float Distance { get; set; } = 10.0f;        // Raza sferei de orbitare

    // Azimutul: unghiul de rotație în jurul axei Y (stânga/dreapta)
    public float Azimuth { get; set; } = -90.0f;
    // Elevația: unghiul deasupra/dedesubtul planului orizontal (sus/jos)
    public float Elevation { get; set; } = 20.0f;

    // Proprietăți cerute de clasa de bază
    public override string Name => "Orbită";

    private Vector3 _position;
    public override Vector3 Position => _position;

    public override Vector3 Front => Vector3.Normalize(Target - _position);

    // Matricea View: camera privește din _position către Target
    public override Matrix4 ViewMatrix => Matrix4.LookAt(_position, Target, Vector3.UnitY);

    // Limite și viteze
    private const float MaxElevation = 89.0f;
    private const float MinDistance = 2.0f;
    private const float MaxDistance = 50.0f;
    private const float RotationSpeed = 0.5f; // Grade per pixel
    private const float KeyRotationSpeed = 90.0f; // Grade per secundă din taste
    private const float ZoomSpeed = 1.5f;

    public OrbitCamera()
    {
        UpdatePosition();
    }

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        bool changed = false;

        // 1. Rotația din mouse (când click stânga este apăsat)
        if (mouse.IsButtonDown(MouseButton.Left) && mouseDelta != Vector2.Zero)
        {
            Azimuth += mouseDelta.X * RotationSpeed;
            Elevation += mouseDelta.Y * RotationSpeed; // Y crește în jos, ecran->unghi
            changed = true;
        }

        // 2. Rotația din săgeți
        if (keyboard.IsKeyDown(Keys.Left)) { Azimuth -= KeyRotationSpeed * dt; changed = true; }
        if (keyboard.IsKeyDown(Keys.Right)) { Azimuth += KeyRotationSpeed * dt; changed = true; }
        if (keyboard.IsKeyDown(Keys.Up)) { Elevation += KeyRotationSpeed * dt; changed = true; }
        if (keyboard.IsKeyDown(Keys.Down)) { Elevation -= KeyRotationSpeed * dt; changed = true; }

        // 3. Zoom-ul (distanța) din rotița mouse-ului (ScrollDelta.Y)
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            Distance -= mouse.ScrollDelta.Y * ZoomSpeed;
            Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
            changed = true;
        }

        // Limităm elevația la +/- 89 grade pentru a evita "răsturnarea" (Gimbal Lock)
        if (changed)
        {
            Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);
            UpdatePosition();
        }
    }

    // Calculează coordonatele carteziene pe baza unghiurilor sferice și a distanței față de țintă
    public void UpdatePosition()
    {
        float azRad = MathHelper.DegreesToRadians(Azimuth);
        float elRad = MathHelper.DegreesToRadians(Elevation);

        float cosEl = MathF.Cos(elRad);
        float sinEl = MathF.Sin(elRad);
        float cosAz = MathF.Cos(azRad);
        float sinAz = MathF.Sin(azRad);

        // Poziția = Țintă + Distanță * Direcție (coordonate sferice -> carteziene)
        _position.X = Target.X + Distance * (cosEl * cosAz);
        _position.Y = Target.Y + Distance * sinEl;
        _position.Z = Target.Z + Distance * (cosEl * sinAz);
    }

    public override void Reset()
    {
        Target = Vector3.Zero;
        Distance = 10.0f;
        Azimuth = -90.0f;
        Elevation = 20.0f;
        FieldOfView = 60.0f;
        UpdatePosition();
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut/Elevație: {NormalizeAngle(Azimuth),7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({_position.X,6:F2}, {_position.Y,6:F2}, {_position.Z,6:F2})";
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}