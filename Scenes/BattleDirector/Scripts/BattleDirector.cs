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
    private Button _playButton;

    private double _timingInterval = .1; //in beats, maybe make note/bpm dependent

    public static SongTemplate Song = new SongTemplate(
        new SongData
        {
            Bpm = 120,
            SongLength = -1,
            NumLoops = 5,
        },
        "Song1",
        "Audio/Song1.ogg",
        "Audio/Midi/Song1.tres"
    );

    public static BattleConfig Config;

    public static BattleConfig MakeBattleConfig()
    {
        BattleConfig result = new BattleConfig { RoomType = Stages.Battle };
        result.EnemyScenePath = Song.EnemyScenePath;
        result.CurSong = Song;
        return result;
    }

    #endregion

    #region Initialization
    private void ResetEverything()
    {
        Audio.Stop();
        Config = MakeBattleConfig();
        SongData curSong = Config.CurSong.SongData;
        Audio.SetStream(GD.Load<AudioStream>(Config.CurSong.AudioLocation));
        if (curSong.SongLength <= 0)
        {
            curSong.SongLength = Audio.Stream.GetLength();
        }

        TimeKeeper.InitVals(curSong.Bpm);
        CD.Initialize(curSong);
    }

    private void BeginPlayback()
    {
        if (Audio.IsPlaying())
            return;

        var timer = GetTree().CreateTimer(AudioServer.GetTimeToNextMix());
        timer.Timeout += () =>
        {
            CM.BeginTweens();
            Audio.Play();
        };
    }

    public override void _Ready()
    {
        CD.NoteInputEvent += OnTimedInput;

        ResetEverything();

        _reinitButton.GrabFocus();
        _reinitButton.Pressed += ResetEverything;
        _playButton.Pressed += BeginPlayback;
    }

    public override void _Process(double delta)
    {
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
        TimeKeeper.LastBeat = beat;
    }
    #endregion

    #region Input&Timing
    private bool PlayerAddNote(ArrowType type, Beat beat)
    {
        CD.AddPlayerNote(type, beat);
        return true;
    }

    //Only called from CD signal when a note is processed
    private void OnTimedInput(ArrowData data, double beatDif)
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
