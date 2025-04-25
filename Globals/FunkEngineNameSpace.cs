using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FunkEngine;

#region Structs
/**
 * <summary>SongData: Basic information defining the statistics of an in-battle song.</summary>
 */
public struct SongData
{
    public int Bpm;
    public double SongLength;
    public int NumLoops;
}

/**
 * <summary>ArrowData: Data representing the necessary information for each arrow checker.</summary>
 */
public struct CheckerData
{
    public Color Color;
    public string Key;
    public NoteChecker Node;
    public ArrowType Type;
}

/**
 * <summary>NoteArrowData: Data To be stored and transmitted to represent a NoteArrow.</summary>
 */
public struct ArrowData : IEquatable<ArrowData>, IComparable<ArrowData>
{
    public ArrowData(ArrowType type, Beat beat, bool isNull, double length = 0)
    {
        Beat = beat;
        Type = type;
        Length = length;
        IsNull = isNull;
    }

    public Beat Beat;
    public readonly double Length; //in beats, should never be >= loop
    public readonly ArrowType Type;
    public bool IsNull;

    public override string ToString() =>
        $"ArrowData: Dir: {Type} - {Beat} - Length: {Length} - Null: {IsNull}";

    public static ArrowData Placeholder { get; private set; } = new(default, default, true);

    public ArrowData BeatFromLength()
    {
        Beat += Length;
        return this;
    }

    public bool Equals(ArrowData other)
    {
        return Beat.Equals(other.Beat) && Type == other.Type;
    }

    public override bool Equals(object obj)
    {
        return obj is ArrowData other && Equals(other);
    }

    public static bool operator ==(ArrowData left, ArrowData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ArrowData left, ArrowData right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Beat, (int)Type);
    }

    public int CompareTo(ArrowData data) //Only care about beat for comparison
    {
        if ((int)Beat.BeatPos == (int)data.Beat.BeatPos && Beat.Loop == data.Beat.Loop)
        {
            if (Type == data.Type)
            {
                return Beat.CompareTo(data.Beat);
            }
            return Type.CompareTo(data.Type);
        }

        return Beat.CompareTo(data.Beat);
    }
}

/**
 * <summary>Beat: Data representing a beat and its loop num.</summary>
 */
public struct Beat : IEquatable<Beat>, IComparable<Beat>
{
    public int Loop = 0;
    public double BeatPos = 0;
    public static readonly Beat One = new Beat(1);
    public static readonly Beat Zero = new Beat(0);

    public Beat(double beat)
    {
        Loop = (int)(beat / TimeKeeper.BeatsPerLoop);
        BeatPos = beat % TimeKeeper.BeatsPerLoop;
    }

    public Beat(double beat, int loop)
    {
        Loop = loop;
        BeatPos = beat % TimeKeeper.BeatsPerLoop;
    }

    public double GetBeatInSong()
    {
        return (BeatPos + Loop * TimeKeeper.BeatsPerLoop) % TimeKeeper.BeatsPerSong;
    }

    public Beat IncDecLoop(int amount)
    {
        Loop += amount;
        return this;
    }

    public Beat RoundBeat()
    {
        BeatPos = (int)Math.Round(BeatPos); //This can technically overflow, but causes no bugs yet.
        return this;
    }

    public override string ToString()
    {
        return $"Beat: {BeatPos:0.0000}, Loop: {Loop}";
    }

    public static bool operator >(Beat beat1, Beat beat2)
    {
        return beat1.Loop > beat2.Loop
            || (beat1.Loop == beat2.Loop && beat1.BeatPos > beat2.BeatPos);
    }

    public static bool operator <(Beat beat1, Beat beat2)
    {
        return beat1.Loop < beat2.Loop
            || (beat1.Loop == beat2.Loop && beat1.BeatPos < beat2.BeatPos);
    }

    public static bool operator <=(Beat beat1, Beat beat2)
    {
        return beat1.Equals(beat2) || beat1 < beat2;
    }

    public static bool operator >=(Beat beat1, Beat beat2)
    {
        return beat1.Equals(beat2) || beat1 > beat2;
    }

    public static Beat operator +(Beat beat1, double beatInc)
    {
        return new Beat(beat1.BeatPos + beatInc).IncDecLoop(beat1.Loop);
    }

    public static Beat operator -(Beat beat1, double beatDec)
    {
        return new Beat(beat1.BeatPos - beatDec).IncDecLoop(beat1.Loop);
    }

    public static Beat operator -(Beat beat1, Beat beat2)
    {
        return new Beat(beat1.BeatPos - beat2.BeatPos).IncDecLoop(beat1.Loop - beat2.Loop);
    }

    public bool Equals(Beat other)
    {
        return Loop == other.Loop && BeatPos.Equals(other.BeatPos);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Loop, BeatPos);
    }

    public int CompareTo(Beat other)
    {
        var loopComparison = Loop.CompareTo(other.Loop);
        return loopComparison != 0 ? loopComparison : BeatPos.CompareTo(other.BeatPos);
    }
}
#endregion

public enum ArrowType
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3,
}

#region Interfaces

public interface IFocusableMenu
{
    IFocusableMenu Prev { get; set; }
    void OpenMenu(IFocusableMenu parentMenu);
    void PauseFocus();
    void ResumeFocus();
    void ReturnToPrev();
}
#endregion
