using System;
using FunkEngine;
using FunkEngine.Classes.BeatDetector;
using FunkEngine.Classes.MidiMaestro;
using Godot;

/**<summary>BattleDirector: Higher priority director to manage battle effects. Can directly access managers, which should signal up to Director WIP</summary>
 */
public partial class Composer : Node2D
{
    #region Declarations

    public static readonly string LoadPath = "res://Scenes/BattleDirector/BattleScene.tscn";

    [Export]
    public bool ForInternalUse;

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

    public static SpinBox BeatOffset;

    [Export]
    private SpinBox _bpmSelector;

    [Export]
    private SpinBox _loopsSelector;

    [Export]
    private Button _selectSongButton;

    [Export]
    private Label _selectSongLabel;

    [Export]
    private Button _forwardButton;

    [Export]
    private Button _rewindButton;

    [Export]
    private SpinBox _jumpSelector;

    [Export]
    private Button _snapButton;

    [Export]
    private Button _mapperButton;

    public static string SongPath = String.Empty;
    public const string ChartDir = "Edits/";
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

    private FileDialog _fileDialog;

    public override void _Ready()
    {
        _fileDialog = new FileDialog();
        _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.Access = FileDialog.AccessEnum.Filesystem;
        _fileDialog.UseNativeDialog = true;
        if (!ForInternalUse)
            _fileDialog.CurrentDir = "user://";
        _fileDialog.Filters = ["*.ogg"];
        AddChild(_fileDialog);

        _fileDialog.FileSelected += (filePath) =>
        {
            SongPath = filePath;
            _selectSongLabel.Text = "Current Song: " + SongPath.GetFile();
            _reinitButton.Disabled = !ValidateLoadChart();
        };

        CD.NoteInputEvent += OnTimedInput;

        _loadName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.LoadPath);
        _saveName.Text = (string)SaveSystem.GetConfigValue(SaveSystem.ConfigSettings.SavePath);
        SaveChartPath = _saveName.Text + ".tres";
        LoadChartPath = _loadName.Text;

        BeatOffset = _beatOffset;

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
        _forwardButton.Pressed += JumpForward;
        _rewindButton.Pressed += JumpBackwards;
        _snapButton.Pressed += SnapToBeat;
        _mapperButton.Pressed += AutoMap;
    }

    public override void _Process(double delta)
    {
        _saveButton.Disabled = (CD.MM?.CurrentChart == null);

        TimeKeeper.CurrentTime = Audio.GetPlaybackPosition();
        Beat realBeat = TimeKeeper.GetBeatFromTime(Audio.GetPlaybackPosition());
        UpdateBeat(realBeat);
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
        Audio.SetStream(AudioStreamOggVorbis.LoadFromFile(SongPath));
        if (curSong.SongLength <= 0)
        {
            curSong.SongLength = Audio.Stream.GetLength();
        }

        TimeKeeper.InitVals(curSong.Bpm);
        CD.Initialize(curSong);
        _startButton.Disabled = false;
        GD.Print(
            "Beats per loop: "
                + TimeKeeper.BeatsPerLoop
                + "Beats per song: "
                + TimeKeeper.BeatsPerSong
        );
    }

    private void JumpForward()
    {
        bool wasPaused = Audio.StreamPaused;

        Audio.SetStreamPaused(false);

        float pos = (float)(
            Audio.GetPlaybackPosition() + TimeKeeper.GetTimeOfBeat(new Beat(_jumpSelector.Value))
        );
        if (pos < Audio.Stream.GetLength())
        {
            Audio.Seek(pos);
        }
        Audio.SetStreamPaused(wasPaused);
    }

    private void JumpBackwards()
    {
        bool wasPaused = Audio.StreamPaused;

        Audio.SetStreamPaused(false);

        float pos = (float)(
            Audio.GetPlaybackPosition() - TimeKeeper.GetTimeOfBeat(new Beat(_jumpSelector.Value))
        );
        if (pos > 0)
        {
            Audio.Seek(pos);
        }
        Audio.SetStreamPaused(wasPaused);
    }

    private void SnapToBeat()
    {
        bool wasPaused = Audio.StreamPaused;

        Audio.SetStreamPaused(false);

        Beat snapBeat = new Beat(Math.Round(TimeKeeper.LastBeat.BeatPos));
        snapBeat.Loop = TimeKeeper.LastBeat.Loop;

        float pos = (float)TimeKeeper.GetTimeOfBeat(snapBeat);
        if (pos > 0 && pos < Audio.Stream.GetLength())
        {
            Audio.Seek(pos);
        }
        Audio.SetStreamPaused(wasPaused);
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

    public static string UserSaveFolder = "Exports";

    private void SaveChart()
    {
        if (ForInternalUse)
        {
            CD.MM.CurrentChart.SaveChart(
                ChartDir + SaveChartPath,
                SongPath.GetFile(),
                (int)_bpmSelector.Value,
                (int)_loopsSelector.Value
            );
            return;
        }

        if (!DirAccess.DirExistsAbsolute("user://" + UserSaveFolder))
            DirAccess.Open("user://").MakeDirRecursive(UserSaveFolder);
        CD.MM.CurrentChart.SaveChart(
            "user://" + UserSaveFolder + "/" + SaveChartPath,
            SongPath.GetFile(),
            (int)_bpmSelector.Value,
            (int)_loopsSelector.Value
        );

        FileAccess file = FileAccess.Open(
            "user://" + UserSaveFolder + "/" + _saveName.Text + ".sontem",
            FileAccess.ModeFlags.Write
        );

        string songAsJson = SongTemplate.ToJSONString(
            new SongTemplate(
                _saveName.Text,
                ["res://Scenes/Puppets/Enemies/Keythulu/Keythulu.tscn"]
            ),
            SaveChartPath
        );

        file.StoreLine(songAsJson);
        file.Close();
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

    public static string ChartBaseDir = ChartDir;

    /// <summary>
    /// Validate that the load path points to a valid chart resource.
    /// </summary>
    /// <returns>True if a resource could be loaded at the load path.</returns>
    private bool ValidateLoadChart()
    {
        LoadChartPath = _loadName.Text + ".tres";

        ChartBaseDir = "user://" + UserSaveFolder + "/";

        if (!FileAccess.FileExists("user://" + UserSaveFolder + "/" + LoadChartPath))
        {
            if (!FileAccess.FileExists(ChartDir + LoadChartPath))
                return false;
            ChartBaseDir = ChartDir;
        }

        try //Can't be parsed.
        {
            NoteChart chart = ResourceLoader.Load<NoteChart>(
                ChartBaseDir + LoadChartPath,
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
        PlayerAddNote(data.Type, data.Beat);
    }

    public override void _Input(InputEvent @event)
    {
        if (
            @event.IsActionPressed("WASD_inventory")
            || @event.IsActionPressed("CONTROLLER_inventory")
        )
        {
            if (!_reinitButton.Disabled)
                ResetEverything();
        }

        if (
            @event.IsActionPressed("WASD_secondaryPlacement")
            || @event.IsActionPressed("CONTROLLER_secondaryPlacement")
        )
        {
            if (!_startButton.Disabled)
                StartPlayback();
            else if (Audio.GetStreamPaused())
            {
                ResumePlayback();
            }
            else
            {
                Audio.SetStreamPaused(true);
            }
        }
    }

    #endregion

    #region  Auto Mapping

    private void AutoMap()
    {
        AudioFileAnalyzer analyzer = new AudioFileAnalyzer();
        analyzer.LoadAudioFromFile(SongPath);

        if (analyzer.PCMStream == null)
        {
            GD.PushError("Failed to load audio stream from file: " + SongPath);
            return;
        }

        try
        {
            analyzer.DetectOnsets();
            analyzer.NormalizeOnsets(0); // Normalization type 0 for now, can be changed later
            float[] onsets = analyzer.getOnsets();
            if (onsets.Length == 0)
            {
                GD.PushError("No onsets detected in the audio file.");
                return;
            }

            //log the detected onsets
            GD.Print("Detected Onsets: " + string.Join(", ", onsets));

            float timePerSample = analyzer.GetTimePerSample();

            Random random = new Random();
            int notesAdded = 0;

            for (int i = 0; i < onsets.Length; i++)
            {
                if (onsets[i] > .1) //TODO Sensitivity
                {
                    float time = i * timePerSample;

                    Beat noteBeat = TimeKeeper.GetBeatFromTime(time);

                    Array arrowTypes = Enum.GetValues(typeof(ArrowType));
                    ArrowType randomType = (ArrowType)
                        arrowTypes.GetValue(random.Next(arrowTypes.Length));

                    PlayerAddNote(randomType, noteBeat);
                    notesAdded++;
                }
            }

            GD.Print($"Auto-mapped {notesAdded} notes based on audio analysis");
        }
        catch (Exception e)
        {
            GD.PushError("Unable to load onsets for file: " + SongPath + "\n Error: " + e);
        }
    }

    #endregion
}
