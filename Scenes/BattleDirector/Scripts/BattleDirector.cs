using System;
using FunkEngine;
using Godot;

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
    private Button _reinitButton;

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

    [Export]
    private SpinBox _bpmSelector;

    [Export]
    private SpinBox _loopsSelector;

    [Export]
    private Button _selectSongButton;

    [Export]
    private Label _selectSongLabel;

    public static string SongPath = String.Empty;
    public const string ChartDir = "Audio/Midi/";
    public static string SaveChartPath;
    public static string LoadChartPath;

    public static SongData Config = new SongData
    {
        Bpm = 120,
        SongLength = -1,
        NumLoops = 5,
    };
    #endregion

    #region Initialization
    private void ResetEverything()
    {
        if (string.IsNullOrEmpty(SongPath))
        {
            _selectSongLabel.Text = "Cannot start without a song selected!";
            return;
        }
        Config = new SongData
        {
            Bpm = (int)_bpmSelector.Value,
            SongLength = -1,
            NumLoops = (int)_loopsSelector.Value,
        };

        Audio.Stop();
        SongData curSong = Config;
        Audio.SetStream(GD.Load<AudioStream>(SongPath));
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

    private FileDialog _fileDialog;

    public override void _Ready()
    {
        _fileDialog = new FileDialog();
        _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.Access = FileDialog.AccessEnum.Filesystem;
        _fileDialog.UseNativeDialog = true;
        _fileDialog.Filters = ["*.ogg", "*.wav"];
        AddChild(_fileDialog);

        _fileDialog.FileSelected += (filePath) =>
        {
            SongPath = filePath;
            _selectSongLabel.Text = "Current Song: " + SongPath;
            _reinitButton.Disabled = !ValidateLoadChart();
        };

        CD.NoteInputEvent += OnTimedInput;

        _loadName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.LoadPath);
        _saveName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.SavePath);
        SaveChartPath = _saveName.Text;
        LoadChartPath = _loadName.Text;

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
        _selectSongButton.Pressed += () => _fileDialog.PopupCentered();
    }

    private void SaveChart()
    {
        CD.MM.CurrentChart.SaveChart(ChartDir + SaveChartPath);
    }

    public override void _Process(double delta)
    {
        _saveButton.Disabled = (CD.MM == null || CD.MM.CurrentChart == null);

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
            return;
        if (!data.IsNull)
            return;
        if ((int)data.Beat.BeatPos % (int)TimeKeeper.BeatsPerLoop == 0)
            return; //We never ever try to place at 0
        if (PlayerAddNote(data.Type, data.Beat))
            return; //Exit handling for a placed note
        return;
    }
    #endregion
}
