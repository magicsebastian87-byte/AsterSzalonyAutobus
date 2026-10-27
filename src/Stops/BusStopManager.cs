using Godot;
using System.Collections.Generic;

namespace AsterSzalonyAutobus.Stops;

[GlobalClass]
public partial class BusStopManager : Node
{
    [ExportGroup("Ustawienia przystanków")]
    [Export] public float BoardingTime = 3.0f;
    [Export] public float StopDetectionRadius = 4.0f;
    [Export] public float MaxStopSpeed = 0.5f;

    [ExportGroup("UI")]
    [Export] public Control StopUI;
    [Export] public Label StopNameLabel;
    [Export] public ProgressBar BoardingProgressBar;
    [Export] public Label PassengersCountLabel;
    [Export] public Label TimeBonusLabel;

    [ExportGroup("Dźwięki")]
    [Export] public AudioStream DoorsOpenSound;
    [Export] public AudioStream DoorsCloseSound;
    [Export] public AudioStream PassengerBoardSound;

    [ExportGroup("Grywalność")]
    [Export] public int BasePassengersPerStop = 8;
    [Export] public int MaxPassengersPerStop = 25;
    [Export] public float PerfectStopBonusSeconds = 10f;
    [Export] public float GoodStopBonusSeconds = 5f;
    [Export] public float CrashPenaltySeconds = 15f;

    private BusStop _currentStop;
    private float _boardingTimer = 0f;
    private bool _isBoarding = false;
    private bool _stopCompleted = false;
    private int _passengersBoarded = 0;
    private int _totalPassengers = 0;
    private int _passengersDelivered = 0;
    private int _stopsCompleted = 0;
    private float _totalTimeBonus = 0f;

    private List<BusStop> _allStops = new();
    private int _nextStopIndex = 0;

    private Bus _bus;
    private AudioStreamPlayer _sfxPlayer;

    public override void _Ready()
    {
        _bus = GetTree().GetFirstNodeInGroup("bus") as Bus;
        _sfxPlayer = new AudioStreamPlayer(); AddChild(_sfxPlayer);
        if (StopUI != null) StopUI.Visible = false;
        GD.Print("[BusStopManager] Gotowy");
    }

    public void RegisterStops(List<BusStop> stops)
    {
        _allStops = stops.OrderBy(s => s.StopOrder).ToList();
        _nextStopIndex = 0; _stopsCompleted = 0; _passengersDelivered = 0; _totalTimeBonus = 0f;
        GD.Print($"[BusStopManager] Zarejestrowano {_allStops.Count} przystanków");
    }

    public void OnBusEnteredStop(BusStop stop)
    {
        if (_isBoarding || _stopCompleted) return;
        if (_currentStop != null && _currentStop != stop) return;
        _currentStop = stop;
        if (_nextStopIndex < _allStops.Count && _allStops[_nextStopIndex] == stop)
            GD.Print($"[BusStopManager] Autobus na przystanku: {stop.StopName}");
    }

    public void OnBusExitedStop(BusStop stop)
    {
        if (_currentStop == stop) { CancelBoarding(); _currentStop = null; }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_isBoarding && _currentStop != null) UpdateBoarding(dt);
        CheckStopCondition();
    }

    private void CheckStopCondition()
    {
        if (_bus == null || _currentStop == null || _isBoarding || _stopCompleted) return;
        float speed = _bus.GetSpeedKmh() / 3.6f;
        Vector3 toStop = _currentStop.GlobalPosition - _bus.GlobalPosition;
        float distance = new Vector2(toStop.X, toStop.Z).Length();
        if (speed <= MaxStopSpeed && distance <= StopDetectionRadius) StartBoarding();
    }

    private void StartBoarding()
    {
        if (_currentStop == null) return;
        _isBoarding = true; _boardingTimer = 0f; _stopCompleted = false;
        _passengersBoarded = 0; _totalPassengers = (int)GD.RandRange(BasePassengersPerStop, MaxPassengersPerStop);
        PlaySound(DoorsOpenSound); ShowStopUI(true);
        GD.Print($"[BusStopManager] Rozpoczęto załadunek na {_currentStop.StopName}. Pasażerowie: {_totalPassengers}");
    }

    private void UpdateBoarding(float dt)
    {
        _boardingTimer += dt;
        float progress = Mathf.Clamp(_boardingTimer / BoardingTime, 0f, 1f);
        int targetBoarded = (int)(_totalPassengers * progress);
        if (targetBoarded > _passengersBoarded) { _passengersBoarded = targetBoarded; if (PassengerBoardSound != null && GD.Randi() % 3 == 0) PlaySound(PassengerBoardSound); }
        UpdateStopUI();
        if (_boardingTimer >= BoardingTime) CompleteBoarding();
    }

    private void CompleteBoarding()
    {
        _isBoarding = false; _stopCompleted = true;
        PlaySound(DoorsCloseSound);
        _passengersDelivered += _passengersBoarded;
        float stopQuality = CalculateStopQuality();
        float timeBonus = 0f;
        if (stopQuality >= 0.9f) { timeBonus = PerfectStopBonusSeconds; ShowTimeBonus($"+{PerfectStopBonusSeconds}s IDEALNE!", Colors.Gold); }
        else if (stopQuality >= 0.7f) { timeBonus = GoodStopBonusSeconds; ShowTimeBonus($"+{GoodStopBonusSeconds}s DOBRZE!", Colors.LightGreen); }
        _totalTimeBonus += timeBonus;
        EmitSignal(SignalName.StopCompleted, _currentStop.StopName, _passengersBoarded, timeBonus);
        GD.Print($"[BusStopManager] Zakończono załadunek na {_currentStop.StopName}. Wsadzono: {_passengersBoarded}/{_totalPassengers}, Bonus: +{timeBonus}s");
        _nextStopIndex++; _stopsCompleted++;
        GetTree().CreateTimer(2.0).Timeout += () => ShowStopUI(false);
        _currentStop = null;
    }

    private float CalculateStopQuality()
    {
        if (_bus == null || _currentStop == null) return 0f;
        Vector3 toStop = _currentStop.GlobalPosition - _bus.GlobalPosition;
        float distance = new Vector2(toStop.X, toStop.Z).Length();
        float centerScore = Mathf.Clamp(1f - distance / StopDetectionRadius, 0f, 1f);
        float angleDiff = Mathf.Abs(_bus.GlobalRotation.Y - _currentStop.GlobalRotation.Y);
        angleDiff = Mathf.FMod(angleDiff, Mathf.Tau);
        float angleScore = Mathf.Clamp(1f - angleDiff / (Mathf.Pi * 0.5f), 0f, 1f);
        return (centerScore * 0.6f + angleScore * 0.4f);
    }

    private void CancelBoarding() { if (!_isBoarding) return; _isBoarding = false; _boardingTimer = 0f; ShowStopUI(false); PlaySound(DoorsCloseSound); GD.Print("[BusStopManager] Załadunek anulowany - autobus odebrał"); }

    private void ShowStopUI(bool show) { if (StopUI != null) StopUI.Visible = show; }
    private void UpdateStopUI() { if (StopUI == null || !StopUI.Visible) return; if (StopNameLabel != null) StopNameLabel.Text = _currentStop?.StopName ?? "Przystanek"; if (BoardingProgressBar != null) BoardingProgressBar.Value = (_boardingTimer / BoardingTime) * 100f; if (PassengersCountLabel != null) PassengersCountLabel.Text = $"Pasażerowie: {_passengersBoarded}/{_totalPassengers}"; }
    private void ShowTimeBonus(string text, Color color) { if (TimeBonusLabel != null) { TimeBonusLabel.Text = text; TimeBonusLabel.AddThemeColorOverride("font_color", color); TimeBonusLabel.Visible = true; GetTree().CreateTimer(3.0).Timeout += () => TimeBonusLabel.Visible = false; } }
    private void PlaySound(AudioStream stream) { if (stream != null && _sfxPlayer != null) { _sfxPlayer.Stream = stream; _sfxPlayer.Play(); } }

    [Signal] public delegate void StopCompletedEventHandler(string stopName, int passengers, float timeBonus);
    [Signal] public delegate void AllStopsCompletedEventHandler(int totalPassengers, float totalTimeBonus);

    public int GetPassengersDelivered() => _passengersDelivered;
    public int GetStopsCompleted() => _stopsCompleted;
    public int GetTotalStops() => _allStops.Count;
    public float GetTotalTimeBonus() => _totalTimeBonus;
    public BusStop GetCurrentStop() => _currentStop;
    public BusStop GetNextStop() => _nextStopIndex < _allStops.Count ? _allStops[_nextStopIndex] : null;
}
