using System;
using System.Linq;
using FunkEngine;
using FunkEngine.Classes.MidiMaestro;
using Godot;
using Melanchall.DryWetMidi.Interaction;

/**<summary>BattleDirector: Higher priority director to manage battle effects. Can directly access managers, which should signal up to Director WIP</summary>
 */
public partial class BattleDirector : Node2D
{
    #region Declarations

    public static readonly string LoadPath = "res://Scenes/BattleDirector/BattleScene.tscn";

    [Export]
    private Conductor CD;

    [Export]
    private ChartManager CM;

    [Export]
    private AudioStreamPlayer Audio;

    [Export]
    private Button _reinitButton; //Initial start button

    [Export]
    private Button _startButton;

    [Export]
    private Button _pauseButton;

    [Export]
    private Label _beatLabel;

    [Export]
    private Button _resumeButton;

    [Export]
    private Button _saveButton;

    [Export]
    private TextEdit _loadName;

    [Export]
    private TextEdit _saveName;

    [Export]
    private SpinBox _holdLength;

    [Export]
    private SpinBox _beatOffset;

    public const string ChartDir = "Audio/Midi/";
    public static string SaveChartPath;
    public static string LoadChartPath;

    public static SongTemplate Config = new SongTemplate(
        new SongData
        {
            Bpm = 120,
            SongLength = -1,
            NumLoops = 5,
        }
    );
    #endregion

    #region Initialization
    private void ResetEverything()
    {
        Audio.Stop();
        SongData curSong = Config.SongData;
        Audio.SetStream(GD.Load<AudioStream>("res://Audio/Song1.ogg"));
        if (curSong.SongLength <= 0)
        {
            curSong.SongLength = Audio.Stream.GetLength();
        }

        TimeKeeper.InitVals(curSong.Bpm);
        CD.Initialize(curSong);
        _startButton.Disabled = false;
    }

    private void StartPlayback()
    {
        if (Audio.IsPlaying())
            return;

        var timer = GetTree().CreateTimer(AudioServer.GetTimeToNextMix());
        timer.Timeout += () =>
        {
            _startButton.Disabled = true;
            CM.BeginTweens();
            Audio.Play();
        };
    }

    private void ResumePlayback()
    {
        if (!Audio.GetStreamPaused())
            return;

        var timer = GetTree().CreateTimer(AudioServer.GetTimeToNextMix());
        timer.Timeout += () =>
        {
            CM.BeginTweens();
            Audio.SetStreamPaused(false);
        };
    }

    public override void _Ready()
    {
        CD.NoteInputEvent += OnTimedInput;

        _loadName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.LoadPath);
        _saveName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.SavePath);

        _saveName.TextChanged += SaveTextChanged;
        _loadName.TextChanged += LoadTextChanged;

        _reinitButton.Disabled = !ValidateLoadChart();

        _reinitButton.GrabFocus();
        _pauseButton.Pressed += () => Audio.StreamPaused = true;
        _reinitButton.Pressed += ResetEverything;
        _startButton.Disabled = true;
        _startButton.Pressed += StartPlayback;
        _resumeButton.Pressed += ResumePlayback;
        _saveButton.Pressed += SaveChart;
    }

    private void SaveChart()
    {
        CD.MM.CurrentChart.SaveChart(ChartDir + SaveChartPath);
    }

    public override void _Process(double delta)
    {
        _saveButton.Disabled = CD.MM == null || CD.MM.CurrentChart == null;

        TimeKeeper.CurrentTime = Audio.GetPlaybackPosition();
        Beat realBeat = TimeKeeper.GetBeatFromTime(Audio.GetPlaybackPosition());
        UpdateBeat(realBeat);
    }

    private void SaveTextChanged()
    {
        SaveChartPath = _saveName.Text + ".tres";
        SaveSystem.UpdateConfig(SaveSystem.ConfigSettings.SavePath, _saveName.Text);
    }

    private void LoadTextChanged()
    {
        _reinitButton.Disabled = !ValidateLoadChart();
    }

    private bool ValidateLoadChart()
    {
        LoadChartPath = _loadName.Text + ".tres";

        if (!FileAccess.FileExists(ChartDir + LoadChartPath))
            return false;
        try //Can't be parsed.
        {
            NoteChart chart = ResourceLoader.Load<NoteChart>(
                ChartDir + LoadChartPath,
                null,
                ResourceLoader.CacheMode.Ignore
            );
            chart.AddNote(ArrowType.Up, 0);
        }
        catch (Exception e)
        {
            GD.PushError(e);
            Console.WriteLine(e);
            return false;
        }

        SaveSystem.UpdateConfig(SaveSystem.ConfigSettings.LoadPath, _loadName.Text);
        return true;
    }

    private void UpdateBeat(Beat beat)
    {
        //Still iffy, but approximately once per beat check, happens at start of new beat
        if (Math.Floor(beat.BeatPos) >= Math.Floor((TimeKeeper.LastBeat + 1).BeatPos))
        {
            CD.ProgressiveSpawnNotes(beat);
        }
        _beatLabel.Text = beat.ToString();
        TimeKeeper.LastBeat = beat;
    }
    #endregion

    #region Input&Timing
    private bool PlayerAddNote(ArrowType type, Beat beat)
    {
        CD.AddPlayerNote(type, beat + _beatOffset.Value, _holdLength.Value);
        return true;
    }

    //Only called from CD signal when a note is processed
    private void OnTimedInput(ArrowData data)
    {
        if (data == ArrowData.Placeholder)
            return; //An inactive note was passed, for now do nothing, could force miss.
        if (data.IsNull) //An empty beat
        {
            if ((int)data.Beat.BeatPos % (int)TimeKeeper.BeatsPerLoop == 0)
                return; //We never ever try to place at 0
            if (PlayerAddNote(data.Type, data.Beat))
                return; //Exit handling for a placed note
            return;
        }
    }
    #endregion
}
