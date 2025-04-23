using System;
using Godot;

public partial class NoteInfo : Resource
{
    public NoteInfo Create(float beat = 0, float len = 0)
    {
        Beat = beat;
        Length = len;
        return this;
    }

    [Export]
    public float Beat = 0;

    [Export]
    public float Length = 0;
}
