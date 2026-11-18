using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Tema acasă – Varianta C: Roata panoramică.
///  - Ierarhie părinte–copil: Butuc (pivot) -> Spițe, Jantă și 8 Pivoți de cabină -> Cabine (corp, acoperiș, stâlpi).
///  - Fiecare cabină compensează rotația roții (-_wheelAngle) pentru a rămâne verticală și se leagănă
///    ca un resort amortizat la accelerare / frânare (Bonus).
///  - Viteza reală urmărește lin viteza țintă prin formula: v <- v + (v_țintă - v) * (1 - e^(-k * dt)).
///  - Tastele 1–8 aduc cabina aleasă jos, la peron, printr-un Tween cu EaseInOut pe drumul cel mai scurt (<= 180°).
/// </summary>
public sealed class RoataPanoramicaScene : Scene
{
    // --- Acțiunile scenei (afișate în meniul H) ---------------------------------------------
    private const string SpeedUp = "crește viteza țintă";
    private const string SpeedDown = "scade viteza țintă";
    private const string SmoothStop = "oprește roata lin";
    private static readonly string[] CabinActions =
    {
        "adu cabina 1 la peron",
        "adu cabina 2 la peron",
        "adu cabina 3 la peron",
        "adu cabina 4 la peron",
        "adu cabina 5 la peron",
        "adu cabina 6 la peron",
        "adu cabina 7 la peron",
        "adu cabina 8 la peron",
    };

    // --- Parametrii constructivi și fizici --------------------------------------------------
    private const int CabinCount = 8;
    private const float WheelRadius = 4.0f;
    private const float HubY = 5.5f;
    private const float RimHalfZ = 0.45f;

    private const float MaxTargetSpeed = 90.0f;     // grade / s
    private const float SpeedStep = 45.0f;          // grade / s² cât timp este ținută săgeata
    private const float SmoothingK = 3.0f;          // constanta de amortizare pentru v -> v_țintă

    // Culori distincte pentru cele 8 cabine
    private static readonly Color4[] CabinColors =
    {
        new(0.95f, 0.30f, 0.30f, 1.0f), // 1 - roșu
        new(0.95f, 0.58f, 0.20f, 1.0f), // 2 - portocaliu
        new(0.95f, 0.85f, 0.22f, 1.0f), // 3 - galben
        new(0.35f, 0.85f, 0.42f, 1.0f), // 4 - verde
        new(0.25f, 0.80f, 0.85f, 1.0f), // 5 - cyan
        new(0.32f, 0.55f, 0.98f, 1.0f), // 6 - albastru
        new(0.68f, 0.42f, 0.95f, 1.0f), // 7 - violet
        new(0.95f, 0.42f, 0.72f, 1.0f), // 8 - roz
    };

    // --- Obiectele ierarhiei ----------------------------------------------------------------
    private SceneObject _hub = null!;
    private readonly SceneObject[] _cabinPivots = new SceneObject[CabinCount];
    private readonly SceneObject[] _cabinBodies = new SceneObject[CabinCount];

    // --- Starea cinematică a roții ----------------------------------------------------------
    private float _wheelAngle;                      // unghiul curent al butucului (grade)
    private float _angularSpeed = 25.0f;            // viteza unghiulară reală v (grade / s)
    private float _targetSpeed = 25.0f;             // viteza țintă v_țintă (grade / s)

    // Tween activ pentru alinierea unei cabine la peron (null când roata se rotește liber)
    private Tween? _alignTween;
    private int _selectedCabin = -1;                // 0..7 când o cabină este chemată la peron

    // --- Bonus: resort amortizat pentru legănarea cabinelor ---------------------------------
    private float _swingAngle;                      // unghiul de balans al cabinelor (grade)
    private float _swingVelocity;                   // viteza unghiulară de balans (grade / s)

    public override string Name => "roata panoramică";

    public override string Status
    {
        get
        {
            if (_alignTween != null && _selectedCabin >= 0)
                return $"aliniere cabina {_selectedCabin + 1} ({_alignTween.Progress:P0})";
            if (_selectedCabin >= 0 && MathF.Abs(_angularSpeed) < 0.1f)
                return $"cabina {_selectedCabin + 1} la peron";
            return $"v = {_angularSpeed:F1}°/s (țintă {_targetSpeed:F1}°/s)";
        }
    }

    public override IEnumerable<string> ActionNames =>
        new[] { SpeedUp, SpeedDown, SmoothStop }.Concat(CabinActions);

    public override void Load(Input input)
    {
        // Poziționăm camera ușor lateral ca să se vadă atât cercul roții, cât și adâncimea 3D
        CameraPosition = new Vector3(3.8f, 6.6f, 14.5f);
        CameraTarget = new Vector3(0.0f, 5.1f, 0.0f);

        // Legarea acțiunilor (nicio tastă nu se citește direct din KeyboardState)
        input.Bind(SpeedUp, Keys.Up);
        input.Bind(SpeedDown, Keys.Down);
        input.Bind(SmoothStop, Keys.Space);

        for (int i = 0; i < CabinCount; i++)
        {
            input.Bind(CabinActions[i], Keys.D1 + i, Keys.KeyPad1 + i);
        }

        // Formele reutilizate în scenă
        Mesh grid = AddMesh(Shapes.Grid(12, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        AddObject("podea", grid);

        BuildSupportAndPlatform(cube, cylinder);
        BuildWheelHierarchy(cube, cylinder);
    }

    private void BuildSupportAndPlatform(Mesh cube, Mesh cylinder)
    {
        // Peronul de îmbarcare, așezat jos sub cabina aflată la -90°
        SceneObject platform = AddObject("peron", cube);
        platform.Transform.Position = new Vector3(0.0f, 0.275f, 0.0f);
        platform.Transform.Scale = new Vector3(2.4f, 0.55f, 2.4f);
        platform.Tint = new Color4(0.45f, 0.48f, 0.54f, 1.0f);

        SceneObject platformEdge = AddObject("margine peron", cube);
        platformEdge.Transform.Position = new Vector3(0.0f, 0.56f, 0.0f);
        platformEdge.Transform.Scale = new Vector3(2.45f, 0.04f, 2.45f);
        platformEdge.Tint = new Color4(0.95f, 0.78f, 0.20f, 1.0f);

        // Picioarele de susținere în formă de A (față și spate)
        float legBaseX = 2.6f;
        float legLength = MathF.Sqrt(legBaseX * legBaseX + HubY * HubY);
        float legAngleDeg = MathHelper.RadiansToDegrees(MathF.Atan2(legBaseX, HubY));

        foreach (float z in new[] { -0.72f, 0.72f })
        {
            SceneObject leftLeg = AddObject("picior stâng", cube);
            leftLeg.Transform.Position = new Vector3(-legBaseX * 0.5f, HubY * 0.5f, z);
            leftLeg.Transform.Scale = new Vector3(0.22f, legLength, 0.22f);
            leftLeg.Transform.Rotation = new Vector3(0.0f, 0.0f, -legAngleDeg);
            leftLeg.Tint = new Color4(0.32f, 0.36f, 0.44f, 1.0f);

            SceneObject rightLeg = AddObject("picior drept", cube);
            rightLeg.Transform.Position = new Vector3(legBaseX * 0.5f, HubY * 0.5f, z);
            rightLeg.Transform.Scale = new Vector3(0.22f, legLength, 0.22f);
            rightLeg.Transform.Rotation = new Vector3(0.0f, 0.0f, legAngleDeg);
            rightLeg.Tint = new Color4(0.32f, 0.36f, 0.44f, 1.0f);
        }

        // Axul fix dintre picioare
        SceneObject mainAxle = AddObject("ax principal", cylinder);
        mainAxle.Transform.Position = new Vector3(0.0f, HubY, 0.0f);
        mainAxle.Transform.Scale = new Vector3(0.35f, 1.75f, 0.35f);
        mainAxle.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        mainAxle.Tint = new Color4(0.65f, 0.68f, 0.72f, 1.0f);
    }

    private void BuildWheelHierarchy(Mesh cube, Mesh cylinder)
    {
        // 1. Părintele suprem al părții mobile: BUTUCUL (pivot în centrul roții)
        _hub = AddObject("butuc", null);
        _hub.Transform.Position = new Vector3(0.0f, HubY, 0.0f);

        // Tamburul central al butucului
        SceneObject hubDrum = AddObject("tambur butuc", cylinder, _hub);
        hubDrum.Transform.Scale = new Vector3(0.95f, 1.15f, 0.95f);
        hubDrum.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
        hubDrum.Tint = new Color4(0.85f, 0.25f, 0.25f, 1.0f);

        // 2. JANTA (24 segmente pe circumferință, față și spate, copil al butucului)
        const int rimSegments = 24;
        float sectorRad = 2.0f * MathF.PI / rimSegments;
        float segmentLength = 2.0f * WheelRadius * MathF.Sin(sectorRad * 0.5f) + 0.03f;
        float midRadius = WheelRadius * MathF.Cos(sectorRad * 0.5f);

        for (int s = 0; s < rimSegments; s++)
        {
            float midAngleRad = (s + 0.5f) * sectorRad;
            float midAngleDeg = MathHelper.RadiansToDegrees(midAngleRad);
            float rx = midRadius * MathF.Cos(midAngleRad);
            float ry = midRadius * MathF.Sin(midAngleRad);

            foreach (float z in new[] { -RimHalfZ, RimHalfZ })
            {
                SceneObject rimSeg = AddObject($"jantă {s}", cube, _hub);
                rimSeg.Transform.Position = new Vector3(rx, ry, z);
                rimSeg.Transform.Scale = new Vector3(0.12f, segmentLength, 0.12f);
                rimSeg.Transform.Rotation = new Vector3(0.0f, 0.0f, midAngleDeg);
                rimSeg.Tint = new Color4(0.88f, 0.88f, 0.92f, 1.0f);
            }
        }

        // 3. SPIȚELE și cele 8 CABINE (copii ai butucului)
        for (int i = 0; i < CabinCount; i++)
        {
            // Cabina 1 (i = 0) pornește la -90° (jos, la peron)
            float angleDeg = -90.0f + i * (360.0f / CabinCount);
            float angleRad = MathHelper.DegreesToRadians(angleDeg);
            float cos = MathF.Cos(angleRad);
            float sin = MathF.Sin(angleRad);

            // Spițe față și spate
            foreach (float z in new[] { -RimHalfZ, RimHalfZ })
            {
                SceneObject spoke = AddObject($"spiță {i + 1}", cube, _hub);
                spoke.Transform.Position = new Vector3(0.5f * WheelRadius * cos, 0.5f * WheelRadius * sin, z);
                spoke.Transform.Scale = new Vector3(0.07f, WheelRadius, 0.07f);
                spoke.Transform.Rotation = new Vector3(0.0f, 0.0f, angleDeg - 90.0f);
                spoke.Tint = new Color4(0.75f, 0.78f, 0.84f, 1.0f);
            }

            // Axul transversal pe care atârnă cabina
            SceneObject pin = AddObject($"ax cabină {i + 1}", cylinder, _hub);
            pin.Transform.Position = new Vector3(WheelRadius * cos, WheelRadius * sin, 0.0f);
            pin.Transform.Scale = new Vector3(0.12f, RimHalfZ * 2.2f, 0.12f);
            pin.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
            pin.Tint = new Color4(0.50f, 0.52f, 0.58f, 1.0f);

            // Pivotul cabinei (copil al butucului; se rotește invers față de roată)
            SceneObject pivot = AddObject($"pivot cabină {i + 1}", null, _hub);
            pivot.Transform.Position = new Vector3(WheelRadius * cos, WheelRadius * sin, 0.0f);
            _cabinPivots[i] = pivot;

            // Tija de suspensie (copil al pivotului cabinei)
            SceneObject hanger = AddObject($"suport cabină {i + 1}", cube, pivot);
            hanger.Transform.Position = new Vector3(0.0f, -0.25f, 0.0f);
            hanger.Transform.Scale = new Vector3(0.06f, 0.50f, 0.06f);
            hanger.Tint = new Color4(0.45f, 0.45f, 0.50f, 1.0f);

            // Nacela / corpul cabinei
            SceneObject body = AddObject($"cabina {i + 1}", cube, pivot);
            body.Transform.Position = new Vector3(0.0f, -0.62f, 0.0f);
            body.Transform.Scale = new Vector3(0.76f, 0.34f, 0.66f);
            body.Tint = CabinColors[i];
            _cabinBodies[i] = body;

            // Acoperișul cabinei
            SceneObject roof = AddObject($"acoperiș cabină {i + 1}", cube, pivot);
            roof.Transform.Position = new Vector3(0.0f, -0.24f, 0.0f);
            roof.Transform.Scale = new Vector3(0.84f, 0.07f, 0.74f);
            roof.Tint = new Color4(0.95f, 0.95f, 0.98f, 1.0f);

            // Doi stâlpi laterali între nacelă și acoperiș
            foreach (float sx in new[] { -0.32f, 0.32f })
            {
                SceneObject post = AddObject($"stâlp cabină {i + 1}", cube, pivot);
                post.Transform.Position = new Vector3(sx, -0.42f, 0.0f);
                post.Transform.Scale = new Vector3(0.05f, 0.34f, 0.05f);
                post.Tint = new Color4(0.85f, 0.85f, 0.90f, 1.0f);
            }
        }
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        // 1. Controlul vitezei din săgeți (Axis) și oprirea lină (Space)
        float throttle = input.Axis(SpeedDown, SpeedUp);
        if (MathF.Abs(throttle) > 0.0f)
        {
            _alignTween = null;
            _selectedCabin = -1;
            _targetSpeed = Math.Clamp(_targetSpeed + throttle * SpeedStep * dt, -MaxTargetSpeed, MaxTargetSpeed);
        }

        if (input.WasPressed(SmoothStop))
        {
            _alignTween = null;
            _selectedCabin = -1;
            _targetSpeed = 0.0f;
            ConsolePanel.PrintMessage("roata panoramică: oprire lină");
        }

        // 2. Tastele 1–8: aduc cabina aleasă jos, la peron, pe drumul cel mai scurt (<= 180°)
        for (int i = 0; i < CabinCount; i++)
        {
            if (input.WasPressed(CabinActions[i]))
            {
                StartAlignToCabin(i);
            }
        }

        // 3. Actualizarea unghiului roții și calculul accelerației unghiulare
        float previousSpeed = _angularSpeed;

        if (_alignTween != null)
        {
            float previousAngle = _wheelAngle;
            _alignTween.Update(dt);
            _wheelAngle = _alignTween.Value.X;
            _angularSpeed = dt > 1e-6f ? (_wheelAngle - previousAngle) / dt : 0.0f;

            if (_alignTween.IsFinished)
            {
                _wheelAngle = _alignTween.To.X;
                _alignTween = null;
                _angularSpeed = 0.0f;
                _targetSpeed = 0.0f;
                ConsolePanel.PrintMessage($"cabina {_selectedCabin + 1} a ajuns la peron");
            }
        }
        else
        {
            // Formula cerută: v <- v + (v_țintă - v) * (1 - e^(-k * dt))
            float factor = 1.0f - MathF.Exp(-SmoothingK * dt);
            _angularSpeed += (_targetSpeed - _angularSpeed) * factor;
            if (MathF.Abs(_targetSpeed) < 1e-3f && MathF.Abs(_angularSpeed) < 0.05f)
            {
                _angularSpeed = 0.0f;
            }
            _wheelAngle += _angularSpeed * dt;
        }

        // 4. Bonus: legănarea cabinelor ca un resort amortizat la accelerare / frânare
        float angularAcceleration = dt > 1e-6f ? (_angularSpeed - previousSpeed) / dt : 0.0f;
        UpdateCabinSpring(dt, angularAcceleration);

        // 5. Aplicăm rotația pe butuc și compensăm rotația pe fiecare cabină (plus balansul)
        _hub.Transform.Rotation = new Vector3(0.0f, 0.0f, _wheelAngle);

        for (int i = 0; i < CabinCount; i++)
        {
            _cabinPivots[i].Transform.Rotation = new Vector3(0.0f, 0.0f, -_wheelAngle + _swingAngle);

            // Evidențiem vizual cabina selectată la peron
            _cabinBodies[i].Tint = (i == _selectedCabin)
                ? Color4.White
                : CabinColors[i];
        }
    }

    private void StartAlignToCabin(int index)
    {
        _selectedCabin = index;
        _targetSpeed = 0.0f;

        // Unghiul inițial al cabinei i pe cerc este (-90° + i * 45°); la peron (-90°) ajunge când:
        // (-90° + i * 45°) + wheelAngle ≡ -90° (mod 360°)  =>  targetAngle = -i * 45°
        float desiredAngle = -index * (360.0f / CabinCount);

        // Cel mai scurt drum unghiular în intervalul [-180°, +180°]
        float delta = (desiredAngle - _wheelAngle) % 360.0f;
        if (delta > 180.0f) delta -= 360.0f;
        if (delta < -180.0f) delta += 360.0f;

        float endAngle = _wheelAngle + delta;
        float duration = MathF.Max(0.6f, MathF.Abs(delta) / 55.0f);

        _alignTween = new Tween(
            new Vector3(_wheelAngle, 0.0f, 0.0f),
            new Vector3(endAngle, 0.0f, 0.0f),
            duration,
            Easing.EaseInOut);

        ConsolePanel.PrintMessage($"aliniere cabina {index + 1}: Δθ = {delta:+0.0;-0.0;0.0}° în {duration:F2} s");
    }

    /// <summary>Integrează oscilația amortizată a cabinelor în sub-pași mici pentru stabilitate și după B.</summary>
    private void UpdateCabinSpring(float dt, float wheelAngularAccel)
    {
        if (dt <= 0.0f) return;

        const float omega = 6.5f;       // pulsația proprie a resortului (rad/s)
        const float zeta = 0.18f;       // factorul de amortizare
        const float driveGain = 0.18f;  // sensibilitatea la accelerația roții
        const float maxStep = 0.005f;

        int steps = Math.Max(1, (int)MathF.Ceiling(dt / maxStep));
        float h = dt / steps;

        for (int s = 0; s < steps; s++)
        {
            float springAccel = -omega * omega * _swingAngle
                                - 2.0f * zeta * omega * _swingVelocity
                                - driveGain * wheelAngularAccel;
            _swingVelocity += springAccel * h;
            _swingAngle = Math.Clamp(_swingAngle + _swingVelocity * h, -35.0f, 35.0f);
        }
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        float normalizedAngle = ((_wheelAngle % 360.0f) + 360.0f) % 360.0f;
        yield return $"Roata (Var. C) : unghi = {normalizedAngle:F1}°   v_reală = {_angularSpeed:F2}°/s   v_țintă = {_targetSpeed:F2}°/s";
        yield return $"Cabine         : balans resort = {_swingAngle:+0.00;-0.00;0.00}°   " +
                     (_alignTween != null
                         ? $"Tween cabina {_selectedCabin + 1}: {_alignTween.Progress:P0}"
                         : _selectedCabin >= 0
                             ? $"cabina {_selectedCabin + 1} la peron"
                             : "rotație liberă");
    }
}