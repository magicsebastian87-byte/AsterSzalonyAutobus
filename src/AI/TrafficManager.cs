using Godot;
using System.Collections.Generic;

namespace AsterSzalonyAutobus.AI;

[GlobalClass]
public partial class TrafficManager : Node
{
    [ExportGroup("Prefab i spawn")]
    [Export] public PackedScene TrafficCarScene;
    [Export] public List<Path3D> TrafficPaths = new();
    [Export] public int MaxCars = 12;
    [Export] public float SpawnInterval = 8f;
    [Export] public float DespawnDistance = 200f;

    [ExportGroup("Zachowanie")]
    [Export] public float BaseSpeed = 14f;
    [Export] public float SpeedVariation = 3f;
    [Export] public float AvoidanceDistance = 15f;
    [Export] public float AvoidanceForce = 8f;

    private List<TrafficCar> _activeCars = new();
    private float _spawnTimer = 0f;
    private Bus _bus;
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _bus = GetTree().GetFirstNodeInGroup("bus") as Bus;
        _rng.Randomize();
        if (TrafficCarScene == null) { GD.Print("[TrafficManager] Brak prefabu - tworzę domyślny"); CreateDefaultCarScene(); }
    }

    private void CreateDefaultCarScene()
    {
        var carRoot = new VehicleBody3D(); carRoot.Name = "TrafficCar";
        carRoot.CollisionLayer = 4; carRoot.CollisionMask = 1 | 2; carRoot.Mass = _rng.RandiRange(1200, 1800);
        var mesh = new MeshInstance3D(); mesh.Mesh = new BoxMesh { Size = new Vector3(1.8f, 1.4f, 4.2f) };
        mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(_rng.RandfRange(0.2f, 0.8f), _rng.RandfRange(0.2f, 0.8f), _rng.RandfRange(0.2f, 0.8f)) };
        carRoot.AddChild(mesh);

        var wheelPositions = new[] { new Vector3(-0.9f, 0.35f, 1.8f), new Vector3(0.9f, 0.35f, 1.8f), new Vector3(-0.9f, 0.35f, -1.8f), new Vector3(0.9f, 0.35f, -1.8f) };
        foreach (var pos in wheelPositions)
        {
            var wheel = new VehicleWheel3D(); wheel.WheelJoint = new Transform3D(Basis.Identity, pos);
            wheel.SuspensionLength = 0.1f; wheel.SuspensionStiffness = 25f; wheel.SuspensionDamping = 2f;
            wheel.SuspensionTravel = 0.1f; wheel.FrictionSlip = 10f; wheel.Radius = 0.3f; wheel.MaxSuspensionForce = 4000f;
            carRoot.AddChild(wheel);
        }

        var script = new TrafficCar(); carRoot.SetScript(script);
        TrafficCarScene = new PackedScene(); TrafficCarScene.Pack(carRoot);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_bus == null) { _bus = GetTree().GetFirstNodeInGroup("bus") as Bus; return; }
        _spawnTimer += dt; TrySpawnCar(dt);

        for (int i = _activeCars.Count - 1; i >= 0; i--)
        {
            var car = _activeCars[i];
            if (car == null || !IsInstanceValid(car)) { _activeCars.RemoveAt(i); continue; }
            car.UpdateAI(dt, _bus);
            if (car.GlobalPosition.DistanceTo(_bus.GlobalPosition) > DespawnDistance) { car.QueueFree(); _activeCars.RemoveAt(i); }
        }
    }

    private void TrySpawnCar(float dt)
    {
        if (_activeCars.Count >= MaxCars) return;
        if (TrafficPaths.Count == 0) return;
        if (_spawnTimer < SpawnInterval) return;

        var path = TrafficPaths[_rng.RandiRange(0, TrafficPaths.Count - 1)];
        if (path.Curve.GetPointCount() < 2) return;

        Vector3 spawnPos = path.GlobalPosition + path.Curve.GetPointPosition(0);
        if (spawnPos.DistanceTo(_bus.GlobalPosition) < 50f) return;

        SpawnCar(spawnPos, path); _spawnTimer = 0f;
    }

    private void SpawnCar(Vector3 position, Path3D path)
    {
        if (TrafficCarScene == null) return;
        var car = TrafficCarScene.Instantiate<TrafficCar>();
        car.GlobalPosition = position;
        car.Initialize(path, BaseSpeed + _rng.RandfRange(-SpeedVariation, SpeedVariation));
        AddChild(car); _activeCars.Add(car);
    }

    public int GetActiveCarCount() => _activeCars.Count;
    public void ClearAllTraffic() { foreach (var car in _activeCars) car?.QueueFree(); _activeCars.Clear(); }
}

[GlobalClass]
public partial class TrafficCar : VehicleBody3D
{
    private Path3D _path; private Curve3D _curve; private float _targetSpeed; private int _currentPoint = 0; private Bus _playerBus;

    public void Initialize(Path3D path, float speed) { _path = path; _curve = path.Curve; _targetSpeed = speed; Mass = 1500f; CenterOfMass = new Vector3(0, 0.5f, 0); }

    public void UpdateAI(float dt, Bus playerBus)
    {
        _playerBus = playerBus; if (_curve == null || _curve.GetPointCount() < 2) return;
        Vector3 targetPos = _path.GlobalPosition + _curve.GetPointPosition(_currentPoint);
        Vector3 toTarget = (targetPos - GlobalPosition).Normalized(); toTarget.Y = 0;

        if (_playerBus != null)
        {
            Vector3 toPlayer = (_playerBus.GlobalPosition - GlobalPosition);
            float distToPlayer = new Vector2(toPlayer.X, toPlayer.Z).Length();
            if (distToPlayer < 15f && distToPlayer > 0.1f)
            {
                Vector3 avoidDir = -toTarget.Normalized();
                toTarget = (toTarget * 0.7f + avoidDir * 0.3f).Normalized();
            }
        }

        float targetAngle = Mathf.Atan2(toTarget.X, toTarget.Z);
        float currentAngle = GlobalRotation.Y;
        float angleDiff = Mathf.Wrap(targetAngle - currentAngle, -Mathf.Pi, Mathf.Pi);
        float steer = Mathf.Clamp(angleDiff * 2.5f, -0.6f, 0.6f);
        GetNode<VehicleWheel3D>("WheelFL").Steering = steer;
        GetNode<VehicleWheel3D>("WheelFR").Steering = steer;

        float currentSpeed = LinearVelocity.Length();
        float speedDiff = _targetSpeed - currentSpeed;
        if (speedDiff > 0.5f) { GetNode<VehicleWheel3D>("WheelRL").EngineForce = 4000f; GetNode<VehicleWheel3D>("WheelRR").EngineForce = 4000f; GetNode<VehicleWheel3D>("WheelRL").Brake = 0; GetNode<VehicleWheel3D>("WheelRR").Brake = 0; }
        else if (speedDiff < -1f) { GetNode<VehicleWheel3D>("WheelRL").Brake = 3000f; GetNode<VehicleWheel3D>("WheelRR").Brake = 3000f; }
        else { GetNode<VehicleWheel3D>("WheelRL").EngineForce = 1000f; GetNode<VehicleWheel3D>("WheelRR").EngineForce = 1000f; }

        if (GlobalPosition.DistanceTo(targetPos) < 5f) { _currentPoint++; if (_currentPoint >= _curve.GetPointCount()) QueueFree(); }
    }
}
