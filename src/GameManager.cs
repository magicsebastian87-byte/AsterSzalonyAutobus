using Godot;
using System;
using System.Collections.Generic;
using AsterSzalonyAutobus.Vehicles;
using AsterSzalonyAutobus.Stops;
using AsterSzalonyAutobus.Route;
using AsterSzalonyAutobus.AI;

namespace AsterSzalonyAutobus;

[GlobalClass]
public partial class GameManager : Node
{
    [ExportGroup("Ustawienia gry")]
    [Export] public float RouteTimeLimit = 300f;
    [Export] public int TargetPassengers = 100;
    [Export] public float TimeBonusPerPassenger = 2f;

    [ExportGroup("UI")]
    [Export] public Label TimeLabel;
    [Export] public Label PassengersLabel;
    [Export] public Label ScoreLabel;
    [Export] public ProgressBar TimeProgressBar;
    [Export] public Control GameOverPanel;
    [Export] public Label GameOverText;
    [Export] public Button RestartButton;
    [Export] public Button QuitButton;

    [ExportGroup("Referencje")]
    [Export] public Bus Bus;
    [Export] public BusStopManager BusStopManager;
    [Export] public GPSNavigator GPSNavigator;
    [Export] public TrafficManager TrafficManager;

    private float _remainingTime; private int _score = 0;
    private bool _gameActive = false; private bool _gameEnded = false;
    private int _totalPassengersDelivered = 0; private float _startTime;

    public override void _Ready()
    {
        Bus ??= GetTree().GetFirstNodeInGroup("bus") as Bus;
        BusStopManager ??= GetNode<BusStopManager>("/root/BusStopManager");
        GPSNavigator ??= GetTree().GetFirstNodeInGroup("gps") as GPSNavigator;
        TrafficManager ??= GetTree().GetFirstNodeInGroup("traffic") as TrafficManager;

        if (BusStopManager != null) { BusStopManager.StopCompleted += OnStopCompleted; BusStopManager.AllStopsCompleted += OnAllStopsCompleted; }
        if (RestartButton != null) RestartButton.Pressed += OnRestartPressed;
        if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;
        if (GameOverPanel != null) GameOverPanel.Visible = false;

        StartNewGame();
    }

    private void StartNewGame()
    {
        _remainingTime = RouteTimeLimit; _score = 0; _totalPassengersDelivered = 0;
        _gameActive = true; _gameEnded = false; _startTime = Time.GetTicksMsec() / 1000f;
        Bus?.ResetPhysics();
        BusStopManager?.RegisterStops(GetAllBusStops());
        TrafficManager?.ClearAllTraffic();
        SetupRoute();
        UpdateUI();
        GD.Print("[GameManager] Nowa gra rozpoczęta!");
    }

    private void SetupRoute()
    {
        var waypoints = new Godot.Collections.Array<Vector3>
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, -500), new Vector3(100, 0, -1000),
            new Vector3(150, 0, -2000), new Vector3(200, 0, -3000)
        };
        var stopsList = GetAllBusStops();
        var stops = new Godot.Collections.Array<BusStop>();
        foreach (var s in stopsList) stops.Add(s);
        GPSNavigator?.SetRoute(waypoints, stops);
    }

    private List<BusStop> GetAllBusStops()
    {
        var stops = new List<BusStop>();
        foreach (var node in GetTree().GetNodesInGroup("bus_stops")) if (node is BusStop stop) stops.Add(stop);
        return stops;
    }

    public override void _Process(double delta)
    {
        if (!_gameActive || _gameEnded) return;
        float dt = (float)delta; _remainingTime -= dt;

        if (Bus != null)
        {
            float speed = Bus.GetSpeedKmh();
            if (speed > 60f) _remainingTime += dt * 0.1f;
            if (Bus.IsDrifting()) { _remainingTime += dt * 0.05f; AddScore(1); }
        }

        if (_remainingTime <= 0f) { EndGame(false, "Czas minął!"); return; }
        UpdateUI();
    }

    private void OnStopCompleted(string stopName, int passengers, float timeBonus)
    {
        if (!_gameActive) return;
        _totalPassengersDelivered += passengers;
        _remainingTime += timeBonus;
        _score += passengers * 10 + (int)(timeBonus * 5);
        _remainingTime += passengers * TimeBonusPerPassenger;
        GD.Print($"[GameManager] {stopName}: +{passengers} pasażerów, +{timeBonus:F1}s, Score: {_score}");
    }

    private void OnAllStopsCompleted(int totalPassengers, float totalTimeBonus)
    {
        if (!_gameActive) return;
        _score += 5000; _score += (int)(_remainingTime * 10);
        EndGame(true, $"Trasa ukończona!\nPasażerowie: {totalPassengers}\nWynik: {_score}");
    }

    private void AddScore(int points) => _score += points;

    private void UpdateUI()
    {
        if (TimeLabel != null) { int m = (int)(_remainingTime / 60); int s = (int)(_remainingTime % 60); TimeLabel.Text = $"{m:D2}:{s:D2}"; }
        if (TimeProgressBar != null) TimeProgressBar.Value = (_remainingTime / RouteTimeLimit) * 100f;
        if (PassengersLabel != null) PassengersLabel.Text = $"Pasażerowie: {_totalPassengersDelivered}/{TargetPassengers}";
        if (ScoreLabel != null) ScoreLabel.Text = $"Wynik: {_score}";
    }

    private void EndGame(bool victory, string message)
    {
        _gameActive = false; _gameEnded = true;
        if (GameOverPanel != null) { GameOverPanel.Visible = true; if (GameOverText != null) { GameOverText.Text = victory ? $"WYGRANA!\n{message}" : $"PORAŻKA\n{message}"; GameOverText.AddThemeColorOverride("font_color", victory ? Colors.Gold : Colors.Red); } }
        GD.Print($"[GameManager] Gra zakończona: {(victory ? "WYGRANA" : "PORAŻKA")} - {message}");
    }

    private void OnRestartPressed() { if (GameOverPanel != null) GameOverPanel.Visible = false; StartNewGame(); }
    private void OnQuitPressed() => GetTree().Quit();

    public float GetRemainingTime() => _remainingTime;
    public int GetScore() => _score;
    public int GetPassengersDelivered() => _totalPassengersDelivered;
    public bool IsGameActive() => _gameActive;
}
