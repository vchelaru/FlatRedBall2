using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals.V3;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace SampleProject1.Screens;

public class GumScreen : Screen
{
    public override void CustomInitialize()
    {
        Label label = new();
        label.Text = "Hello World";
        (label.Visual as LabelVisual).Color = Color.White;

        Add(label);

        Engine.Camera.BackgroundColor = Color.DarkBlue;
        label.Anchor(Gum.Wireframe.Anchor.Center);

        base.CustomInitialize();
    }
}
