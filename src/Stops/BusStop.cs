using Godot;

namespace AsterSzalonyAutobus.Stops;

[GlobalClass]
public partial class BusStop : Area3D
{
    [Export] public string StopName = "Przystanek";
    [Export] public int StopOrder = 0;
    [Export] public bool IsStartStop = false;
    [Export] public bool IsEndStop = false;

    [ExportGroup("Wizualizacja")]
    [Export] public MeshInstance3D VisualMarker;
    [Export] public Label3D StopNameLabel;
    [Export] public Marker3D ParkingMarker;

    private BusStopManager _manager;
    private bool _busInside = false;

    public override void _Ready()
    {
        _manager = GetNode<BusStopManager>("/root/BusStopManager");
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        if (StopNameLabel != null) StopNameLabel.Text = StopName;
        AddToGroup("bus_stops");
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Bus bus && bus.IsInGroup("bus"))
        {
            _busInside = true;
            _manager?.OnBusEnteredStop(this);
            if (VisualMarker != null)
            {
                if (VisualMarker.MaterialOverride is StandardMaterial3D mat) mat.AlbedoColor = Colors.Lime;
                else VisualMarker.MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.Lime };
            }
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Bus bus && bus.IsInGroup("bus"))
        {
            _busInside = false;
            _manager?.OnBusExitedStop(this);
            if (VisualMarker != null)
            {
                if (VisualMarker.MaterialOverride is StandardMaterial3D mat) mat.AlbedoColor = new Color(0.2f, 0.6f, 1.0f);
            }
        }
    }

    public Vector3 GetParkingPosition() => ParkingMarker != null ? ParkingMarker.GlobalPosition : GlobalPosition;
    public Vector3 GetParkingDirection() => ParkingMarker != null ? -ParkingMarker.GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;
}
