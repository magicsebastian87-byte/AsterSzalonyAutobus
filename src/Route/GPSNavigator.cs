using Godot;
using System.Collections.Generic;

namespace AsterSzalonyAutobus.Route;

[GlobalClass]
public partial class GPSNavigator : Node3D
{
    [ExportGroup("Referencje")]
    [Export] public Node3D ArrowMesh;
    [Export] public Camera3D MainCamera;

    [ExportGroup("Ustawienia")]
    [Export] public float ArrowHeight = 8f;
    [Export] public float ArrowDistance = 15f;
    [Export] public float MinArrowDistance = 5f;
    [Export] public float SmoothingSpeed = 5f;

    [ExportGroup("Trasa")]
    [Export] public List<Vector3> RouteWaypoints = new();
    [Export] public List<BusStop> RouteStops = new();

    private int _currentWaypointIndex = 0;
    private int _currentStopIndex = 0;
    private Vector3 _targetPosition = Vector3.Zero;
    private Bus _bus;
    private bool _routeActive = false;

    public override void _Ready()
    {
        _bus = GetTree().GetFirstNodeInGroup("bus") as Bus;
        if (ArrowMesh != null) ArrowMesh.Visible = false;
        if (MainCamera == null) MainCamera = GetViewport().GetCamera3D();
    }

    public void SetRoute(List<Vector3> waypoints, List<BusStop> stops = null)
    {
        RouteWaypoints = waypoints ?? new List<Vector3>();
        RouteStops = stops ?? new List<BusStop>();
        _currentWaypointIndex = 0; _currentStopIndex = 0; _routeActive = RouteWaypoints.Count > 0;
        UpdateTarget();
        GD.Print($"[GPSNavigator] Trasa: {RouteWaypoints.Count} waypointów, {RouteStops.Count} przystanków");
    }

    public void SetNextStopTarget(BusStop stop)
    {
        if (stop == null) return;
        _targetPosition = stop.GetParkingPosition();
        _currentStopIndex = RouteStops.IndexOf(stop); if (_currentStopIndex < 0) _currentStopIndex = 0;
        if (ArrowMesh != null) ArrowMesh.Visible = true;
        GD.Print($"[GPSNavigator] Cel: {stop.StopName} ({_targetPosition})");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!_routeActive || _bus == null || ArrowMesh == null) return;
        UpdateArrowPosition(dt); UpdateArrowRotation(dt); CheckWaypointReached();
    }

    private void UpdateArrowPosition(float dt)
    {
        if (_bus == null) return;
        Vector3 busPos = _bus.GlobalPosition;
        Vector3 busForward = -_bus.GlobalTransform.Basis.Z;
        Vector3 targetPos = busPos + busForward * ArrowDistance + Vector3.Up * ArrowHeight;
        ArrowMesh.GlobalPosition = ArrowMesh.GlobalPosition.Lerp(targetPos, dt * SmoothingSpeed);
    }

    private void UpdateArrowRotation(float dt)
    {
        Vector3 toTarget = (_targetPosition - ArrowMesh.GlobalPosition).Normalized(); toTarget.Y = 0;
        if (toTarget.LengthSquared() < 0.01f) return;
        Quaternion targetRot = Quaternion.FromEuler(new Vector3(0, Mathf.Atan2(toTarget.X, toTarget.Z), 0));
        ArrowMesh.GlobalRotation = ArrowMesh.GlobalRotation.Lerp(targetRot.GetEuler(), dt * SmoothingSpeed);
    }

    private void CheckWaypointReached()
    {
        if (_bus == null) return;
        float distToTarget = _bus.GlobalPosition.DistanceTo(_targetPosition);
        if (distToTarget < MinArrowDistance) { if (ArrowMesh != null) ArrowMesh.Visible = false; }
        else if (ArrowMesh != null && !ArrowMesh.Visible) ArrowMesh.Visible = true;

        if (_currentWaypointIndex < RouteWaypoints.Count)
        {
            float distToWaypoint = _bus.GlobalPosition.DistanceTo(RouteWaypoints[_currentWaypointIndex]);
            if (distToWaypoint < 10f) { _currentWaypointIndex++; UpdateTarget(); }
        }
    }

    private void UpdateTarget()
    {
        if (_currentWaypointIndex < RouteWaypoints.Count) _targetPosition = RouteWaypoints[_currentWaypointIndex];
        else if (_currentStopIndex < RouteStops.Count) _targetPosition = RouteStops[_currentStopIndex].GetParkingPosition();
        else { _routeActive = false; if (ArrowMesh != null) ArrowMesh.Visible = false; GD.Print("[GPSNavigator] Trasa ukończona!"); }
    }

    public Vector3 GetTargetPosition() => _targetPosition;
    public float GetDistanceToTarget() => _bus != null ? _bus.GlobalPosition.DistanceTo(_targetPosition) : 0f;
    public int GetCurrentWaypointIndex() => _currentWaypointIndex;
    public int GetTotalWaypoints() => RouteWaypoints.Count;
    public bool IsRouteActive() => _routeActive;
}
