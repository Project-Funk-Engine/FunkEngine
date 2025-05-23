using System;
using System.Collections.Generic;
using FunkEngine;
using Godot;

/**<summary>Conductor: Arm of BattleDirector for handling note lanes and timing.</summary>
 */
public partial class Conductor : Node
{
    [Export]
    private ChartManager CM;

    public MidiMaestro MM;

    private List<ArrowData> _noteData = new List<ArrowData>();

    private double _beatSpawnOffset;

    #region Initialization

    public override void _Ready()
    {
        CM.ArrowFromInput += ReceiveNoteInput;
        CM.ArrowSelected += RemoveArrow;
    }

    public void Initialize(SongData curSong)
    {
        _noteData = new List<ArrowData>();

        MM = new MidiMaestro(Composer.ChartBaseDir + Composer.LoadChartPath);

        CM.Initialize(curSong, MM.CurrentChart.SongSpeed);

        //Approximately the first note offscreen
        _beatSpawnOffset = Math.Ceiling(
            CM.Size.X / TimeKeeper.ChartWidth * TimeKeeper.BeatsPerLoop
        );
        AddInitialNotes();
    }

    private void AddInitialNotes()
    {
        foreach (ArrowType type in Enum.GetValues(typeof(ArrowType)))
        {
            foreach (NoteInfo mNote in MM.GetNotes(type))
            {
                AddNoteData(type, new Beat(mNote.Beat), mNote.Length);
            }
        }
        SpawnInitialNotes();
    }

    private void SpawnInitialNotes()
    {
        for (int i = 1; i <= _beatSpawnOffset; i++)
        {
            SpawnNotesAtBeat(new Beat(i));
        }
    }

    public delegate void InputHandler(ArrowData data);
    public event InputHandler NoteInputEvent;

    private void ReceiveNoteInput(ArrowData data)
    {
        GD.Print(data.ToString());
        NoteInputEvent?.Invoke(data);
    }
    #endregion

    private int AddNoteData(ArrowType type, Beat beat, double length = 0)
    {
        ArrowData result = new ArrowData(type, beat, false, length);
        if (_noteData.Count == 0)
        {
            _noteData.Add(result);
            return 0;
        }

        int index = _noteData.BinarySearch(result); //TODO: This sorts correctly, but we don't take advantage yet.
        if (index > 0)
        {
            GD.PushWarning("Duplicate note attempted add " + type + " " + beat);
            return -1;
        }
        _noteData.Insert(~index, result);
        return ~index;
    }

    //TODO: Beat spawn redundancy checking, efficiency
    private void SpawnNotesAtBeat(Beat beat)
    {
        for (int i = 0; i < _noteData.Count; i++)
        {
            if (
                _noteData[i].Beat.Loop != beat.Loop
                || (int)_noteData[i].Beat.BeatPos != (int)beat.BeatPos
            )
                continue;
            SpawnNote(i);
        }
    }

    private void SpawnNote(int index, bool newPlayerNote = false)
    {
        CM.AddNoteArrow(_noteData[index], newPlayerNote);
        _noteData[index] = new ArrowData(
            _noteData[index].Type,
            _noteData[index].Beat.IncDecLoop(1),
            _noteData[index].IsNull,
            _noteData[index].Length
        ); //Structs make me sad sometimes
    }

    private void RemoveArrow(NoteArrow arrow)
    {
        MM.CurrentChart.RemoveNote(arrow.Type, (float)arrow.Beat.BeatPos);
        int index = _noteData.FindIndex(data =>
            data.Type == arrow.Type && Math.Abs(data.Beat.BeatPos - arrow.Beat.BeatPos) < 0.01
        );
        if (index != -1)
        {
            _noteData.RemoveAt(index);
        }
        else
        {
            GD.PushError("Could not find arrow to remove " + arrow.Type + " " + arrow.Beat);
        }
        arrow.Visible = false;
        arrow.RaiseKill(arrow);
    }

    public void AddPlayerNote(ArrowType type, Beat beat, double length = 0)
    {
        int index = AddNoteData(type, beat, length); //Currently player notes aren't sorted correctly
        if (index != -1)
        {
            SpawnNote(index, true);
            MM.CurrentChart.AddNote(type, (float)beat.BeatPos, (float)length);
            GD.Print("Adding note " + type + " " + (float)beat.BeatPos);
        }
        else
            GD.PushError("Duplicate player note was attempted. (This should be stopped by CM)");
    }

    public void ProgressiveSpawnNotes(Beat beat)
    {
        Beat spawnBeat = beat + _beatSpawnOffset;
        SpawnNotesAtBeat(spawnBeat.RoundBeat());
    }
}
