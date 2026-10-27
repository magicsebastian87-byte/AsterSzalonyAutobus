using Godot;
using System;

namespace AsterSzalonyAutobus.Vehicles;

[Tool]
public partial class Bus : VehicleBody3D
{
    [ExportGroup("Silnik i napęd")]
    [Export] public float EngineForce = 85000f;
    [Export] public float MaxEngineRpm = 2200f;
    [Export] public float[] GearRatios = { 12.5f, 8.2f, 5.4f, 3.6f, 2.4f, 1.6f, 1.0f, 0.8f };
    [Export] public float FinalDriveRatio = 4.8f;
    [Export] public float NitroMultiplier = 1.35f;
    [Export] public float NitroDuration = 4.0f;
    [Export] public float NitroCooldown = 15.0f;

    [ExportGroup("Hamowanie i poślizg")]
    [Export] public float BrakeForce = 120000f;
    [Export] public float HandbrakeForce = 80000f;
    [Export] public float RearTractionMultiplier = 0.75f;
    [Export] public float DriftThreshold = 0.85f;

    [ExportGroup("Zawieszenie i geometria")]
    [Export] public float SteeringAngle = 38f;
    [Export] public float AckermannFactor = 0.3f;
    [Export] public float AntiRollBarForce = 15000f;

    [ExportGroup("Dźwięki i wizualizacja")]
    [Export] public AudioStreamPlayer3D EngineSound;
    [Export] public AudioStreamPlayer3D DriftSound;
    [Export] public ParticleSystem DriftParticlesFL;
    [Export] public ParticleSystem DriftParticlesFR;
    [Export] public ParticleSystem DriftParticlesRL;
    [Export] public ParticleSystem DriftParticlesRR;

    private float _currentGear = 1;
    private float _engineRpm = 800f;
    private float _nitroTimer = 0f;
    private float _nitroCooldownTimer = 0f;
    private bool _nitroActive = false;
    private float _currentEngineForce = 0f;
    private Vector3 _prevVelocity = Vector3.Zero;

    private VehicleWheel3D _wheelFL, _wheelFR, _wheelRL, _wheelRR;

    public override void _Ready()
    {
        _wheelFL = GetNode<VehicleWheel3D>("WheelFL");
        _wheelFR = GetNode<VehicleWheel3D>("WheelFR");
        _wheelRL = GetNode<VehicleWheel3D>("WheelRL");
        _wheelRR = GetNode<VehicleWheel3D>("WheelRR");

        ApplyRearTractionMultiplier();
        Mass = 15000f;
        CenterOfMass = new Vector3(0, 1.2f, 0.5f);
        GD.Print($"[Bus] Gotowy. Masa: {Mass}kg, Środek masy: {CenterOfMass}");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        HandleInput(dt);
        UpdateEngine(dt);
        UpdateNitro(dt);
        UpdateAntiRollBar(dt);
        UpdateDriftEffects(dt);
        UpdateAudio(dt);
        _prevVelocity = LinearVelocity;
    }

    private void HandleInput(float dt)
    {
        float accelerate = Input.GetActionStrength("accelerate");
        float brake = Input.GetActionStrength("brake");
        float steer = Input.GetActionStrength("steer_right") - Input.GetActionStrength("steer_left");
        bool handbrake = Input.IsActionPressed("handbrake");
        bool nitroPressed = Input.IsActionJustPressed("nitro");

        float targetSteering = steer * Mathf.DegToRad(SteeringAngle);
        _wheelFL.Steering = targetSteering;
        _wheelFR.Steering = targetSteering * (1.0f - AckermannFactor);

        if (accelerate > 0.01f) EngineForce = _currentEngineForce * accelerate;
        else { EngineForce = 0f; _wheelRL.EngineForce = -5000f; _wheelRR.EngineForce = -5000f; }

        float brakeForce = brake * BrakeForce;
        _wheelFL.Brake = brakeForce; _wheelFR.Brake = brakeForce;
        _wheelRL.Brake = brakeForce; _wheelRR.Brake = brakeForce;

        if (handbrake) { _wheelRL.Brake = HandbrakeForce; _wheelRR.Brake = HandbrakeForce; _wheelRL.FrictionSlip *= 0.3f; _wheelRR.FrictionSlip *= 0.3f; }
        else ApplyRearTractionMultiplier();

        if (nitroPressed && _nitroCooldownTimer <= 0f && !_nitroActive) ActivateNitro();
    }

    private void ApplyRearTractionMultiplier() { _wheelRL.FrictionSlip = 10.5f * RearTractionMultiplier; _wheelRR.FrictionSlip = 10.5f * RearTractionMultiplier; }

    private void UpdateEngine(float dt)
    {
        float wheelRpm = Mathf.Abs(_wheelRL.GetRpm());
        _engineRpm = Mathf.Lerp(_engineRpm, 800f + wheelRpm * 0.8f, dt * 5f);
        _engineRpm = Mathf.Clamp(_engineRpm, 800f, MaxEngineRpm);

        float speedKmh = LinearVelocity.Length() * 3.6f;
        int targetGear = Mathf.Clamp((int)(speedKmh / 15f) + 1, 1, GearRatios.Length);
        _currentGear = Mathf.Lerp(_currentGear, targetGear, dt * 3f);

        float gearRatio = GearRatios[Mathf.Clamp((int)_currentGear - 1, 0, GearRatios.Length - 1)] * FinalDriveRatio;
        _currentEngineForce = EngineForce * gearRatio * 0.85f;

        _wheelRL.EngineForce = _currentEngineForce * 0.5f;
        _wheelRR.EngineForce = _currentEngineForce * 0.5f;
    }

    private void ActivateNitro()
    {
        _nitroActive = true; _nitroTimer = NitroDuration; _nitroCooldownTimer = NitroCooldown;
        _currentEngineForce *= NitroMultiplier;
        if (DriftParticlesFL != null) DriftParticlesFL.Emitting = true;
        if (DriftParticlesFR != null) DriftParticlesFR.Emitting = true;
        if (DriftParticlesRL != null) DriftParticlesRL.Emitting = true;
        if (DriftParticlesRR != null) DriftParticlesRR.Emitting = true;
        GD.Print("[Bus] NITRO AKTYWNE!");
    }

    private void UpdateNitro(float dt)
    {
        if (_nitroActive) { _nitroTimer -= dt; if (_nitroTimer <= 0f) { _nitroActive = false; _currentEngineForce /= NitroMultiplier; if (DriftParticlesFL != null) DriftParticlesFL.Emitting = false; if (DriftParticlesFR != null) DriftParticlesFR.Emitting = false; if (DriftParticlesRL != null) DriftParticlesRL.Emitting = false; if (DriftParticlesRR != null) DriftParticlesRR.Emitting = false; GD.Print("[Bus] Nitro wygasło"); } }
        if (_nitroCooldownTimer > 0f) _nitroCooldownTimer -= dt;
    }

    private void UpdateAntiRollBar(float dt) { /* placeholder */ }

    private void UpdateDriftEffects(float dt)
    {
        float rearSlip = (_wheelRL.SkidInfo.Length() + _wheelRR.SkidInfo.Length()) * 0.5f;
        bool isDrifting = rearSlip > DriftThreshold && LinearVelocity.Length() > 5f;
        if (isDrifting && DriftSound != null && !DriftSound.Playing) DriftSound.Play();
        else if (!isDrifting && DriftSound != null && DriftSound.Playing) DriftSound.Stop();
        bool emitParticles = isDrifting && _nitroActive;
        if (DriftParticlesRL != null) DriftParticlesRL.Emitting = emitParticles;
        if (DriftParticlesRR != null) DriftParticlesRR.Emitting = emitParticles;
    }

    private void UpdateAudio(float dt)
    {
        if (EngineSound == null) return;
        float rpmNormalized = (_engineRpm - 800f) / (MaxEngineRpm - 800f);
        EngineSound.PitchScale = 0.7f + rpmNormalized * 1.3f;
        float load = Mathf.Abs(Input.GetActionStrength("accelerate"));
        EngineSound.VolumeDb = Mathf.Lerp(-20f, 0f, load);
    }

    public float GetSpeedKmh() => LinearVelocity.Length() * 3.6f;
    public float GetEngineRpm() => _engineRpm;
    public int GetCurrentGear() => Mathf.Clamp((int)_currentGear, 1, GearRatios.Length);
    public bool IsNitroActive() => _nitroActive;
    public float GetNitroCooldownRemaining() => _nitroCooldownTimer;
    public bool IsDrifting() { float rearSlip = (_wheelRL.SkidInfo.Length() + _wheelRR.SkidInfo.Length()) * 0.5f; return rearSlip > DriftThreshold && LinearVelocity.Length() > 5f; }
    public void ResetPhysics() { LinearVelocity = Vector3.Zero; AngularVelocity = Vector3.Zero; _engineRpm = 800f; _currentGear = 1; _nitroActive = false; _nitroTimer = 0f; _nitroCooldownTimer = 0f; ApplyRearTractionMultiplier(); }
}
