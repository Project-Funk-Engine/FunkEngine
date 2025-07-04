using System;

namespace FunkEngine;

using Godot;

public partial class NoteArrow : Sprite2D
{ //TextRect caused issues later :)
    public static readonly string LoadPath = "res://Scenes/NoteManager/NoteArrow.tscn";
    protected const float LeftBound = -1000f;
    protected static readonly Beat BeatBound = new Beat(60);

    [Export]
    public Sprite2D OutlineSprite;

    [Export]
    public Sprite2D IconSprite;

    [Export]
    public Button Selector;

    public ArrowData Data;
    public Beat Beat => Data.Beat;
    public ArrowType Type => Data.Type;

    private double _beatTime;
    public bool IsHit;
    private bool _isQueued;

    public override void _Ready()
    {
        ZIndex = 2;
        Selector.Pressed += InvokeSelected;
    }

    public virtual void Init(CheckerData parentChecker, ArrowData arrowData, double beatTime)
    {
        Data = arrowData;
        _beatTime = beatTime;

        Position = new Vector2(GetNewPosX(), parentChecker.Node.GlobalPosition.Y);
        RotationDegrees = parentChecker.Node.RotationDegrees;
        IconSprite.Rotation = -Rotation;
        OutlineSprite.Modulate = parentChecker.Color;
    }

    public void NoteHit()
    {
        if (IsHit)
            return;
        Modulate *= .7f;
        IsHit = true;
    }

    public delegate void SelectedEventHandler(NoteArrow noteArrow);
    public event SelectedEventHandler Selected;

    private void InvokeSelected()
    {
        Selected?.Invoke(this);
    }

    public delegate void HittableEventHandler(NoteArrow note);
    public event HittableEventHandler QueueForHit;

    private void CheckHittable()
    {
        if (_isQueued || !(Beat - TimeKeeper.LastBeat <= Beat.One))
            return;
        _isQueued = true;
        QueueForHit?.Invoke(this);
    }

    public delegate void MissedEventHandler(NoteArrow note);
    public event MissedEventHandler Missed;

    protected void RaiseMissed(NoteArrow note) => Missed?.Invoke(note);

    protected virtual void CheckMissed()
    {
        if (IsHit || !(TimeKeeper.LastBeat - Beat > Beat.One))
            return;
        RaiseMissed(this);
    }

    public delegate void KillEventHandler(NoteArrow note);
    public event KillEventHandler QueueForPool;

    public void RaiseKill(NoteArrow note) => QueueForPool?.Invoke(note);

    public virtual void Recycle()
    {
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        if (IsHit)
            Modulate = Colors.White;
        IsHit = false;
        _isQueued = false;
    }

    private void BeatChecks()
    {
        CheckMissed();
        CheckHittable();
    }

    protected virtual void PosChecks()
    {
        if (Position.X < LeftBound || TimeKeeper.LastBeat - Data.Beat > BeatBound)
        {
            if (!IsHit)
                RaiseMissed(this);
            RaiseKill(this);
        }
    }

    public override void _Process(double delta)
    {
        BeatChecks(); //beat checks first, why? Because
        Vector2 newPos = Position;
        newPos.X = GetNewPosX();
        if (!float.IsNaN(newPos.X))
            Position = newPos;
        PosChecks();
    }

    private float GetNewPosX()
    {
        double relativePosition =
            (TimeKeeper.CurrentTime - _beatTime)
            / (TimeKeeper.SongLength)
            * (TimeKeeper.ChartWidth * TimeKeeper.LoopsPerSong);
        //If this should be placed for the next song replay, offset
        double posOffset =
            (
                (Beat.Loop / TimeKeeper.LoopsPerSong)
                > TimeKeeper.LastBeat.Loop / TimeKeeper.LoopsPerSong
            )
                ? TimeKeeper.ChartWidth * TimeKeeper.LoopsPerSong
                : 0;

        return (float)(-relativePosition + posOffset);
    }

    //Is the passed in beat within range of this arrow's beat, for checking if player can place near this note
    public virtual bool IsInRange(Beat incomingBeat)
    {
        int curBeatPos = (int)(Beat.BeatPos * 10);
        int incBeatPos = (int)((incomingBeat.BeatPos + Composer.BeatOffset.Value) * 10);
        return Math.Abs(curBeatPos - incBeatPos) < 1;
    }
}
