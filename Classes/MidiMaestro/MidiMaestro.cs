using System;
using System.Linq;
using FunkEngine;
using Godot;

/**
 <summary> MidiMaestro: Manages reading midi file into lane note information.</summary>

 */
public partial class MidiMaestro : Resource
{
    //The four note rows that we care about
    private readonly NoteInfo[] _upNotes;
    private readonly NoteInfo[] _downNotes;
    private readonly NoteInfo[] _leftNotes;
    private readonly NoteInfo[] _rightNotes;

    public NoteChart CurrentChart { get; private set; }

    //private MidiFile strippedSong;
    /**
     * <summary>Constructor loads midi file and populates lane note arrays with midiNoteInfo</summary>
     * <param name="filePath">A string file path to a valid midi file</param>
     */
    public MidiMaestro(string filePath)
    {
        if (!OS.HasFeature("editor"))
        {
            filePath = OS.GetExecutablePath().GetBaseDir() + "/" + filePath;
        }

        if (!FileAccess.FileExists(filePath))
        {
            GD.PushError("ERROR: Unable to load level Midi file: " + filePath);
        }

        CurrentChart = ResourceLoader.Load<NoteChart>(
            filePath,
            null,
            ResourceLoader.CacheMode.Replace
        );
        _upNotes = CurrentChart.GetLane(ArrowType.Up).ToArray();
        _downNotes = CurrentChart.GetLane(ArrowType.Down).ToArray();
        _leftNotes = CurrentChart.GetLane(ArrowType.Left).ToArray();
        _rightNotes = CurrentChart.GetLane(ArrowType.Right).ToArray();
    }

    /**
     * <summary>Gets NoteInfo by lane. </summary>
     */
    public NoteInfo[] GetNotes(ArrowType arrowType)
    {
        return arrowType switch
        {
            ArrowType.Up => _upNotes,
            ArrowType.Down => _downNotes,
            ArrowType.Left => _leftNotes,
            ArrowType.Right => _rightNotes,
            _ => throw new ArgumentOutOfRangeException(nameof(arrowType), arrowType, null),
        };
    }
}
