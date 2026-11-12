using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Camera FPS (first-person): orientarea vine din două unghiuri Euler, yaw și pitch,
/// controlate cu mouse-ul; deplasarea se face cu W A S D.
/// În modul "mers" (implicit) camera rămâne la înălțimea ochilor; în modul "zbor liber"
/// (free-fly, tasta G) se deplasează pe direcția privirii.
/// </summary>
public class FpsCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------

    private Vector3 _position;
    public override Vector3 Position => _position;

    /// <summary>Girația (yaw), în grade: rotația stânga-dreapta, în jurul axei Y. -90° = privire spre -Z.</summary>
    public float Yaw { get; private set; }

    /// <summary>Tangajul (pitch), în grade: rotația sus-jos. Limitat la ±MaxPitch.</summary>
    public float Pitch { get; private set; }

    /// <summary>Zbor liber (true) sau mers pe sol (false). Comutat cu tasta G (facultativ, TODO 5.7).</summary>
    public bool FreeFly { get; set; }

    // Baza camerei: trei vectori unitate, perpendiculari doi câte doi (secțiunea 5).
    private Vector3 _front = -Vector3.UnitZ;
    private Vector3 _right = Vector3.UnitX;
    private Vector3 _up = Vector3.UnitY;

    public override Vector3 Front => _front;

    // --- Parametri ----------------------------------------------------------------

    public const float MaxPitch = 89.0f;               // grade; de ce nu 90, vezi secțiunea 5.3
    public float MoveSpeed = 3.0f;                     // unități de lume pe secundă
    public float SprintFactor = 2.5f;                  // Shift ținut apăsat
    public float MouseSensitivity = 0.1f;              // grade pe pixel
    public float FovStep = 2.0f;                       // grade pentru un pas al rotiței

    private static readonly Vector3 WorldUp = Vector3.UnitY;
    private static readonly Vector3 StartPosition = new(0.0f, 1.7f, 6.0f);   // 1,7 m = înălțimea ochilor
    private const float StartYaw = -90.0f;
    private const float StartPitch = 0.0f;

    public FpsCamera()
    {
        Reset();
    }

    public override string Name => FreeFly ? "FPS – zbor liber" : "FPS – mers";

    /// <summary>Matricea view: camera aflată în Position privește spre punctul Position + Front.</summary>
    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(_position, _position + _front, _up);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // --- 1. Orientarea: mouse-ul modifică yaw și pitch -------------------------

        // TODO 5.5 (a): deplasarea pe orizontală modifică yaw, cea pe verticală modifică pitch.
        Yaw += mouseDelta.X * MouseSensitivity;
        Pitch -= mouseDelta.Y * MouseSensitivity; // semn schimbat

        // Limitați pitch-ul la intervalul [-MaxPitch, MaxPitch].
        Pitch = Math.Clamp(Pitch, -MaxPitch, MaxPitch);

        UpdateVectors();

        // --- 2. FOV: rotița mouse-ului --------------------------------------------

        // TODO 5.1 (c): rotița în sus (ScrollDelta.Y > 0) micșorează FOV-ul (efect de zoom)
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            // Înmulțim cu -1 pentru a inversa logica: scroll up (Y pozitiv) va scădea FieldOfView
            ChangeFov(-mouse.ScrollDelta.Y * FovStep);
        }


        // --- 3. Deplasarea: W A S D ---------------------------------------------------

        // --- 3. Deplasarea: W A S D ---------------------------------------------------

        // TODO 5.6
        // (1) Proiectăm direcția Front pe planul orizontal (Y devine 0) și o normalizăm.
        // Asta ne asigură că dacă privim în jos și apăsăm W, nu intrăm în podea, ci mergem înainte pe orizontală.
        Vector3 forward = Vector3.Normalize(new Vector3(_front.X, 0.0f, _front.Z));

        // (2) Calculăm vectorul "move" ca sumă a direcțiilor tastelor apăsate
        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= forward;
        if (keyboard.IsKeyDown(Keys.D)) move += _right;
        if (keyboard.IsKeyDown(Keys.A)) move -= _right;

        // (3) Normalizăm vectorul "move" ca să nu ne mișcăm mai repede pe diagonală (W + D)
        if (move.LengthSquared > 0.0f) // Verificăm să nu fie 0 pentru a evita erori (NaN) la normalizare
        {
            move = Vector3.Normalize(move);
        }

        // (4) Calculăm viteza curentă, aplicând factorul de sprint dacă e apăsat Shift stânga
        float currentSpeed = MoveSpeed;
        if (keyboard.IsKeyDown(Keys.LeftShift))
        {
            currentSpeed *= SprintFactor;
        }

        // Actualizăm poziția înmulțind cu dt (timpul scurs de la ultimul cadru)
        _position += move * currentSpeed * dt;

        // TODO 5.7 (facultativ): în zbor liber (FreeFly), "forward" este chiar _front,
        //           iar Space urcă și Ctrl stânga coboară (pe axa Y a lumii, WorldUp).
    }

    /// <summary>
    /// Recalculează baza camerei din yaw și pitch (unghiuri Euler -> vector de direcție).
    /// </summary>
    private void UpdateVectors()
    {
        // TODO 5.5 (b): Front din yaw și pitch, apoi Right și Up prin produse vectoriale; normalizați.

        // 1. Conversia unghiurilor în radiani
        float yawRad = MathHelper.DegreesToRadians(Yaw);
        float pitchRad = MathHelper.DegreesToRadians(Pitch);

        // 2. Calculul vectorului _front folosind trigonometria pe sferă
        Vector3 front;
        front.X = MathF.Cos(pitchRad) * MathF.Cos(yawRad);
        front.Y = MathF.Sin(pitchRad);
        front.Z = MathF.Cos(pitchRad) * MathF.Sin(yawRad);
        _front = Vector3.Normalize(front);

        // 3. Calculul lui _right (perpendicular pe _front și pe axa Y a lumii)
        _right = Vector3.Normalize(Vector3.Cross(_front, WorldUp));

        // 4. Calculul lui _up (perpendicular pe _right și _front)
        _up = Vector3.Normalize(Vector3.Cross(_right, _front));
    }

    public override void Reset()
    {
        _position = StartPosition;
        Yaw = StartYaw;
        Pitch = StartPitch;
        FieldOfView = 60.0f;
        UpdateVectors();
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Poziție        : ({_position.X,6:F2}, {_position.Y,6:F2}, {_position.Z,6:F2})";
        yield return $"Yaw / pitch    : {NormalizeAngle(Yaw),7:F1}° / {Pitch,6:F1}°";
        yield return $"Front          : ({_front.X,6:F2}, {_front.Y,6:F2}, {_front.Z,6:F2})";
        yield return $"Right          : ({_right.X,6:F2}, {_right.Y,6:F2}, {_right.Z,6:F2})";
        yield return $"Up             : ({_up.X,6:F2}, {_up.Y,6:F2}, {_up.Z,6:F2})";
    }

    /// <summary>Aduce un unghi în intervalul (-180°, 180°] (doar pentru afișare).</summary>
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}
